"""AppVM — root ViewModel for the GuideArch app shell.

Owns observable state that lives above any single scenario: the active
theme, the runtime mode, and the child :class:`ScenarioVM`. Every View
binds to AppVM and navigates down to ``app_vm.scenario`` for scenario-
specific state.

Cross-impl contract
-------------------
* ``theme`` — open string observable. "dark" and "light" are mandated
  in all three impls; impls may extend by calling :func:`register_theme`
  at startup (before the first AppVM is constructed) to expose
  framework-supported variants.
* ``mode`` — ``"web" | "native" | "tauri"``. Set at construction and
  immutable on the public surface. There is no ``set_mode`` command.
* ``scenario`` — :class:`ScenarioVM`. Reference is stable; only its
  internal state mutates.

Persistence
-----------
Theme round-trips through
``platformdirs.user_config_dir("guidearch") / "settings.json"`` (atomic
write — ``.tmp`` then rename). Missing / unreadable / unknown values
silently fall back to :data:`DEFAULT_THEME` and the next successful
:meth:`AppVM.set_theme` rewrites the file.

``set_theme`` validates against the registered theme set. An unknown
name appends a warning and leaves state unchanged — it never raises.
The View's theme picker should be safe to feed arbitrary user input.
"""

from __future__ import annotations

import json
from collections.abc import Callable
from pathlib import Path
from typing import Literal

import reactivex as rx
from platformdirs import user_config_dir
from reactivex.subject import Subject
from vmx.commands.relay_command import RelayCommandOf
from vmx.messages.property_changed import PropertyChangedMessage
from vmx.messages.protocols import Message
from vmx.services.message_hub import MessageHub

from guidearch.viewmodels.scenario_vm import ScenarioVM

# ---------------------------------------------------------------------------
# Theme registry
# ---------------------------------------------------------------------------

DEFAULT_THEME = "dark"

# "dark" and "light" are mandated cross-impl. Impls extend at startup
# via register_theme() before constructing the first AppVM.
_known_themes: set[str] = {"dark", "light"}


def register_theme(name: str) -> None:
    """Register an additional theme name (idempotent)."""
    _known_themes.add(name)


def known_themes() -> frozenset[str]:
    """Snapshot of currently-registered theme names."""
    return frozenset(_known_themes)


def is_known_theme(name: str) -> bool:
    return name in _known_themes


# ---------------------------------------------------------------------------
# Persistence
# ---------------------------------------------------------------------------

Mode = Literal["web", "native", "tauri"]


def _settings_path() -> Path:
    return Path(user_config_dir("guidearch")) / "settings.json"


def _load_persisted_theme() -> str:
    path = _settings_path()
    try:
        if not path.exists():
            return DEFAULT_THEME
        with path.open("r", encoding="utf-8") as fh:
            data = json.load(fh)
        if isinstance(data, dict):
            name = data.get("theme")
            if isinstance(name, str) and name in _known_themes:
                return name
    except (OSError, json.JSONDecodeError):
        # I/O, permission, malformed JSON — all non-fatal; fall through.
        pass
    return DEFAULT_THEME


def _persist_theme(theme: str) -> None:
    path = _settings_path()
    try:
        path.parent.mkdir(parents=True, exist_ok=True)
        tmp = path.with_suffix(path.suffix + ".tmp")
        with tmp.open("w", encoding="utf-8") as fh:
            json.dump({"theme": theme}, fh)
        tmp.replace(path)
    except OSError:
        # Best-effort: in-memory state is always authoritative.
        pass


# ---------------------------------------------------------------------------
# AppVM
# ---------------------------------------------------------------------------


class AppVM:
    """Root ViewModel; mirrors the TS ``AppVM`` and the C# ``ComponentVM<AppState>``.

    Construction
    ------------
    Use the :func:`make_app_vm` factory. Tests may inject ``load_theme`` /
    ``persist_theme`` stubs to round-trip through a fake store, and
    ``scenario`` to reuse an existing :class:`ScenarioVM`.
    """

    def __init__(
        self,
        *,
        mode: Mode = "native",
        load_theme: Callable[[], str] | None = None,
        persist_theme: Callable[[str], None] | None = None,
        scenario: ScenarioVM | None = None,
        hub: MessageHub[Message] | None = None,
    ) -> None:
        self._load = load_theme or _load_persisted_theme
        self._save = persist_theme or _persist_theme
        self._scenario = scenario or ScenarioVM()
        self.hub: MessageHub[Message] = hub or MessageHub()

        start = self._load()
        if start not in _known_themes:
            start = DEFAULT_THEME

        # State
        self._theme: str = start
        self._mode: Mode = mode
        self._warnings: tuple[str, ...] = ()

        # property_changed observable (matches ScenarioVM pattern)
        self._property_changed_subject: Subject[str] = Subject()

        # set_theme as a fluent command for parity with the other impls.
        # The builder's task signature is ``Callable[[T | None], None]``; wrap
        # _do_set_theme so a None param is a no-op rather than a hard error
        # (matches RelayCommandOf's "param may be missing" contract).
        def _exec(name: str | None) -> None:
            if name is not None:
                self._do_set_theme(name)

        self.set_theme_cmd: RelayCommandOf[str] = RelayCommandOf.builder().task(_exec).build()

    # ── Observable properties ────────────────────────────────────────────

    @property
    def theme(self) -> str:
        return self._theme

    @property
    def mode(self) -> Mode:
        return self._mode

    @property
    def warnings(self) -> tuple[str, ...]:
        return self._warnings

    @property
    def scenario(self) -> ScenarioVM:
        return self._scenario

    @property
    def property_changed(self) -> rx.Observable[str]:
        return self._property_changed_subject

    # ── Commands ────────────────────────────────────────────────────────

    def set_theme(self, name: str) -> None:
        """Direct callable — equivalent to ``set_theme_cmd.execute(name)``."""
        self._do_set_theme(name)

    def _do_set_theme(self, name: str) -> None:
        if name not in _known_themes:
            self._warnings = (*self._warnings, f"Unknown theme: {name}")
            self._property_changed_subject.on_next("warnings")
            self.hub.send(PropertyChangedMessage(self, "AppVM", "Warnings"))
            return
        if name == self._theme:
            return
        self._theme = name
        self._property_changed_subject.on_next("theme")
        self.hub.send(PropertyChangedMessage(self, "AppVM", "Theme"))
        self._save(name)


# ---------------------------------------------------------------------------
# Factory
# ---------------------------------------------------------------------------


def make_app_vm(
    *,
    mode: Mode = "native",
    load_theme: Callable[[], str] | None = None,
    persist_theme: Callable[[str], None] | None = None,
    scenario: ScenarioVM | None = None,
    hub: MessageHub[Message] | None = None,
) -> AppVM:
    """Construct an :class:`AppVM`. Mirrors TS ``makeAppVm`` / C# ``AppVMFactory.Create``."""
    return AppVM(
        mode=mode,
        load_theme=load_theme,
        persist_theme=persist_theme,
        scenario=scenario,
        hub=hub,
    )
