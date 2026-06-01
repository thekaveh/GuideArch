"""GuideArch Python entrypoint — M4 analysis UI (NiceGUI).

App shell
---------
- Top toolbar: New / Open / Save / Save As / [spacer] / Solve
- Tab strip (top): Decisions / Alternatives / Properties / Coefficients /
  Constraints / Results / Critical Decisions / Critical Constraints
- Status bar (bottom): scenario name · candidate count · warnings

File dialogs
------------
Web mode: ui.upload for Open; ui.download for Save.
Native mode: tkinter.filedialog (synchronous) with graceful ImportError fallback.

Re-solve
--------
v1.0 re-solves synchronously inside ScenarioVM._apply_scenario_mutation;
there is no debounce timer (deferred to v1.1 per the v1.0 status note at the
top of spec/editors.md). Editor event handlers therefore do not need to
schedule a solve — the VM mutation call already performs it.

M4 additions
------------
- Results tab is a split layout: left (60%) ranked-candidates table,
  right (40%) two stacked ui.echart instances (Chart A: bar, Chart B: triangle).
- Critical Decisions tab: read-only table of criticalDecisions.
- Critical Constraints tab: read-only table of criticalConstraints with
  faded background for redundant rows.
"""

from __future__ import annotations

import argparse
import io
import json
from collections.abc import Callable
from typing import Any

from nicegui import ui

from guidearch.view.theme import inject_css
from guidearch.viewmodels.app_vm import AppVM, Mode, make_app_vm
from guidearch.viewmodels.scenario_vm import ScenarioVM

# ---------------------------------------------------------------------------
# Globals
# ---------------------------------------------------------------------------

_app_vm: AppVM | None = None
_is_native: bool = False


def _get_app_vm() -> AppVM:
    global _app_vm
    if _app_vm is None:
        mode: Mode = "native" if _is_native else "web"
        _app_vm = make_app_vm(mode=mode)
    return _app_vm


def _get_vm() -> ScenarioVM:
    return _get_app_vm().scenario


# ---------------------------------------------------------------------------
# File-dialog helpers
# ---------------------------------------------------------------------------


def _open_file_native(vm: ScenarioVM) -> None:
    """Open a file using tkinter dialog (native mode)."""
    try:
        import tkinter as tk
        from tkinter import filedialog

        root = tk.Tk()
        root.withdraw()
        path = filedialog.askopenfilename(
            title="Open scenario",
            filetypes=[("JSON files", "*.json"), ("All files", "*.*")],
        )
        root.destroy()
        if path:
            vm.open_cmd.execute(path)
    except ImportError:
        ui.notify("tkinter unavailable; enter path in Open dialog.", color="warning")


def _save_as_native(vm: ScenarioVM) -> None:
    """Save As using tkinter dialog (native mode).

    Toasts success/failure in the same shape as _do_save (which the web-mode
    path uses) so the user gets consistent feedback regardless of mode.
    """
    try:
        import tkinter as tk
        from tkinter import filedialog

        root = tk.Tk()
        root.withdraw()
        path = filedialog.asksaveasfilename(
            title="Save scenario as",
            defaultextension=".json",
            filetypes=[("JSON files", "*.json"), ("All files", "*.*")],
        )
        root.destroy()
        if not path:
            return
        was_dirty = vm.is_dirty
        vm.save_as_cmd.execute(path)
        if vm.is_dirty and was_dirty and vm.status.startswith("Save failed:"):
            ui.notify(vm.status, color="negative")
        else:
            ui.notify("Saved.", color="positive")
    except ImportError:
        ui.notify("tkinter unavailable; use Save (web mode).", color="warning")


def _download_scenario(vm: ScenarioVM) -> None:
    """Web-mode Save: download the current scenario as JSON bytes.

    Mirrors the TS browser-mode handleSave (anchor-download) — server-side
    writes via vm.save_cmd would put the file on the NiceGUI host, which
    is wrong in web mode (user wanted their browser to download it, not
    write to a server temp dir or corrupt a bundled sample). The 'Saved.'
    notification and is_dirty clearing happen at the call site (the
    Save button handler) so success/failure feedback matches _do_save.

    The download filename prefers the scenario name over the file_path
    basename: after an upload, file_path is a server-side tmpXXX.json
    which is a useless suggestion for the user's local save target.
    """
    if vm.scenario is None:
        return
    from guidearch.viewmodels.scenario_vm import _scenario_to_dict

    data = _scenario_to_dict(vm.scenario)
    buf = io.BytesIO(json.dumps(data, indent=2, sort_keys=True, ensure_ascii=False).encode())
    # Prefer scenario.name (e.g. 'SAS', 'EDS', or whatever the user named
    # their scenario) over a server-side tmp path basename.
    safe_name = (
        "".join(c if c.isalnum() or c in "-_." else "_" for c in vm.scenario.name).strip("_")
        or "scenario"
    )
    filename = f"{safe_name}.json"
    ui.download(buf.read(), filename)


# ---------------------------------------------------------------------------
# Empty-state helper (§8) — used by every tab when no scenario is loaded
# or when a tab has no rows yet. The hero variant carries an SVG
# illustration and CTAs; the compact variant is a smaller inline panel.
# Mirrors langs/typescript/src/routes/lib/EmptyState.svelte and the
# C# MainWindow.axaml first-launch hero so the visual identity is the
# same across all three impls.
# ---------------------------------------------------------------------------


def _render_first_launch_hero(vm: ScenarioVM) -> None:
    """Render the unified first-launch hero. Called from every tab's
    "no scenario loaded" branch so the user sees the same welcome
    regardless of which tab they happen to land on first."""
    _render_empty_state(
        hero=True,
        kicker="Welcome to GuideArch",
        headline="Pick a software architecture, with fuzzy TOPSIS.",
        body=(
            "Model decisions, alternatives, weighted quality properties, "
            "and constraints. GuideArch ranks every feasible candidate, then "
            "shows which decisions move the result most and which constraints "
            "bind hardest. Start with a bundled sample to see it in action."
        ),
        primary=("Open Sample SAS", lambda: _open_sample_by_id(vm, "sas")),
        secondary=("Open Sample EDS", lambda: _open_sample_by_id(vm, "eds")),
    )


def _open_sample_by_id(vm: ScenarioVM, sample_id: str) -> None:
    """Load a bundled sample by id ("sas" / "eds"). Used by hero CTAs.

    No dirty-discard prompt — hero CTAs only render when no scenario is
    loaded, so there is nothing to lose.
    """
    from guidearch.samples import SAMPLES as _SAMPLES

    for sample in _SAMPLES:
        if str(sample["id"]) == sample_id:
            vm.open_cmd.execute(str(sample["path"]))
            return


def _hero_illustration_svg() -> str:
    """Inline SVG for the three-triangle hero motif.

    Matches the TS EmptyState and C# hero Path geometry so the apps read
    as the same product in side-by-side screenshots.
    """
    accent = "#8b5cf6"
    return (
        '<svg width="120" height="96" viewBox="0 0 120 96" fill="none" '
        'xmlns="http://www.w3.org/2000/svg" aria-hidden="true">'
        f'<path d="M10 78 L40 18 L70 78 Z" stroke="{accent}" stroke-width="1.4" '
        f'fill="{accent}" fill-opacity="0.08"/>'
        f'<path d="M40 78 L70 12 L100 78 Z" stroke="{accent}" stroke-width="1.4" '
        f'fill="{accent}" fill-opacity="0.16"/>'
        f'<path d="M70 78 L92 36 L114 78 Z" stroke="{accent}" stroke-width="1.4" '
        f'fill="{accent}" fill-opacity="0.24"/>'
        f'<line x1="6" x2="118" y1="80" y2="80" stroke="{accent}" '
        'stroke-opacity="0.4" stroke-width="1"/>'
        "</svg>"
    )


def _render_empty_state(
    *,
    headline: str,
    body: str,
    kicker: str | None = None,
    hero: bool = False,
    primary: tuple[str, Callable[[], None]] | None = None,
    secondary: tuple[str, Callable[[], None]] | None = None,
) -> None:
    """Render an empty-state block — hero or compact — at the current container.

    Parameters mirror the TS ``EmptyState.svelte`` props so the docstring
    here doubles as the cross-impl contract.
    """
    container_classes = "items-center w-full text-center " + (
        "gap-3 py-16" if hero else "gap-2 py-10"
    )
    with ui.column().classes(container_classes):
        if hero:
            ui.html(_hero_illustration_svg()).classes("mb-2")
        if kicker is not None:
            ui.label(kicker).classes(
                "text-[var(--accent-hover)] text-xs font-semibold tracking-widest uppercase"
            )
        headline_size = "text-2xl" if hero else "text-sm"
        ui.label(headline).classes(f"text-[var(--text-primary)] {headline_size} font-semibold")
        body_classes = "text-[var(--text-muted)] max-w-2xl leading-relaxed" + (
            " text-sm mt-1" if hero else " text-xs"
        )
        ui.label(body).classes(body_classes)
        if primary is not None or secondary is not None:
            with ui.row().classes("gap-2 mt-3"):
                if primary is not None:
                    ui.button(primary[0], on_click=primary[1]).props(
                        "color=primary unelevated"
                    ).classes("text-sm")
                if secondary is not None:
                    ui.button(secondary[0], on_click=secondary[1]).props(
                        "outline color=primary"
                    ).classes("text-sm")


# ---------------------------------------------------------------------------
# Status bar
# ---------------------------------------------------------------------------


def _status_text(vm: ScenarioVM) -> str:
    parts: list[str] = []
    if vm.scenario:
        parts.append(vm.scenario.name)
    parts.append(vm.status)
    if vm.warnings:
        parts.append(f"({len(vm.warnings)} warning(s))")
    if vm.is_dirty:
        parts.append("[unsaved]")
    return "  ·  ".join(parts)


# ---------------------------------------------------------------------------
# Tab: Decisions
# ---------------------------------------------------------------------------


def _render_decisions_tab(vm: ScenarioVM, container: Any) -> None:
    scenario = vm.scenario
    if scenario is None:
        # §8 first-launch hero — the Decisions tab is the default landing,
        # so this is the user's first impression.
        _render_first_launch_hero(vm)
        return

    def _refresh() -> None:
        container.clear()
        with container:
            _render_decisions_tab(vm, container)

    with ui.row().classes("items-center gap-2 mb-3"):
        ui.label("Decisions").classes("text-base font-semibold text-[var(--text-primary)]")
        ui.space()
        ui.button("+ Add Decision", on_click=lambda: _do_add_decision(vm, _refresh)).props(
            "flat color=secondary"
        )

    if not scenario.decisions:
        ui.label("No decisions yet.").classes("text-[var(--text-muted)] py-4")
        return

    # §5.5 Cards: bg-surface, border-strong, 8px radius, 16px padding
    _dec_card_cls = "w-full mb-2 bg-[var(--bg-surface)] border border-[var(--border-strong)]"
    _dec_row_cls = "items-center gap-3 w-full"
    for dec in scenario.decisions:
        with ui.card().classes(_dec_card_cls), ui.row().classes(_dec_row_cls):
            name_input = (
                ui.input(value=dec.name).props("dense outlined dark").classes("flex-1 font-medium")
            )
            name_input.on(
                "blur",
                lambda e, d=dec, ni=name_input: _do_update_decision_name(
                    vm, d.id, ni.value, _refresh
                ),
            )
            # §3 Code/ID: 12px monospace
            ui.label(f"id: {dec.id[:8]}…").classes("text-xs text-[var(--text-muted)] font-mono")
            ui.button(
                icon="delete",
                on_click=lambda d=dec: _do_delete_decision(vm, d.id, _refresh),
            ).props("flat color=negative dense")


def _do_add_decision(vm: ScenarioVM, refresh: Any) -> None:
    try:
        # The 'Add Decision' button is reachable even before any scenario is
        # loaded — auto-create an empty one so the click does what the user
        # expects rather than throwing a swallowed 'No scenario loaded.'
        if vm.scenario is None:
            vm.new_cmd.execute()
        vm.add_decision()
        refresh()
    except Exception as exc:
        ui.notify(str(exc), color="negative")


def _do_update_decision_name(vm: ScenarioVM, decision_id: str, name: str, refresh: Any) -> None:
    try:
        vm.update_decision_name(decision_id, name.strip() or "Decision")
        refresh()
    except Exception as exc:
        ui.notify(str(exc), color="negative")


def _do_delete_decision(vm: ScenarioVM, decision_id: str, refresh: Any) -> None:
    def _confirmed() -> None:
        # Was `async def` previously, called inside a lambda that returned
        # the unawaited coroutine — NiceGUI just dropped it and the Delete
        # button silently no-op'd. Sync function now actually fires.
        try:
            vm.delete_decision(decision_id)
            refresh()
        except Exception as exc:
            ui.notify(str(exc), color="negative")

    def _confirmed_then_close() -> None:
        # Sequential helper so the button's on_click lambda returns None
        # (mypy disallows tuple-of-None constructions).
        _confirmed()
        dlg.close()

    with (
        ui.dialog() as dlg,
        ui.card().classes("bg-[var(--bg-surface-3)] border border-[var(--border-strong)]"),
    ):
        ui.label("Delete this decision and all its alternatives?").classes(
            "text-[var(--text-primary)] text-base mb-4"
        )
        with ui.row():
            ui.button(
                "Delete",
                on_click=lambda: _confirmed_then_close(),
            ).props("color=negative")
            ui.button("Cancel", on_click=dlg.close).props("flat")
    dlg.open()


# ---------------------------------------------------------------------------
# Tab: Alternatives
# ---------------------------------------------------------------------------


def _render_alternatives_tab(vm: ScenarioVM, container: Any) -> None:
    scenario = vm.scenario
    if scenario is None:
        _render_first_launch_hero(vm)
        return

    def _refresh() -> None:
        container.clear()
        with container:
            _render_alternatives_tab(vm, container)

    ui.label("Alternatives").classes("text-base font-semibold text-[var(--text-primary)] mb-3")

    if not scenario.decisions:
        ui.label("Add decisions first.").classes("text-[var(--text-muted)]")
        return

    for dec in scenario.decisions:
        dec_alts = [a for a in scenario.alternatives if a.decision_id == dec.id]
        with ui.expansion(
            f"{dec.name}  ({len(dec_alts)} alternatives)",
            icon="folder",
        ).classes(
            "w-full mb-2 bg-[var(--bg-surface)] border border-[var(--border-strong)] rounded-lg"
        ):
            with ui.row().classes("items-center gap-2 mb-2"):
                ui.space()
                ui.button(
                    "+ Add Alternative",
                    on_click=lambda d=dec: _do_add_alternative(vm, d.id, _refresh),
                ).props("flat color=secondary dense")

            if not dec_alts:
                ui.label("No alternatives.").classes("text-[var(--text-muted)] text-sm py-2 pl-4")
                continue

            for alt in dec_alts:
                with ui.row().classes("items-center gap-3 w-full pl-4 py-1"):
                    name_input = (
                        ui.input(value=alt.name).props("dense outlined dark").classes("flex-1")
                    )
                    name_input.on(
                        "blur",
                        lambda e, a=alt, ni=name_input: _do_update_alternative_name(
                            vm, a.id, ni.value, _refresh
                        ),
                    )
                    ui.label(f"id: {alt.id[:8]}…").classes(
                        "text-xs text-[var(--text-muted)] font-mono"
                    )
                    ui.button(
                        icon="delete",
                        on_click=lambda a=alt: _do_delete_alternative(vm, a.id, _refresh),
                    ).props("flat color=negative dense")


def _do_add_alternative(vm: ScenarioVM, decision_id: str, refresh: Any) -> None:
    try:
        vm.add_alternative(decision_id)
        refresh()
    except Exception as exc:
        ui.notify(str(exc), color="negative")


def _do_update_alternative_name(vm: ScenarioVM, alt_id: str, name: str, refresh: Any) -> None:
    try:
        vm.update_alternative_name(alt_id, name.strip() or "Alternative")
        refresh()
    except Exception as exc:
        ui.notify(str(exc), color="negative")


def _do_delete_alternative(vm: ScenarioVM, alt_id: str, refresh: Any) -> None:
    def _confirmed() -> None:
        try:
            vm.delete_alternative(alt_id)
            refresh()
        except Exception as exc:
            ui.notify(str(exc), color="negative")

    def _confirmed_then_close() -> None:
        _confirmed()
        dlg.close()

    with (
        ui.dialog() as dlg,
        ui.card().classes("bg-[var(--bg-surface-3)] border border-[var(--border-strong)]"),
    ):
        ui.label("Delete this alternative and its coefficients?").classes(
            "text-[var(--text-primary)] text-base mb-4"
        )
        with ui.row():
            ui.button("Delete", on_click=lambda: _confirmed_then_close()).props("color=negative")
            ui.button("Cancel", on_click=dlg.close).props("flat")
    dlg.open()


# ---------------------------------------------------------------------------
# Tab: Properties
# ---------------------------------------------------------------------------


def _render_properties_tab(vm: ScenarioVM, container: Any) -> None:
    scenario = vm.scenario
    if scenario is None:
        _render_first_launch_hero(vm)
        return

    def _refresh() -> None:
        container.clear()
        with container:
            _render_properties_tab(vm, container)

    with ui.row().classes("items-center gap-2 mb-3"):
        ui.label("Properties").classes("text-base font-semibold text-[var(--text-primary)]")
        ui.space()
        ui.button("+ Add Property", on_click=lambda: _do_add_property(vm, _refresh)).props(
            "flat color=secondary"
        )

    if not scenario.properties:
        ui.label("No properties yet.").classes("text-[var(--text-muted)] py-4")
        return

    # §5.5 Cards: bg-surface, border-strong, 8px radius, 16px padding
    _prop_card_cls = "w-full mb-2 bg-[var(--bg-surface)] border border-[var(--border-strong)]"
    _prop_row_cls = "items-center gap-3 w-full flex-wrap"
    for prop in scenario.properties:
        with ui.card().classes(_prop_card_cls), ui.row().classes(_prop_row_cls):
            name_input = (
                ui.input(value=prop.name, label="Name")
                .props("dense outlined dark")
                .classes("flex-1 min-w-32")
            )
            name_input.on(
                "blur",
                lambda e, p=prop, ni=name_input: _do_update_property(
                    vm, p.id, name=ni.value, refresh=_refresh
                ),
            )

            kind_select = (
                ui.select(
                    options=["min", "max"],
                    value=prop.kind,
                    label="Kind",
                )
                .props("dense outlined dark")
                .classes("w-24")
            )
            kind_select.on(
                "update:model-value",
                lambda e, p=prop, ks=kind_select: _do_update_property(
                    vm, p.id, kind=ks.value, refresh=_refresh
                ),
            )

            weight_input = (
                ui.number(
                    value=prop.weight,
                    label="Weight",
                    min=0.0001,
                    step=0.5,
                    format="%.4g",
                )
                .props("dense outlined dark")
                .classes("w-28")
            )
            weight_input.on(
                "blur",
                lambda e, p=prop, wi=weight_input: _do_update_property(
                    vm,
                    p.id,
                    weight=float(wi.value) if wi.value else p.weight,
                    refresh=_refresh,
                ),
            )

            ui.label(f"id: {prop.id[:8]}…").classes("text-xs text-[var(--text-muted)] font-mono")
            ui.button(
                icon="delete",
                on_click=lambda p=prop: _do_delete_property(vm, p.id, _refresh),
            ).props("flat color=negative dense")


def _do_add_property(vm: ScenarioVM, refresh: Any) -> None:
    try:
        if vm.scenario is None:
            vm.new_cmd.execute()
        vm.add_property()
        refresh()
    except Exception as exc:
        ui.notify(str(exc), color="negative")


def _do_update_property(
    vm: ScenarioVM,
    prop_id: str,
    name: str | None = None,
    kind: Any = None,
    weight: float | None = None,
    refresh: Any = None,
) -> None:
    try:
        vm.update_property(prop_id, name=name, kind=kind, weight=weight)
        if refresh:
            refresh()
    except Exception as exc:
        ui.notify(str(exc), color="negative")


def _do_delete_property(vm: ScenarioVM, prop_id: str, refresh: Any) -> None:
    def _confirmed() -> None:
        try:
            vm.delete_property(prop_id)
            refresh()
        except Exception as exc:
            ui.notify(str(exc), color="negative")

    def _confirmed_then_close() -> None:
        _confirmed()
        dlg.close()

    with (
        ui.dialog() as dlg,
        ui.card().classes("bg-[var(--bg-surface-3)] border border-[var(--border-strong)]"),
    ):
        ui.label("Delete this property and its coefficients?").classes(
            "text-[var(--text-primary)] text-base mb-4"
        )
        with ui.row():
            ui.button("Delete", on_click=lambda: _confirmed_then_close()).props("color=negative")
            ui.button("Cancel", on_click=dlg.close).props("flat")
    dlg.open()


# ---------------------------------------------------------------------------
# Tab: Coefficients
# ---------------------------------------------------------------------------


def _render_coefficients_tab(vm: ScenarioVM, container: Any) -> None:
    scenario = vm.scenario
    if scenario is None:
        _render_first_launch_hero(vm)
        return

    if not scenario.properties or not scenario.alternatives:
        _render_empty_state(
            headline="Coefficient matrix is not ready",
            body=(
                "The matrix needs at least one alternative and one property. "
                "Add them on their tabs and the cells will populate here."
            ),
        )
        return
    props = scenario.properties
    alts = scenario.alternatives
    decisions = scenario.decisions

    # Build a lookup for coefficients
    coef_map: dict[tuple[str, str], Any] = {
        (c.alternative_id, c.property_id): c for c in scenario.coefficients
    }

    def _refresh() -> None:
        container.clear()
        with container:
            _render_coefficients_tab(vm, container)

    ui.label("Coefficients (fuzzy matrix)").classes(
        "text-base font-semibold text-[var(--text-primary)] mb-2"
    )
    ui.label("Format: lower · modal · upper").classes("text-xs text-[var(--text-muted)] mb-3")

    with ui.scroll_area().style("height: calc(100vh - 220px)").classes("w-full"):
        # §5.3 Table header: bg-surface, text-secondary, 12px, 32px row height, sticky.
        # Property columns flex to share the available canvas; min-w keeps
        # each column above readable width. Horizontal scroll only kicks in
        # when propCount * min-w exceeds the canvas.
        with ui.row().classes("gap-1 mb-1 bg-[var(--bg-surface)] pb-1 w-full"):
            ui.label("Alternative").classes(
                "font-semibold text-[var(--text-secondary)] w-44 shrink-0 text-xs"
            )
            for prop in props:
                kind_badge = "↓" if prop.kind == "min" else "↑"
                with ui.column().classes("items-center flex-1 min-w-[9rem]"):
                    ui.label(prop.name).classes(
                        "font-semibold text-[var(--text-secondary)] text-xs text-center"
                    )
                    ui.label(f"{kind_badge} w={prop.weight:.3g}").classes(
                        "text-xs text-[var(--text-muted)]"
                    )

        # Group by decision
        for dec in decisions:
            dec_alts = [a for a in alts if a.decision_id == dec.id]
            if not dec_alts:
                continue
            # Group header: bg-surface-2
            with ui.row().classes("w-full bg-[var(--bg-surface-2)] rounded px-2 py-1 mb-1"):
                ui.label(dec.name).classes("font-semibold text-[var(--text-primary)] text-sm")

            for alt in dec_alts:
                # §5.3 Body rows: 36px height, border-subtle between rows
                with ui.row().classes("gap-1 mb-1 items-center w-full"):
                    ui.label(alt.name).classes(
                        "text-[var(--text-primary)] text-sm w-44 shrink-0 truncate"
                    )
                    for prop in props:
                        coef = coef_map.get((alt.id, prop.id))
                        if coef is None:
                            ui.label("—").classes(
                                "flex-1 min-w-[9rem] text-center text-[var(--text-muted)]"
                            )
                            continue

                        lower_v = coef.value.lower
                        modal_v = coef.value.modal
                        upper_v = coef.value.upper
                        # §2.4 warning token for invalid fuzzy triplet
                        warn_color = (
                            "border-[var(--warning)]"
                            if lower_v > modal_v or modal_v > upper_v
                            else "border-[var(--border-strong)]"
                        )

                        cell_classes = (
                            "gap-1 items-center border rounded p-1 "
                            f"flex-1 min-w-[9rem] {warn_color}"
                        )
                        with ui.row().classes(cell_classes):
                            l_in = (
                                ui.number(
                                    value=lower_v,
                                    format="%.3g",
                                    step=0.1,
                                )
                                .props("dense dark borderless")
                                .classes("w-10 font-mono text-xs")
                            )
                            ui.label("·").classes("text-[var(--text-muted)] text-xs")
                            m_in = (
                                ui.number(
                                    value=modal_v,
                                    format="%.3g",
                                    step=0.1,
                                )
                                .props("dense dark borderless")
                                .classes("w-10 font-mono text-xs")
                            )
                            ui.label("·").classes("text-[var(--text-muted)] text-xs")
                            u_in = (
                                ui.number(
                                    value=upper_v,
                                    format="%.3g",
                                    step=0.1,
                                )
                                .props("dense dark borderless")
                                .classes("w-10 font-mono text-xs")
                            )

                        def _make_blur_handler(
                            a_id: str,
                            p_id: str,
                            li: Any,
                            mi: Any,
                            ui_: Any,
                            rf: Any,
                        ) -> Any:
                            def _handler(_: Any = None) -> None:
                                try:
                                    vm.update_coefficient(
                                        a_id,
                                        p_id,
                                        float(li.value or 0),
                                        float(mi.value or 0),
                                        float(ui_.value or 0),
                                    )
                                    rf()
                                except Exception as exc:
                                    ui.notify(str(exc), color="negative")

                            return _handler

                        handler = _make_blur_handler(alt.id, prop.id, l_in, m_in, u_in, _refresh)
                        l_in.on("blur", handler)
                        m_in.on("blur", handler)
                        u_in.on("blur", handler)


# ---------------------------------------------------------------------------
# Tab: Constraints
# ---------------------------------------------------------------------------


def _render_constraints_tab(vm: ScenarioVM, container: Any) -> None:
    scenario = vm.scenario
    if scenario is None:
        _render_first_launch_hero(vm)
        return

    def _refresh() -> None:
        container.clear()
        with container:
            _render_constraints_tab(vm, container)

    ui.label("Constraints").classes("text-base font-semibold text-[var(--text-primary)] mb-2")

    with ui.tabs() as sub_tabs:
        tab_thresh = ui.tab("Threshold")
        tab_dep = ui.tab("Dependency")
        tab_conf = ui.tab("Conflict")

    with ui.tab_panels(sub_tabs, value=tab_thresh).classes("w-full"):
        with ui.tab_panel(tab_thresh):
            _render_threshold_sub(vm, scenario, _refresh)
        with ui.tab_panel(tab_dep):
            _render_dependency_sub(vm, scenario, _refresh)
        with ui.tab_panel(tab_conf):
            _render_conflict_sub(vm, scenario, _refresh)


def _render_threshold_sub(vm: ScenarioVM, scenario: Any, refresh: Any) -> None:
    from guidearch.models.constraint import ThresholdConstraint

    props = scenario.properties
    prop_options = {p.id: p.name for p in props}

    with ui.row().classes("items-center gap-2 mb-3"):
        ui.space()
        if props:
            ui.button(
                "+ Add Threshold",
                on_click=lambda: _do_add_threshold(vm, props[0].id, refresh),
            ).props("flat color=secondary")

    threshold_cs = [
        (i, c) for i, c in enumerate(scenario.constraints) if isinstance(c, ThresholdConstraint)
    ]

    if not threshold_cs:
        ui.label("No threshold constraints.").classes("text-[var(--text-muted)] py-4")
        return

    for idx, c in threshold_cs:
        invalid = c.min is not None and c.max is not None and c.min > c.max
        # §2.4 danger for invalid range, border-strong otherwise
        card_border = "border-[var(--danger)]" if invalid else "border-[var(--border-strong)]"
        with ui.card().classes(  # noqa: SIM117
            f"w-full mb-2 bg-[var(--bg-surface)] border {card_border}"
        ):
            with ui.row().classes("items-center gap-3 flex-wrap"):
                # Property dropdown
                prop_sel = (
                    ui.select(
                        options=prop_options,
                        value=c.property_id,
                        label="Property",
                    )
                    .props("dense outlined dark")
                    .classes("w-48")
                )
                prop_sel.on(
                    "update:model-value",
                    lambda e, i=idx, ps=prop_sel: _do_update_threshold(
                        vm, i, property_id=ps.value, refresh=refresh
                    ),
                )

                # Min
                min_in = (
                    ui.number(
                        value=c.min if c.min is not None else None,
                        label="Min",
                        step=0.1,
                    )
                    .props("dense outlined dark clearable")
                    .classes("w-24")
                )
                min_in.on(
                    "blur",
                    lambda e, i=idx, mi=min_in: _do_update_threshold(
                        vm,
                        i,
                        min_val=float(mi.value) if mi.value is not None else None,
                        refresh=refresh,
                    ),
                )

                # Max
                max_in = (
                    ui.number(
                        value=c.max if c.max is not None else None,
                        label="Max",
                        step=0.1,
                    )
                    .props("dense outlined dark clearable")
                    .classes("w-24")
                )
                max_in.on(
                    "blur",
                    lambda e, i=idx, ma=max_in: _do_update_threshold(
                        vm,
                        i,
                        max_val=float(ma.value) if ma.value is not None else None,
                        refresh=refresh,
                    ),
                )

                if invalid:
                    ui.badge("min > max", color="negative").classes("ml-2")

                ui.button(
                    icon="delete",
                    on_click=lambda i=idx: _do_delete_constraint(vm, i, refresh),
                ).props("flat color=negative dense")


def _do_add_threshold(vm: ScenarioVM, prop_id: str, refresh: Any) -> None:
    try:
        # Provide a default min bound so the call satisfies the
        # 'at least one of min/max' invariant. The user edits the value
        # in-place after the row appears. C#'s Add path also seeds a
        # default; calling with both bounds None throws.
        vm.add_threshold_constraint(prop_id, min_val=0.0)
        refresh()
    except Exception as exc:
        ui.notify(str(exc), color="negative")


def _do_update_threshold(
    vm: ScenarioVM,
    index: int,
    property_id: str | None = None,
    min_val: Any = ...,
    max_val: Any = ...,
    refresh: Any = None,
) -> None:
    from guidearch.viewmodels.scenario_vm import _UNSET

    try:
        vm.update_threshold_constraint(
            index,
            property_id=property_id,
            min_val=min_val if min_val is not ... else _UNSET,
            max_val=max_val if max_val is not ... else _UNSET,
        )
        if refresh:
            refresh()
    except Exception as exc:
        ui.notify(str(exc), color="negative")


def _render_dependency_sub(vm: ScenarioVM, scenario: Any, refresh: Any) -> None:
    from guidearch.models.constraint import DependencyConstraint

    alts = scenario.alternatives
    decisions = scenario.decisions
    dec_map = {d.id: d.name for d in decisions}
    alt_options = {a.id: f"{dec_map.get(a.decision_id, '?')} / {a.name}" for a in alts}

    with ui.row().classes("items-center gap-2 mb-3"):
        ui.space()
        if len(alts) >= 2:
            ui.button(
                "+ Add Dependency",
                on_click=lambda: _do_add_dependency(vm, alts[0].id, alts[1].id, refresh),
            ).props("flat color=secondary")

    dep_cs = [
        (i, c) for i, c in enumerate(scenario.constraints) if isinstance(c, DependencyConstraint)
    ]

    if not dep_cs:
        ui.label("No dependency constraints.").classes("text-[var(--text-muted)] py-4")
        return

    for idx, c in dep_cs:
        self_edge = c.source_alternative_id == c.target_alternative_id
        card_border = "border-[var(--warning)]" if self_edge else "border-[var(--border-strong)]"
        with ui.card().classes(  # noqa: SIM117
            f"w-full mb-2 bg-[var(--bg-surface)] border {card_border}"
        ):
            with ui.row().classes("items-center gap-3 flex-wrap"):
                src_sel = (
                    ui.select(
                        options=alt_options,
                        value=c.source_alternative_id,
                        label="Source",
                    )
                    .props("dense outlined dark")
                    .classes("flex-1 min-w-48")
                )
                src_sel.on(
                    "update:model-value",
                    lambda e, i=idx, ss=src_sel: _do_update_dependency(
                        vm, i, source_id=ss.value, refresh=refresh
                    ),
                )

                ui.label("→").classes("text-[var(--text-secondary)] text-lg")

                tgt_sel = (
                    ui.select(
                        options=alt_options,
                        value=c.target_alternative_id,
                        label="Target",
                    )
                    .props("dense outlined dark")
                    .classes("flex-1 min-w-48")
                )
                tgt_sel.on(
                    "update:model-value",
                    lambda e, i=idx, ts=tgt_sel: _do_update_dependency(
                        vm, i, target_id=ts.value, refresh=refresh
                    ),
                )

                if self_edge:
                    ui.badge("self-edge", color="warning").classes("ml-2")

                ui.button(
                    icon="delete",
                    on_click=lambda i=idx: _do_delete_constraint(vm, i, refresh),
                ).props("flat color=negative dense")


def _do_add_dependency(vm: ScenarioVM, src: str, tgt: str, refresh: Any) -> None:
    try:
        vm.add_dependency_constraint(src, tgt)
        refresh()
    except Exception as exc:
        ui.notify(str(exc), color="negative")


def _do_update_dependency(
    vm: ScenarioVM,
    index: int,
    source_id: str | None = None,
    target_id: str | None = None,
    refresh: Any = None,
) -> None:
    try:
        vm.update_dependency_constraint(index, source_id=source_id, target_id=target_id)
        if refresh:
            refresh()
    except Exception as exc:
        ui.notify(str(exc), color="negative")


def _render_conflict_sub(vm: ScenarioVM, scenario: Any, refresh: Any) -> None:
    from guidearch.models.constraint import ConflictConstraint

    alts = scenario.alternatives
    decisions = scenario.decisions
    dec_map = {d.id: d.name for d in decisions}
    alt_options = {a.id: f"{dec_map.get(a.decision_id, '?')} / {a.name}" for a in alts}

    with ui.row().classes("items-center gap-2 mb-3"):
        ui.space()
        if len(alts) >= 2:
            ui.button(
                "+ Add Conflict",
                on_click=lambda: _do_add_conflict(vm, alts[0].id, alts[1].id, refresh),
            ).props("flat color=secondary")

    conf_cs = [
        (i, c) for i, c in enumerate(scenario.constraints) if isinstance(c, ConflictConstraint)
    ]

    if not conf_cs:
        ui.label("No conflict constraints.").classes("text-[var(--text-muted)] py-4")
        return

    for idx, c in conf_cs:
        self_edge = c.alternative_a_id == c.alternative_b_id
        card_border = "border-[var(--warning)]" if self_edge else "border-[var(--border-strong)]"
        with ui.card().classes(  # noqa: SIM117
            f"w-full mb-2 bg-[var(--bg-surface)] border {card_border}"
        ):
            with ui.row().classes("items-center gap-3 flex-wrap"):
                a_sel = (
                    ui.select(
                        options=alt_options,
                        value=c.alternative_a_id,
                        label="Alt A",
                    )
                    .props("dense outlined dark")
                    .classes("flex-1 min-w-48")
                )
                a_sel.on(
                    "update:model-value",
                    lambda e, i=idx, a=a_sel: _do_update_conflict(
                        vm, i, alt_a_id=a.value, refresh=refresh
                    ),
                )

                ui.label("✗").classes("text-[var(--danger)] text-lg")

                b_sel = (
                    ui.select(
                        options=alt_options,
                        value=c.alternative_b_id,
                        label="Alt B",
                    )
                    .props("dense outlined dark")
                    .classes("flex-1 min-w-48")
                )
                b_sel.on(
                    "update:model-value",
                    lambda e, i=idx, b=b_sel: _do_update_conflict(
                        vm, i, alt_b_id=b.value, refresh=refresh
                    ),
                )

                if self_edge:
                    ui.badge("self-edge", color="warning").classes("ml-2")

                ui.button(
                    icon="delete",
                    on_click=lambda i=idx: _do_delete_constraint(vm, i, refresh),
                ).props("flat color=negative dense")


def _do_add_conflict(vm: ScenarioVM, a: str, b: str, refresh: Any) -> None:
    try:
        vm.add_conflict_constraint(a, b)
        refresh()
    except Exception as exc:
        ui.notify(str(exc), color="negative")


def _do_update_conflict(
    vm: ScenarioVM,
    index: int,
    alt_a_id: str | None = None,
    alt_b_id: str | None = None,
    refresh: Any = None,
) -> None:
    try:
        vm.update_conflict_constraint(index, alt_a_id=alt_a_id, alt_b_id=alt_b_id)
        if refresh:
            refresh()
    except Exception as exc:
        ui.notify(str(exc), color="negative")


def _do_delete_constraint(vm: ScenarioVM, index: int, refresh: Any) -> None:
    try:
        vm.delete_constraint(index)
        refresh()
    except Exception as exc:
        ui.notify(str(exc), color="negative")


# ---------------------------------------------------------------------------
# Tab: Results  (M4: split pane — table left, charts right)
# ---------------------------------------------------------------------------


def _build_alt_maps(
    vm: ScenarioVM,
) -> tuple[dict[str, str], dict[str, str], dict[str, str]]:
    """Return (alt_id→name, alt_id→dec_name, decision_id→dec_name) maps."""
    scenario = vm.scenario
    if scenario is None:
        return {}, {}, {}
    dec_map = {d.id: d.name for d in scenario.decisions}
    alt_map = {a.id: a.name for a in scenario.alternatives}
    alt_dec = {a.id: dec_map.get(a.decision_id, "?") for a in scenario.alternatives}
    return alt_map, alt_dec, dec_map


def _render_results_tab(vm: ScenarioVM, container: Any) -> None:
    scenario = vm.scenario
    if scenario is None:
        _render_first_launch_hero(vm)
        return

    candidates = vm.candidates
    if not candidates:
        with ui.column().classes("items-center justify-center w-full py-16 gap-2"):
            ui.label("No candidates — check scenario and constraints.").classes(
                "text-[var(--text-secondary)] text-sm font-medium"
            )
            ui.label("All architecture combinations may be eliminated by constraints.").classes(
                "text-[var(--text-muted)] text-xs"
            )
        return

    alt_map, alt_dec, _ = _build_alt_maps(vm)
    prop_names = [p.name for p in scenario.properties] if scenario else []

    top30 = candidates[:30]
    top50 = candidates[:50]

    # ── Candidates table columns/rows (left pane) ────────────────────────────
    columns: list[dict[str, Any]] = [
        {
            "name": "rank",
            "label": "#",
            "field": "rank",
            "align": "right",
            "sortable": True,
            "style": "width: 48px; font-variant-numeric: tabular-nums;",
        },
        {
            "name": "score",
            "label": "Score",
            "field": "score",
            "align": "right",
            "sortable": True,
            "style": "width: 90px; font-variant-numeric: tabular-nums;",
        },
        {
            "name": "alternatives",
            "label": "Alternatives",
            "field": "alternatives",
            "align": "left",
        },
    ]

    # ── Split layout ─────────────────────────────────────────────────────────
    with ui.row().classes("w-full gap-4 items-start"):
        # Left: table (~58%)
        with ui.column().classes("flex-1 min-w-0"):
            ui.label(f"Top {len(top50)} of {len(candidates)} ranked candidates").classes(
                "text-sm font-semibold text-[var(--text-primary)] mb-2"
            )

            sel_idx = vm.selected_candidate_index
            # Build rows with selection highlight
            sel_rows = []
            for cand in top50:
                alt_labels = [
                    f"{alt_dec.get(aid, '?')}/{alt_map.get(aid, aid[:8])}"
                    for aid in cand.alternative_ids
                ]
                display = ", ".join(alt_labels[:4])
                if len(alt_labels) > 4:
                    display += f", … +{len(alt_labels) - 4}"
                row_class = ""
                if sel_idx is not None and cand.rank == sel_idx:
                    # §5.3 Selected: accent-muted background
                    row_class = "bg-[var(--accent-muted)]"
                sel_rows.append(
                    {
                        "rank": cand.rank,
                        "score": f"{cand.score:.6g}",
                        "alternatives": display,
                        "_class": row_class,
                    }
                )

            tbl = (
                ui.table(columns=columns, rows=sel_rows, row_key="rank")
                .classes("w-full max-h-screen overflow-y-auto")
                .props("dark dense flat")
            )

            # Row click selects candidate
            tbl.on(
                "row-click",
                lambda e, v=vm: _on_candidate_row_click(v, e),
            )

        # Right: chart sub-tabs (Ranking / Profile / Compare). Each tab
        # gets the full vertical space of the column instead of three
        # cramped panels. Default = Ranking (familiar entry point).
        with ui.column().classes("w-96 gap-2 shrink-0"):
            from guidearch.view.chart_data import (
                candidates_bar_option,
                comparison_option,
                triangle_option,
            )

            with ui.tabs().classes("w-full bg-[var(--bg-surface-2)] rounded") as chart_tabs:
                tab_rank = ui.tab("Ranking")
                tab_profile = ui.tab("Profile")
                tab_compare = ui.tab("Compare")

            with (
                ui.tab_panels(chart_tabs, value=tab_rank)
                .classes("w-full bg-transparent")
                .props("dark")
            ):
                with ui.tab_panel(tab_rank).classes("p-0"):
                    # Chart A — bar chart
                    chart_a_opt = candidates_bar_option(top30, alt_map, vm.selected_candidate_index)
                    chart_a = (
                        ui.echart(chart_a_opt)
                        .classes("w-full rounded border border-[var(--border-strong)]")
                        .style("height:520px; background: var(--bg-surface);")
                    )
                    chart_a.on(
                        "click",
                        lambda e, v=vm, cands=top30: _on_chart_a_click(v, cands, e),
                    )

                with ui.tab_panel(tab_profile).classes("p-0"):
                    # Chart B — triangle for selected candidate
                    sel = vm.selected_candidate_index
                    if sel is not None and sel < len(candidates):
                        selected_cand = candidates[sel]
                        chart_b_opt = triangle_option(selected_cand, prop_names, alt_map)
                    else:
                        chart_b_opt = triangle_option(candidates[0], prop_names, alt_map)
                    (
                        ui.echart(chart_b_opt)
                        .classes("w-full rounded border border-[var(--border-strong)]")
                        .style("height:520px; background: var(--bg-surface);")
                    )

                with ui.tab_panel(tab_compare).classes("p-0"):
                    # Chart C — top-10 polyline comparison across properties
                    chart_c_opt = comparison_option(
                        candidates,
                        tuple(scenario.properties),
                        tuple(scenario.coefficients),
                        vm.selected_candidate_index,
                    )
                    chart_c = (
                        ui.echart(chart_c_opt)
                        .classes("w-full rounded border border-[var(--border-strong)]")
                        .style("height:520px; background: var(--bg-surface);")
                    )
                    chart_c.on(
                        "click",
                        lambda e, v=vm, cands=candidates: _on_chart_c_click(v, cands, e),
                    )


def _on_candidate_row_click(vm: ScenarioVM, event: Any) -> None:
    """Handle row-click in the candidates table: set selected_candidate_index.

    NiceGUI passes a click event whose `args.row` is a dict; the dict may or
    may not contain a 'rank' key depending on the table-column projection.
    We only swallow KeyError / TypeError / ValueError — anything else is a
    real bug (e.g. an AttributeError on `event.args`) and should surface in
    the NiceGUI log instead of being silently lost.
    """
    try:
        rank = int(event.args["row"]["rank"])
    except (KeyError, TypeError, ValueError):
        return
    vm.selected_candidate_index = rank


def _on_chart_a_click(vm: ScenarioVM, top_candidates: tuple[Any, ...], event: Any) -> None:
    """Handle ECharts click on Chart A bar: set selected_candidate_index.

    ECharts passes back `event.args` whose `dataIndex` is the bar index
    within the series. Catch KeyError / TypeError / ValueError only —
    anything else propagates so we don't mask real wiring bugs.
    """
    try:
        data_index = int(event.args.get("dataIndex", 0))
    except (KeyError, TypeError, ValueError):
        return
    if 0 <= data_index < len(top_candidates):
        vm.selected_candidate_index = top_candidates[data_index].rank


def _on_chart_c_click(vm: ScenarioVM, candidates: tuple[Any, ...], event: Any) -> None:
    """Handle clicks on Chart C — top-10 comparison polylines.

    ECharts click events carry ``seriesIndex`` for the line that was hit.
    Each series corresponds to candidates[seriesIndex]; we use that to
    drive selection. Legend clicks fire the same handler and resolve the
    same way. Narrow exception scope mirrors Chart A's handler.
    """
    try:
        series_index = int(event.args.get("seriesIndex", -1))
    except (KeyError, TypeError, ValueError):
        return
    if 0 <= series_index < len(candidates):
        vm.selected_candidate_index = candidates[series_index].rank


# ---------------------------------------------------------------------------
# Tab: Critical Decisions
# ---------------------------------------------------------------------------


def _render_critical_decisions_tab(vm: ScenarioVM, container: Any) -> None:
    scenario = vm.scenario
    if scenario is None:
        _render_first_launch_hero(vm)
        return

    crit = vm.critical_decisions
    if not crit:
        with ui.column().classes("items-center justify-center w-full py-16 gap-2"):
            ui.label("No critical decisions computed yet.").classes(
                "text-[var(--text-secondary)] text-sm font-medium"
            )
            ui.label("Click Solve to compute critical decisions.").classes(
                "text-[var(--text-muted)] text-xs"
            )
        return

    dec_map = {d.id: d.name for d in scenario.decisions}

    # Sort ascending by rank (lower score = more critical)
    sorted_crit = sorted(crit, key=lambda c: c.rank)

    columns: list[dict[str, Any]] = [
        {"name": "rank", "label": "Rank", "field": "rank", "align": "right", "sortable": True},
        {"name": "decision", "label": "Decision", "field": "decision", "align": "left"},
        {"name": "score", "label": "Score", "field": "score", "align": "right", "sortable": True},
        {"name": "triangular", "label": "Triangular value", "field": "triangular", "align": "left"},
        {"name": "normalized", "label": "Normalized", "field": "normalized", "align": "left"},
    ]

    rows = []
    for cd in sorted_crit:
        tv = cd.triangular_value
        nv = cd.normalized_value
        rows.append(
            {
                "rank": cd.rank,
                "decision": dec_map.get(cd.decision_id, cd.decision_id[:8]),
                "score": f"{cd.score:.6g}",
                "triangular": f"({tv.lower:.4g}, {tv.modal:.4g}, {tv.upper:.4g})",
                "normalized": f"({nv.positive:.4g}, {nv.average:.4g}, {nv.negative:.4g})",
            }
        )

    ui.label("Critical Decisions").classes(
        "text-base font-semibold text-[var(--text-primary)] mb-2"
    )
    ui.label("Sorted ascending by rank (lower score = more critical)").classes(
        "text-xs text-[var(--text-muted)] mb-3"
    )
    ui.table(columns=columns, rows=rows, row_key="rank").classes(
        "w-full max-h-screen overflow-y-auto"
    ).props("dark dense flat")


# ---------------------------------------------------------------------------
# Tab: Critical Constraints
# ---------------------------------------------------------------------------


def _render_critical_constraints_tab(vm: ScenarioVM, container: Any) -> None:
    scenario = vm.scenario
    if scenario is None:
        _render_first_launch_hero(vm)
        return

    crit = vm.critical_constraints
    if not crit:
        with ui.column().classes("items-center justify-center w-full py-16 gap-2"):
            ui.label("No constraints active — add constraints to see analysis.").classes(
                "text-[var(--text-secondary)] text-sm font-medium"
            )
            ui.label("Threshold, dependency, and conflict constraints appear here.").classes(
                "text-[var(--text-muted)] text-xs"
            )
        return

    # Sort descending by eliminated (most-binding first)
    sorted_crit = sorted(crit, key=lambda c: c.eliminated, reverse=True)

    rows = []
    for cc in sorted_crit:
        pct = 100.0 * cc.eliminated / cc.total if cc.total > 0 else 0.0
        rows.append(
            {
                "index": cc.constraint_index,
                "kind": cc.kind,
                "eliminated": cc.eliminated,
                "total": cc.total,
                "elim_pct": f"{pct:.2f}%",
                "redundant": "Yes" if cc.redundant else "No",
                "_redundant": cc.redundant,
            }
        )

    ui.label("Critical Constraints").classes(
        "text-base font-semibold text-[var(--text-primary)] mb-2"
    )
    ui.label(
        "Sorted descending by eliminated (most-binding first). "
        "Redundant rows shown with faded background."
    ).classes("text-xs text-[var(--text-muted)] mb-3")

    # Render as a custom table to support per-row background styling
    with ui.scroll_area().classes("w-full max-h-screen"):
        # §5.3 Header: bg-surface, text-secondary, 12px, 32px height
        with ui.row().classes(
            "w-full bg-[var(--bg-surface)] rounded px-2 py-1 mb-1 "
            "font-semibold text-[var(--text-secondary)] text-xs gap-0"
        ):
            ui.label("Index").classes("w-12 text-right pr-2")
            ui.label("Kind").classes("w-28 pr-2")
            ui.label("Eliminated").classes("w-24 text-right pr-2")
            ui.label("Total").classes("w-20 text-right pr-2")
            ui.label("Eliminated %").classes("w-20 text-right pr-2")
            ui.label("Redundant").classes("w-20 text-center")

        for row in rows:
            redundant = row["_redundant"]
            # Redundant rows faded per spec §5.3; bg-page for body rows
            row_cls = "opacity-40" if redundant else ""
            bg_cls = "bg-[var(--bg-page)]" if not redundant else "bg-[var(--bg-surface)]"
            with ui.row().classes(
                f"w-full {bg_cls} border border-[var(--border-subtle)] rounded px-2 py-1 mb-1 "
                f"text-[var(--text-primary)] text-xs gap-0 {row_cls}"
            ):
                ui.label(str(row["index"])).classes("w-12 text-right pr-2 font-mono")
                ui.label(str(row["kind"])).classes("w-28 pr-2")
                ui.label(str(row["eliminated"])).classes("w-24 text-right pr-2 font-mono")
                ui.label(str(row["total"])).classes("w-20 text-right pr-2 font-mono")
                ui.label(str(row["elim_pct"])).classes("w-20 text-right pr-2 font-mono")
                # Redundant=Yes → warning color; No → neutral (secondary)
                badge_color = "warning" if redundant else "grey-8"
                ui.badge(str(row["redundant"]), color=badge_color).classes("w-20 justify-center")


# ---------------------------------------------------------------------------
# Main page
# ---------------------------------------------------------------------------


def index() -> None:
    """Render the full M3 app shell."""
    app_vm = _get_app_vm()
    vm = app_vm.scenario

    # Theme observable → NiceGUI Quasar dark mode. AppVM owns the source
    # of truth; we apply it here so a future theme picker can flip
    # ui.dark_mode() from one place.
    dark_mode = ui.dark_mode()

    def _apply_theme() -> None:
        if app_vm.theme == "light":
            dark_mode.disable()
        else:
            dark_mode.enable()

    def _on_property_changed(name: str) -> None:
        if name == "theme":
            _apply_theme()

    _apply_theme()
    app_vm.property_changed.subscribe(on_next=_on_property_changed)
    inject_css()
    ui.add_head_html("<style>.sticky-top { position: sticky; top: 0; z-index: 10; }</style>")

    with ui.column().classes("w-full min-h-screen gap-0"):
        # Build the Open dialog first so it's always defined before the toolbar refs it
        from nicegui.elements.dialog import Dialog as _Dialog

        open_dialog: _Dialog
        if not _is_native:
            with (
                ui.dialog() as open_dialog,
                ui.card().classes(
                    "bg-[var(--bg-surface)] border border-[var(--border-strong)] w-96"
                ),
            ):
                ui.label("Open Scenario File").classes(
                    "text-[var(--text-primary)] text-base font-semibold mb-2"
                )
                path_input = ui.input("Paste file path…").props("dark outlined").classes("w-full")
                ui.button(
                    "Open by path",
                    on_click=lambda: _do_open_by_path(vm, path_input.value, open_dialog),
                ).props("color=primary")
                ui.separator().classes("my-2")
                ui.label("Or upload a JSON file:").classes("text-[var(--text-secondary)] text-sm")
                ui.upload(
                    label="Choose file",
                    on_upload=lambda e: _do_open_upload(vm, e, open_dialog),
                    auto_upload=True,
                ).props("dark accept=.json")

        # ── Toolbar (§6: 56px tall, 24px h-padding, ghost buttons) ─────────
        with ui.row().classes(
            "guidearch-toolbar w-full items-center "
            "bg-[var(--bg-page)] border-b border-[var(--border-subtle)] gap-2"
        ):
            ui.label("GuideArch").classes("text-xl font-bold text-[var(--accent)] mr-4")

            def _do_new_guarded() -> None:
                was_dirty = vm.is_dirty
                _do_new(vm)
                # _do_new always succeeds, so wasDirty deterministically
                # implies a discard happened.
                if was_dirty:
                    _stamp_discard_warning("Create a new scenario")

            ui.button("New", icon="add", on_click=_do_new_guarded).props("flat color=white")

            def _open_native_guarded() -> None:
                was_dirty = vm.is_dirty
                _do_open_native(vm)
                # Only stamp if the open actually replaced the scenario.
                # Loader failures and cancelled pickers both leave is_dirty
                # at True.
                if was_dirty and not vm.is_dirty:
                    _stamp_discard_warning("Open")

            def _open_dialog_guarded() -> None:
                # Web-mode Open opens a dialog where the user may either
                # paste a path or upload a file. The stamp-warning happens
                # in _do_open_by_path / _do_open_upload on success, not
                # here — opening the dialog itself is not the discard
                # event. (Mirrors the C# OnOpenClicked structure where
                # the warning is stamped post-picker, not pre-picker.)
                open_dialog.open()

            if _is_native:
                ui.button("Open…", icon="folder_open", on_click=_open_native_guarded).props(
                    "flat color=white"
                )
            else:
                ui.button("Open…", icon="folder_open", on_click=_open_dialog_guarded).props(
                    "flat color=white"
                )

            from guidearch.samples import SAMPLES as _SAMPLES

            for _sample in _SAMPLES:
                _sample_path = str(_sample["path"])
                _sample_label = str(_sample["label"]).split(" — ")[0]  # "SAS" or "EDS"

                def _open_sample_guarded(p: str = _sample_path) -> None:
                    was_dirty = vm.is_dirty
                    vm.open_cmd.execute(p)
                    # Only stamp when the open actually succeeded — a load
                    # failure leaves is_dirty at True (open_cmd's catch path).
                    if was_dirty and not vm.is_dirty:
                        _stamp_discard_warning("Open Sample")

                ui.button(
                    f"Open Sample {_sample_label}",
                    icon="science",
                    on_click=_open_sample_guarded,
                ).props("flat color=primary")

            # Web-mode Save = anchor-download, not server-side write. Mirrors
            # the TS Toolbar.handleSave: a user-clicked Save should never
            # write to the NiceGUI host's disk (multi-user deploys would
            # otherwise share / overwrite scenario files). Native mode keeps
            # _do_save which routes through vm.save_cmd → local file system.
            if _is_native:
                save_btn = ui.button("Save", icon="save", on_click=lambda: _do_save(vm)).props(
                    "flat color=white"
                )
                if vm.scenario is None or vm.file_path is None:
                    save_btn.props(add="disabled")
            else:
                save_btn = ui.button(
                    "Save",
                    icon="save",
                    on_click=lambda: _do_save_browser(vm),
                ).props("flat color=white")
                if vm.scenario is None:
                    save_btn.props(add="disabled")

            if _is_native:
                ui.button(
                    "Save As…",
                    icon="save_as",
                    on_click=lambda: _save_as_native(vm),
                ).props("flat color=white")
            else:
                # Web-mode Save As also goes through _do_save_browser so
                # the post-download 'Downloaded.' toast + is_dirty clear
                # run (Save and Save As share the anchor-download path —
                # the user picks the filename in the browser dialog).
                ui.button(
                    "Save As…",
                    icon="save_as",
                    on_click=lambda: _do_save_browser(vm),
                ).props("flat color=white")

            ui.space()

            # Theme toggle: flips AppVM.Theme; the on-page dark_mode
            # subscription rewires the Quasar dark class on each change.
            theme_btn = ui.button(
                icon="dark_mode" if app_vm.theme == "light" else "light_mode",
                on_click=lambda: app_vm.set_theme("light" if app_vm.theme == "dark" else "dark"),
            ).props("flat color=white")
            theme_btn.tooltip("Toggle theme")

            def _refresh_theme_icon(name: str) -> None:
                if name != "theme":
                    return
                theme_btn.props(f"icon={'dark_mode' if app_vm.theme == 'light' else 'light_mode'}")

            app_vm.property_changed.subscribe(on_next=_refresh_theme_icon)

            # §5.1 Primary button: accent bg, accent-on text, 8/16 padding, 6px radius
            ui.button(
                "Solve",
                icon="play_arrow",
                on_click=lambda: _do_explicit_solve(vm),
            ).props("color=primary")

        # ── Tab strip (§5.4: 40px tall, border-subtle underline) ───────────
        with ui.tabs().classes(
            "w-full bg-[var(--bg-surface)] text-[var(--text-secondary)]"
        ) as tabs:
            tab_decisions = ui.tab("Decisions")
            tab_alts = ui.tab("Alternatives")
            tab_props = ui.tab("Properties")
            tab_coefs = ui.tab("Coefficients")
            tab_constraints = ui.tab("Constraints")
            tab_results = ui.tab("Results")
            tab_crit_dec = ui.tab("Critical Decisions")
            tab_crit_con = ui.tab("Critical Constraints")

        # ── Tab panels ──────────────────────────────────────────────────────
        # §6 Main pane: bg-page, 24px padding (applied per tab panel)
        main_content = (
            ui.tab_panels(tabs, value=tab_results if vm.scenario else tab_decisions)
            .classes("flex-1 w-full bg-[var(--bg-page)] overflow-auto")
            .props("dark")
        )

        with main_content:
            with ui.tab_panel(tab_decisions):
                dec_container: Any = ui.column().classes("w-full p-6")
                with dec_container:
                    _render_decisions_tab(vm, dec_container)

            with ui.tab_panel(tab_alts):
                alt_container: Any = ui.column().classes("w-full p-6")
                with alt_container:
                    _render_alternatives_tab(vm, alt_container)

            with ui.tab_panel(tab_props):
                prop_container: Any = ui.column().classes("w-full p-6")
                with prop_container:
                    _render_properties_tab(vm, prop_container)

            with ui.tab_panel(tab_coefs):
                coef_container: Any = ui.column().classes("w-full p-6")
                with coef_container:
                    _render_coefficients_tab(vm, coef_container)

            with ui.tab_panel(tab_constraints):
                constr_container: Any = ui.column().classes("w-full p-6")
                with constr_container:
                    _render_constraints_tab(vm, constr_container)

            with ui.tab_panel(tab_results):
                res_container: Any = ui.column().classes("w-full p-6")
                with res_container:
                    _render_results_tab(vm, res_container)

            with ui.tab_panel(tab_crit_dec):
                crit_dec_container: Any = ui.column().classes("w-full p-6")
                with crit_dec_container:
                    _render_critical_decisions_tab(vm, crit_dec_container)

            with ui.tab_panel(tab_crit_con):
                crit_con_container: Any = ui.column().classes("w-full p-6")
                with crit_con_container:
                    _render_critical_constraints_tab(vm, crit_con_container)

        # ── Status bar (§6: 32px tall, 24px h-padding, 12px text-secondary) ─
        status_label = ui.label(_status_text(vm)).classes(
            "guidearch-statusbar w-full flex items-center "
            "bg-[var(--bg-page)] text-[var(--text-secondary)] "
            "border-t border-[var(--border-subtle)] font-mono"
        )

    # ── Reactive updates ─────────────────────────────────────────────────────

    def _on_vm_change(prop: str) -> None:
        status_label.set_text(_status_text(vm))
        if prop in ("candidates", "selected_candidate_index"):
            res_container.clear()
            with res_container:
                _render_results_tab(vm, res_container)
        if prop in ("candidates", "critical_decisions"):
            crit_dec_container.clear()
            with crit_dec_container:
                _render_critical_decisions_tab(vm, crit_dec_container)
        if prop in ("candidates", "critical_constraints"):
            crit_con_container.clear()
            with crit_con_container:
                _render_critical_constraints_tab(vm, crit_con_container)
        if prop in ("scenario",):
            # Full re-render of all tabs (e.g. after New/Open)
            for cont, fn in [
                (dec_container, _render_decisions_tab),
                (alt_container, _render_alternatives_tab),
                (prop_container, _render_properties_tab),
                (coef_container, _render_coefficients_tab),
                (constr_container, _render_constraints_tab),
                (res_container, _render_results_tab),
                (crit_dec_container, _render_critical_decisions_tab),
                (crit_con_container, _render_critical_constraints_tab),
            ]:
                cont.clear()
                with cont:
                    fn(vm, cont)
        # Save-button enabled state must refresh on the same properties the
        # Save button's initial-state gate reads. Native mode requires both
        # scenario AND file_path (vm.save_cmd predicate); web mode requires
        # only scenario (anchor-download, no file path needed). Without this
        # split, a New in web mode would disable the Save button even
        # though _do_save_browser would work fine.
        if prop in ("scenario", "file_path", "is_dirty"):
            scenario_ok = vm.scenario is not None
            file_path_ok = vm.file_path is not None
            can_save = scenario_ok and (file_path_ok if _is_native else True)
            if can_save:
                save_btn.props(remove="disabled")
            else:
                save_btn.props(add="disabled")

    vm.property_changed.subscribe(on_next=_on_vm_change)

    # v1.0: no debounce timer — re-solve happens synchronously inside
    # ScenarioVM._apply_scenario_mutation when an editor calls a mutator.
    # See the v1.0 status note at the top of spec/editors.md for why; debounce is deferred to v1.1.


# ---------------------------------------------------------------------------
# Action helpers wired to toolbar
# ---------------------------------------------------------------------------


def _do_new(vm: ScenarioVM) -> None:
    vm.new_cmd.execute()


def _do_open_native(vm: ScenarioVM) -> None:
    _open_file_native(vm)


def _do_open_by_path(vm: ScenarioVM, path: str, dialog: Any) -> None:
    path = path.strip()
    if path:
        was_dirty = vm.is_dirty
        vm.open_cmd.execute(path)
        if was_dirty and not vm.is_dirty:
            _stamp_discard_warning("Open")
    dialog.close()


def _do_open_upload(vm: ScenarioVM, event: Any, dialog: Any) -> None:
    """Handle uploaded file content (web mode).

    We need a real filesystem path so open_cmd can use load_scenario, but
    the file is only ever read once during open_cmd. Clean up the temp
    file immediately afterwards so a long-running web session doesn't
    accumulate /tmp/tmp*.json leaks across repeated Opens.
    """
    import contextlib
    import os
    import tempfile

    tmp_path: str | None = None
    was_dirty = vm.is_dirty
    try:
        content = event.content.read()
        with tempfile.NamedTemporaryFile(suffix=".json", delete=False, mode="wb") as tmp:
            tmp.write(content)
            tmp_path = tmp.name
        vm.open_cmd.execute(tmp_path)
        if was_dirty and not vm.is_dirty:
            _stamp_discard_warning("Open")
    except Exception as exc:
        ui.notify(f"Upload failed: {exc}", color="negative")
    finally:
        if tmp_path is not None:
            with contextlib.suppress(OSError):
                os.unlink(tmp_path)
        dialog.close()


def _do_save_browser(vm: ScenarioVM, file_path: str | None = None) -> None:
    """Web-mode Save / Save As: anchor-download, then mark saved.

    Mirrors the TS handleSave + vm._browserMarkSaved pair. No vm.save_cmd
    here — that would write to the NiceGUI host's disk. Pass file_path to
    also update vm.file_path (Save As); leave None for plain Save.
    """
    if vm.scenario is None:
        return
    _download_scenario(vm)
    vm.browser_mark_saved(file_path)
    ui.notify("Downloaded.", color="positive")


def _stamp_discard_warning(action: str) -> None:
    """Surface the 'discarded unsaved changes' warning after a successful
    scenario-replacing action. Mirrors the TS Toolbar's confirm dialog and
    the C# StampDiscardWarning helper. Callers gate this on the pre-action
    is_dirty snapshot so a cancelled file picker or failed loader doesn't
    leave a phantom warning. A real modal-confirm dialog matching the TS
    UX is on the v1.1 backlog for all three impls.
    """
    ui.notify(
        f"{action} replaced unsaved changes — last revision discarded.",
        color="warning",
    )


def _do_save(vm: ScenarioVM) -> None:
    if not vm.file_path:
        ui.notify("No file path — use Save As.", color="warning")
        return
    # Snapshot is_dirty before execute. A successful save clears is_dirty;
    # the VM's catch-and-warn path (commit b85dec5) leaves is_dirty true
    # AND sets status to 'Save failed: …'. Comparing the dirty bit avoids
    # the prior status-string trick which misfired on repeat-failures
    # where the failure message text was identical.
    was_dirty = vm.is_dirty
    vm.save_cmd.execute()
    if vm.is_dirty and was_dirty and vm.status.startswith("Save failed:"):
        ui.notify(vm.status, color="negative")
    else:
        ui.notify("Saved.", color="positive")


def _do_explicit_solve(vm: ScenarioVM) -> None:
    try:
        vm.solve_cmd.execute()
    except Exception as exc:
        ui.notify(f"Solve error: {exc}", color="negative")


# ---------------------------------------------------------------------------
# Entrypoint
# ---------------------------------------------------------------------------


def main() -> None:
    global _is_native

    parser = argparse.ArgumentParser(prog="guidearch")
    parser.add_argument(
        "--native",
        action="store_true",
        help="Open in a native pywebview window instead of a browser",
    )
    parser.add_argument("--port", type=int, default=8080)
    args = parser.parse_args()
    _is_native = args.native

    ui.page("/")(index)
    ui.run(
        title="GuideArch",
        native=args.native,
        port=args.port,
        reload=False,
        show=not args.native,
        dark=True,
    )


if __name__ in {"__main__", "__mp_main__"}:
    main()
