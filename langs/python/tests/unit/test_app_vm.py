"""AppVM unit tests — the 5 mandatory cross-impl checks.

Mirrors langs/typescript/tests/unit/app-vm.test.ts and
langs/csharp/tests/GuideArch.ViewModels.Tests/AppVMTests.cs.

  1. Default theme = "dark" when persistence is empty.
  2. Theme round-trips through the persistence layer.
  3. Unknown theme is non-fatal (warning appended, state unchanged).
  4. property_changed fires when theme changes.
  5. mode is set at construction and immutable thereafter.

A sixth probe checks that ``app_vm.scenario`` is a stable child reference.
"""

from __future__ import annotations

import pytest

from guidearch.viewmodels.app_vm import DEFAULT_THEME, make_app_vm


class _StubPersistence:
    def __init__(self) -> None:
        self._stored: str | None = None

    def load(self) -> str:
        return self._stored if self._stored is not None else DEFAULT_THEME

    def save(self, value: str) -> None:
        self._stored = value

    def peek(self) -> str | None:
        return self._stored


def test_defaults_to_dark_when_nothing_persisted() -> None:
    stub = _StubPersistence()
    app = make_app_vm(load_theme=stub.load, persist_theme=stub.save, mode="web")
    assert app.theme == DEFAULT_THEME
    assert app.theme == "dark"


def test_round_trips_theme_through_persistence() -> None:
    stub = _StubPersistence()
    app1 = make_app_vm(load_theme=stub.load, persist_theme=stub.save, mode="web")
    app1.set_theme("light")
    assert app1.theme == "light"
    assert stub.peek() == "light"

    # Fresh AppVM reading the same persistence restores 'light'.
    app2 = make_app_vm(load_theme=stub.load, persist_theme=stub.save, mode="web")
    assert app2.theme == "light"


def test_unknown_theme_is_non_fatal() -> None:
    stub = _StubPersistence()
    app = make_app_vm(load_theme=stub.load, persist_theme=stub.save, mode="web")

    before = app.theme
    # MUST NOT raise.
    app.set_theme("chartreuse")
    assert app.theme == before
    assert len(app.warnings) == 1
    assert "chartreuse" in app.warnings[0]
    # Persistence must NOT have been touched.
    assert stub.peek() is None


def test_property_changed_fires_on_theme_change() -> None:
    stub = _StubPersistence()
    app = make_app_vm(load_theme=stub.load, persist_theme=stub.save, mode="web")

    seen: list[str] = []
    app.property_changed.subscribe(on_next=seen.append)
    app.set_theme("light")

    assert "theme" in seen


def test_mode_is_set_at_construction_and_immutable() -> None:
    stub = _StubPersistence()
    app = make_app_vm(load_theme=stub.load, persist_theme=stub.save, mode="tauri")
    assert app.mode == "tauri"

    # No setter on the public surface.
    assert not hasattr(app, "set_mode")
    assert not hasattr(app, "set_mode_cmd")

    # Direct attribute assignment is rejected (mode is a read-only property).
    with pytest.raises(AttributeError):
        app.mode = "web"  # type: ignore[misc]


def test_scenario_is_stable_child_reference() -> None:
    stub = _StubPersistence()
    app = make_app_vm(load_theme=stub.load, persist_theme=stub.save, mode="web")
    before = app.scenario
    app.set_theme("light")
    assert app.scenario is before
