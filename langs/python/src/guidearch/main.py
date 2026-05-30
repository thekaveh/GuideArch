"""GuideArch Python entrypoint — M4 analysis UI (NiceGUI).

App shell
---------
- Top toolbar: New / Open / Save / Save As / [spacer] / Solve
- Tab strip (top): Decisions / Alternatives / Properties / Coefficients /
  Constraints / Results / Critical decisions / Critical constraints
- Status bar (bottom): scenario name · candidate count · warnings

File dialogs
------------
Web mode: ui.upload for Open; ui.download for Save.
Native mode: tkinter.filedialog (synchronous) with graceful ImportError fallback.

Re-solve debounce
-----------------
The VM's _solve_needed flag is polled via a 100 ms ui.timer.  Any mutation
in a tab editor that changes solve-relevant data calls _schedule_solve() which
sets a pending flag; the timer fires solve_cmd.execute() once.

M4 additions
------------
- Results tab is a split layout: left (60%) ranked-candidates table,
  right (40%) two stacked ui.echart instances (Chart A: bar, Chart B: triangle).
- Critical decisions tab: read-only table of criticalDecisionsResult.
- Critical constraints tab: read-only table of criticalConstraintsResult with
  faded background for redundant rows.
"""

from __future__ import annotations

import argparse
import io
import json
import traceback
from pathlib import Path
from typing import Any

from nicegui import ui

from guidearch.viewmodels.scenario_vm import ScenarioVM, make_scenario_vm

# ---------------------------------------------------------------------------
# Globals
# ---------------------------------------------------------------------------

_vm: ScenarioVM | None = None
_is_native: bool = False
_solve_pending: bool = False


def _get_vm() -> ScenarioVM:
    global _vm
    if _vm is None:
        _vm = make_scenario_vm()
    return _vm


def _schedule_solve() -> None:
    global _solve_pending
    _solve_pending = True


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
    """Save As using tkinter dialog (native mode)."""
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
        if path:
            vm.save_as_cmd.execute(path)
    except ImportError:
        ui.notify("tkinter unavailable; use Save (web mode).", color="warning")


def _download_scenario(vm: ScenarioVM) -> None:
    """Web-mode Save: download the current scenario as JSON bytes."""
    if vm.scenario is None:
        return
    from guidearch.viewmodels.scenario_vm import _scenario_to_dict

    data = _scenario_to_dict(vm.scenario)
    buf = io.BytesIO(json.dumps(data, indent=2, sort_keys=True, ensure_ascii=False).encode())
    filename = (
        Path(vm.file_path).name if vm.file_path else "scenario.json"
    )
    ui.download(buf.read(), filename)


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
        ui.label("No scenario loaded.").classes("text-gray-400")
        return

    def _refresh() -> None:
        container.clear()
        with container:
            _render_decisions_tab(vm, container)

    with ui.row().classes("items-center gap-2 mb-3"):
        ui.label("Decisions").classes("text-lg font-semibold text-gray-200")
        ui.space()
        ui.button("+ Add Decision", on_click=lambda: _do_add_decision(vm, _refresh)).props(
            "flat color=positive"
        )

    if not scenario.decisions:
        ui.label("No decisions yet.").classes("text-gray-500 py-4")
        return

    _dec_card_cls = "w-full mb-2 bg-gray-800 border border-gray-700"
    _dec_row_cls = "items-center gap-3 w-full"
    for dec in scenario.decisions:
        with ui.card().classes(_dec_card_cls), ui.row().classes(_dec_row_cls):
                name_input = (
                    ui.input(value=dec.name)
                    .props("dense outlined dark")
                    .classes("flex-1 font-medium")
                )
                name_input.on(
                    "blur",
                    lambda e, d=dec, ni=name_input: _do_update_decision_name(
                        vm, d.id, ni.value, _refresh
                    ),
                )
                ui.label(f"id: {dec.id[:8]}…").classes(
                    "text-xs text-gray-500 font-mono"
                )
                ui.button(
                    icon="delete",
                    on_click=lambda d=dec: _do_delete_decision(vm, d.id, _refresh),
                ).props("flat color=negative dense")


def _do_add_decision(vm: ScenarioVM, refresh: Any) -> None:
    try:
        vm.add_decision()
        _schedule_solve()
        refresh()
    except Exception as exc:
        ui.notify(str(exc), color="negative")


def _do_update_decision_name(
    vm: ScenarioVM, decision_id: str, name: str, refresh: Any
) -> None:
    try:
        vm.update_decision_name(decision_id, name.strip() or "Decision")
        refresh()
    except Exception as exc:
        ui.notify(str(exc), color="negative")


def _do_delete_decision(vm: ScenarioVM, decision_id: str, refresh: Any) -> None:
    async def _confirmed() -> None:
        try:
            vm.delete_decision(decision_id)
            _schedule_solve()
            refresh()
        except Exception as exc:
            ui.notify(str(exc), color="negative")

    with ui.dialog() as dlg, ui.card().classes("bg-gray-900"):
        ui.label("Delete this decision and all its alternatives?").classes(
            "text-white text-base mb-4"
        )
        with ui.row():
            ui.button(
                "Delete",
                on_click=lambda: (dlg.close(), _confirmed()),
            ).props("color=negative")
            ui.button("Cancel", on_click=dlg.close).props("flat")
    dlg.open()


# ---------------------------------------------------------------------------
# Tab: Alternatives
# ---------------------------------------------------------------------------


def _render_alternatives_tab(vm: ScenarioVM, container: Any) -> None:
    scenario = vm.scenario
    if scenario is None:
        ui.label("No scenario loaded.").classes("text-gray-400")
        return

    def _refresh() -> None:
        container.clear()
        with container:
            _render_alternatives_tab(vm, container)

    ui.label("Alternatives").classes("text-lg font-semibold text-gray-200 mb-3")

    if not scenario.decisions:
        ui.label("Add decisions first.").classes("text-gray-500")
        return

    for dec in scenario.decisions:
        dec_alts = [a for a in scenario.alternatives if a.decision_id == dec.id]
        with ui.expansion(
            f"{dec.name}  ({len(dec_alts)} alternatives)",
            icon="folder",
        ).classes("w-full mb-2 bg-gray-800 border border-gray-700 rounded"):
            with ui.row().classes("items-center gap-2 mb-2"):
                ui.space()
                ui.button(
                    "+ Add Alternative",
                    on_click=lambda d=dec: _do_add_alternative(vm, d.id, _refresh),
                ).props("flat color=positive dense")

            if not dec_alts:
                ui.label("No alternatives.").classes("text-gray-500 text-sm py-2 pl-4")
                continue

            for alt in dec_alts:
                with ui.row().classes("items-center gap-3 w-full pl-4 py-1"):
                    name_input = (
                        ui.input(value=alt.name)
                        .props("dense outlined dark")
                        .classes("flex-1")
                    )
                    name_input.on(
                        "blur",
                        lambda e, a=alt, ni=name_input: _do_update_alternative_name(
                            vm, a.id, ni.value, _refresh
                        ),
                    )
                    ui.label(f"id: {alt.id[:8]}…").classes("text-xs text-gray-500 font-mono")
                    ui.button(
                        icon="delete",
                        on_click=lambda a=alt: _do_delete_alternative(vm, a.id, _refresh),
                    ).props("flat color=negative dense")


def _do_add_alternative(vm: ScenarioVM, decision_id: str, refresh: Any) -> None:
    try:
        vm.add_alternative(decision_id)
        _schedule_solve()
        refresh()
    except Exception as exc:
        ui.notify(str(exc), color="negative")


def _do_update_alternative_name(
    vm: ScenarioVM, alt_id: str, name: str, refresh: Any
) -> None:
    try:
        vm.update_alternative_name(alt_id, name.strip() or "Alternative")
        refresh()
    except Exception as exc:
        ui.notify(str(exc), color="negative")


def _do_delete_alternative(vm: ScenarioVM, alt_id: str, refresh: Any) -> None:
    async def _confirmed() -> None:
        try:
            vm.delete_alternative(alt_id)
            _schedule_solve()
            refresh()
        except Exception as exc:
            ui.notify(str(exc), color="negative")

    with ui.dialog() as dlg, ui.card().classes("bg-gray-900"):
        ui.label("Delete this alternative and its coefficients?").classes(
            "text-white text-base mb-4"
        )
        with ui.row():
            ui.button("Delete", on_click=lambda: (dlg.close(), _confirmed())).props(
                "color=negative"
            )
            ui.button("Cancel", on_click=dlg.close).props("flat")
    dlg.open()


# ---------------------------------------------------------------------------
# Tab: Properties
# ---------------------------------------------------------------------------


def _render_properties_tab(vm: ScenarioVM, container: Any) -> None:
    scenario = vm.scenario
    if scenario is None:
        ui.label("No scenario loaded.").classes("text-gray-400")
        return

    def _refresh() -> None:
        container.clear()
        with container:
            _render_properties_tab(vm, container)

    with ui.row().classes("items-center gap-2 mb-3"):
        ui.label("Properties").classes("text-lg font-semibold text-gray-200")
        ui.space()
        ui.button("+ Add Property", on_click=lambda: _do_add_property(vm, _refresh)).props(
            "flat color=positive"
        )

    if not scenario.properties:
        ui.label("No properties yet.").classes("text-gray-500 py-4")
        return

    _prop_card_cls = "w-full mb-2 bg-gray-800 border border-gray-700"
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

                ui.label(f"id: {prop.id[:8]}…").classes("text-xs text-gray-500 font-mono")
                ui.button(
                    icon="delete",
                    on_click=lambda p=prop: _do_delete_property(vm, p.id, _refresh),
                ).props("flat color=negative dense")


def _do_add_property(vm: ScenarioVM, refresh: Any) -> None:
    try:
        vm.add_property()
        _schedule_solve()
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
        if weight is not None or kind is not None:
            _schedule_solve()
        if refresh:
            refresh()
    except Exception as exc:
        ui.notify(str(exc), color="negative")


def _do_delete_property(vm: ScenarioVM, prop_id: str, refresh: Any) -> None:
    async def _confirmed() -> None:
        try:
            vm.delete_property(prop_id)
            _schedule_solve()
            refresh()
        except Exception as exc:
            ui.notify(str(exc), color="negative")

    with ui.dialog() as dlg, ui.card().classes("bg-gray-900"):
        ui.label("Delete this property and its coefficients?").classes(
            "text-white text-base mb-4"
        )
        with ui.row():
            ui.button("Delete", on_click=lambda: (dlg.close(), _confirmed())).props(
                "color=negative"
            )
            ui.button("Cancel", on_click=dlg.close).props("flat")
    dlg.open()


# ---------------------------------------------------------------------------
# Tab: Coefficients
# ---------------------------------------------------------------------------


def _render_coefficients_tab(vm: ScenarioVM, container: Any) -> None:
    scenario = vm.scenario
    if scenario is None:
        ui.label("No scenario loaded.").classes("text-gray-400")
        return

    if not scenario.properties or not scenario.alternatives:
        ui.label("Add decisions, alternatives and properties first.").classes(
            "text-gray-500"
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
        "text-lg font-semibold text-gray-200 mb-2"
    )
    ui.label("Format: lower · modal · upper").classes("text-xs text-gray-500 mb-3")

    with ui.scroll_area().classes("w-full"):
        # Header row
        with ui.row().classes("gap-1 mb-1 sticky-top bg-gray-900 pb-1"):
            ui.label("Alternative").classes(
                "font-semibold text-gray-300 w-44 text-sm"
            )
            for prop in props:
                kind_badge = "↓" if prop.kind == "min" else "↑"
                with ui.column().classes("items-center w-36"):
                    ui.label(prop.name).classes(
                        "font-semibold text-gray-300 text-xs text-center"
                    )
                    ui.label(f"{kind_badge} w={prop.weight:.3g}").classes(
                        "text-xs text-gray-500"
                    )

        # Group by decision
        for dec in decisions:
            dec_alts = [a for a in alts if a.decision_id == dec.id]
            if not dec_alts:
                continue
            # Group header
            with ui.row().classes("w-full bg-gray-700 rounded px-2 py-1 mb-1"):
                ui.label(dec.name).classes("font-bold text-gray-200 text-sm")

            for alt in dec_alts:
                with ui.row().classes("gap-1 mb-1 items-center"):
                    ui.label(alt.name).classes(
                        "text-gray-300 text-sm w-44 truncate"
                    )
                    for prop in props:
                        coef = coef_map.get((alt.id, prop.id))
                        if coef is None:
                            ui.label("—").classes("w-36 text-center text-gray-600")
                            continue

                        lower_v = coef.value.lower
                        modal_v = coef.value.modal
                        upper_v = coef.value.upper
                        warn_color = (
                            "border-yellow-500"
                            if lower_v > modal_v or modal_v > upper_v
                            else "border-gray-600"
                        )

                        with ui.column().classes(
                            f"w-36 border rounded p-1 {warn_color}"
                        ):
                            with ui.row().classes("gap-1 items-center"):
                                l_in = (
                                    ui.number(
                                        value=lower_v,
                                        format="%.3g",
                                        step=0.1,
                                    )
                                    .props("dense dark borderless")
                                    .classes("w-10 font-mono text-xs")
                                )
                                ui.label("·").classes("text-gray-500 text-xs")
                                m_in = (
                                    ui.number(
                                        value=modal_v,
                                        format="%.3g",
                                        step=0.1,
                                    )
                                    .props("dense dark borderless")
                                    .classes("w-10 font-mono text-xs")
                                )
                                ui.label("·").classes("text-gray-500 text-xs")
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
                                        _schedule_solve()
                                        rf()
                                    except Exception as exc:
                                        ui.notify(str(exc), color="negative")

                                return _handler

                            handler = _make_blur_handler(
                                alt.id, prop.id, l_in, m_in, u_in, _refresh
                            )
                            l_in.on("blur", handler)
                            m_in.on("blur", handler)
                            u_in.on("blur", handler)


# ---------------------------------------------------------------------------
# Tab: Constraints
# ---------------------------------------------------------------------------


def _render_constraints_tab(vm: ScenarioVM, container: Any) -> None:
    scenario = vm.scenario
    if scenario is None:
        ui.label("No scenario loaded.").classes("text-gray-400")
        return

    def _refresh() -> None:
        container.clear()
        with container:
            _render_constraints_tab(vm, container)

    ui.label("Constraints").classes("text-lg font-semibold text-gray-200 mb-2")

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
            ).props("flat color=positive")

    threshold_cs = [
        (i, c)
        for i, c in enumerate(scenario.constraints)
        if isinstance(c, ThresholdConstraint)
    ]

    if not threshold_cs:
        ui.label("No threshold constraints.").classes("text-gray-500 py-4")
        return

    for idx, c in threshold_cs:
        invalid = c.min is not None and c.max is not None and c.min > c.max
        card_border = "border-red-600" if invalid else "border-gray-700"
        with ui.card().classes(f"w-full mb-2 bg-gray-800 border {card_border}"):  # noqa: SIM117
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
        vm.add_threshold_constraint(prop_id)
        _schedule_solve()
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
        _schedule_solve()
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
            ).props("flat color=positive")

    dep_cs = [
        (i, c)
        for i, c in enumerate(scenario.constraints)
        if isinstance(c, DependencyConstraint)
    ]

    if not dep_cs:
        ui.label("No dependency constraints.").classes("text-gray-500 py-4")
        return

    for idx, c in dep_cs:
        self_edge = c.source_alternative_id == c.target_alternative_id
        card_border = "border-yellow-600" if self_edge else "border-gray-700"
        with ui.card().classes(f"w-full mb-2 bg-gray-800 border {card_border}"):  # noqa: SIM117
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

                ui.label("→").classes("text-gray-400 text-lg")

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


def _do_add_dependency(
    vm: ScenarioVM, src: str, tgt: str, refresh: Any
) -> None:
    try:
        vm.add_dependency_constraint(src, tgt)
        _schedule_solve()
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
        _schedule_solve()
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
            ).props("flat color=positive")

    conf_cs = [
        (i, c)
        for i, c in enumerate(scenario.constraints)
        if isinstance(c, ConflictConstraint)
    ]

    if not conf_cs:
        ui.label("No conflict constraints.").classes("text-gray-500 py-4")
        return

    for idx, c in conf_cs:
        self_edge = c.alternative_a_id == c.alternative_b_id
        card_border = "border-yellow-600" if self_edge else "border-gray-700"
        with ui.card().classes(f"w-full mb-2 bg-gray-800 border {card_border}"):  # noqa: SIM117
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

                ui.label("✗").classes("text-red-400 text-lg")

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
        _schedule_solve()
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
        _schedule_solve()
        if refresh:
            refresh()
    except Exception as exc:
        ui.notify(str(exc), color="negative")


def _do_delete_constraint(vm: ScenarioVM, index: int, refresh: Any) -> None:
    try:
        vm.delete_constraint(index)
        _schedule_solve()
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
        ui.label("No scenario loaded.").classes("text-gray-400")
        return

    candidates = vm.candidates
    if not candidates:
        ui.label("No candidates — check scenario and constraints.").classes(
            "text-gray-500 py-8"
        )
        return

    alt_map, alt_dec, _ = _build_alt_maps(vm)
    prop_names = [p.name for p in scenario.properties] if scenario else []

    top30 = candidates[:30]
    top50 = candidates[:50]

    # ── Candidates table columns/rows (left pane) ────────────────────────────
    columns: list[dict[str, Any]] = [
        {"name": "rank", "label": "#", "field": "rank", "align": "right", "sortable": True},
        {"name": "score", "label": "Score", "field": "score", "align": "right", "sortable": True},
        {"name": "alternatives", "label": "Alternatives", "field": "alternatives", "align": "left"},
    ]

    rows = []
    for cand in top50:
        alt_labels = [
            f"{alt_dec.get(aid, '?')}/{alt_map.get(aid, aid[:8])}"
            for aid in cand.alternative_ids
        ]
        display = ", ".join(alt_labels[:4])
        if len(alt_labels) > 4:
            display += f", … +{len(alt_labels) - 4}"
        rows.append(
            {
                "rank": cand.rank,
                "score": f"{cand.score:.6g}",
                "alternatives": display,
            }
        )

    # ── Split layout ─────────────────────────────────────────────────────────
    with ui.row().classes("w-full gap-0 items-start"):
        # Left: table (~60%)
        with ui.column().classes("w-3/5 pr-4"):
            ui.label(
                f"Top {len(top50)} of {len(candidates)} ranked candidates"
            ).classes("text-lg font-semibold text-gray-200 mb-2")

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
                    row_class = "bg-yellow-900"
                sel_rows.append(
                    {
                        "rank": cand.rank,
                        "score": f"{cand.score:.6g}",
                        "alternatives": display,
                        "_class": row_class,
                    }
                )

            tbl = ui.table(columns=columns, rows=sel_rows, row_key="rank").classes(
                "w-full max-h-screen overflow-y-auto"
            ).props("dark dense flat")

            # Row click selects candidate
            tbl.on(
                "row-click",
                lambda e, v=vm: _on_candidate_row_click(v, e),
            )

        # Right: charts (~40%)
        with ui.column().classes("w-2/5 gap-2"):
            from guidearch.view.chart_data import candidates_bar_option, triangle_option

            # Chart A — bar chart
            chart_a_opt = candidates_bar_option(top30, alt_map, vm.selected_candidate_index)
            chart_a = ui.echart(chart_a_opt).classes("w-full").style("height:220px")

            # Chart A click handler
            chart_a.on(
                "click",
                lambda e, v=vm, cands=top30: _on_chart_a_click(v, cands, e),
            )

            # Chart B — triangle
            sel = vm.selected_candidate_index
            if sel is not None and sel < len(candidates):
                selected_cand = candidates[sel]
                chart_b_opt = triangle_option(selected_cand, prop_names, alt_map)
            else:
                chart_b_opt = triangle_option(candidates[0], prop_names, alt_map)
            ui.echart(chart_b_opt).classes("w-full").style("height:220px")


def _on_candidate_row_click(vm: ScenarioVM, event: Any) -> None:
    """Handle row-click in the candidates table: set selected_candidate_index."""
    try:
        rank = int(event.args["row"]["rank"])
        vm.selected_candidate_index = rank
    except Exception:
        pass


def _on_chart_a_click(
    vm: ScenarioVM, top_candidates: tuple[Any, ...], event: Any
) -> None:
    """Handle ECharts click on Chart A bar: set selected_candidate_index."""
    try:
        # ECharts click event: dataIndex is the bar index within the series
        data_index = int(event.args.get("dataIndex", 0))
        if data_index < len(top_candidates):
            vm.selected_candidate_index = top_candidates[data_index].rank
    except Exception:
        pass


# ---------------------------------------------------------------------------
# Tab: Critical decisions
# ---------------------------------------------------------------------------


def _render_critical_decisions_tab(vm: ScenarioVM, container: Any) -> None:
    scenario = vm.scenario
    if scenario is None:
        ui.label("No scenario loaded.").classes("text-gray-400")
        return

    crit = vm.critical_decisions_result
    if not crit:
        ui.label("No critical decisions — run solve first.").classes(
            "text-gray-500 py-8"
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

    ui.label("Critical Decisions").classes("text-lg font-semibold text-gray-200 mb-2")
    ui.label(
        "Sorted ascending by rank (lower score = more critical)"
    ).classes("text-xs text-gray-500 mb-3")
    ui.table(columns=columns, rows=rows, row_key="rank").classes(
        "w-full max-h-screen overflow-y-auto"
    ).props("dark dense flat")


# ---------------------------------------------------------------------------
# Tab: Critical constraints
# ---------------------------------------------------------------------------


def _render_critical_constraints_tab(vm: ScenarioVM, container: Any) -> None:
    scenario = vm.scenario
    if scenario is None:
        ui.label("No scenario loaded.").classes("text-gray-400")
        return

    crit = vm.critical_constraints_result
    if not crit:
        ui.label("No critical constraints — run solve first.").classes(
            "text-gray-500 py-8"
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

    ui.label("Critical Constraints").classes("text-lg font-semibold text-gray-200 mb-2")
    ui.label(
        "Sorted descending by eliminated (most-binding first). "
        "Redundant rows shown with faded background."
    ).classes("text-xs text-gray-500 mb-3")

    # Render as a custom table to support per-row background styling
    with ui.scroll_area().classes("w-full max-h-screen"):
        # Header
        with ui.row().classes(
            "w-full bg-gray-700 rounded px-2 py-1 mb-1 font-semibold text-gray-300 text-xs gap-0"
        ):
            ui.label("Idx").classes("w-12 text-right pr-2")
            ui.label("Kind").classes("w-28 pr-2")
            ui.label("Eliminated").classes("w-24 text-right pr-2")
            ui.label("Total").classes("w-20 text-right pr-2")
            ui.label("Elim %").classes("w-20 text-right pr-2")
            ui.label("Redundant").classes("w-20 text-center")

        for row in rows:
            redundant = row["_redundant"]
            row_cls = "opacity-40" if redundant else ""
            bg_cls = "bg-gray-800" if not redundant else "bg-gray-900"
            with ui.row().classes(
                f"w-full {bg_cls} border border-gray-700 rounded px-2 py-1 mb-1 "
                f"text-gray-300 text-xs gap-0 {row_cls}"
            ):
                ui.label(str(row["index"])).classes("w-12 text-right pr-2 font-mono")
                ui.label(str(row["kind"])).classes("w-28 pr-2")
                ui.label(str(row["eliminated"])).classes("w-24 text-right pr-2 font-mono")
                ui.label(str(row["total"])).classes("w-20 text-right pr-2 font-mono")
                ui.label(str(row["elim_pct"])).classes("w-20 text-right pr-2 font-mono")
                badge_color = "gray" if redundant else "positive"
                ui.badge(str(row["redundant"]), color=badge_color).classes("w-20 justify-center")


# ---------------------------------------------------------------------------
# Main page
# ---------------------------------------------------------------------------


def index() -> None:
    """Render the full M3 app shell."""
    vm = _get_vm()

    ui.dark_mode().enable()
    ui.add_head_html(
        "<style>"
        "body { background: #111827; } "
        ".nicegui-content { padding: 0 !important; }"
        ".sticky-top { position: sticky; top: 0; z-index: 10; }"
        "</style>"
    )

    with ui.column().classes("w-full min-h-screen gap-0"):
        # Build the Open dialog first so it's always defined before the toolbar refs it
        from nicegui.elements.dialog import Dialog as _Dialog

        open_dialog: _Dialog
        if not _is_native:
            with ui.dialog() as open_dialog, ui.card().classes("bg-gray-900 w-96"):
                ui.label("Open Scenario File").classes("text-white text-lg mb-2")
                path_input = (
                    ui.input("Paste file path…").props("dark outlined").classes("w-full")
                )
                ui.button(
                    "Open by path",
                    on_click=lambda: _do_open_by_path(vm, path_input.value, open_dialog),
                ).props("color=indigo")
                ui.separator().classes("my-2")
                ui.label("Or upload a JSON file:").classes("text-gray-400 text-sm")
                ui.upload(
                    label="Choose file",
                    on_upload=lambda e: _do_open_upload(vm, e, open_dialog),
                    auto_upload=True,
                ).props("dark accept=.json")

        # ── Toolbar ─────────────────────────────────────────────────────────
        with ui.row().classes(
            "w-full items-center bg-gray-900 border-b border-gray-700 px-4 py-2 gap-2"
        ):
            ui.label("GuideArch").classes("text-xl font-bold text-indigo-400 mr-4")

            ui.button("New", icon="add", on_click=lambda: _do_new(vm)).props(
                "flat color=white"
            )

            if _is_native:
                ui.button(
                    "Open…",
                    icon="folder_open",
                    on_click=lambda: _do_open_native(vm),
                ).props("flat color=white")
            else:
                ui.button(
                    "Open…",
                    icon="folder_open",
                    on_click=lambda: open_dialog.open(),
                ).props("flat color=white")

            save_btn = ui.button("Save", icon="save", on_click=lambda: _do_save(vm)).props(
                "flat color=white"
            )
            if vm.scenario is None or vm.file_path is None:
                save_btn.props(add="disabled")

            if _is_native:
                ui.button(
                    "Save As…",
                    icon="save_as",
                    on_click=lambda: _save_as_native(vm),
                ).props("flat color=white")
            else:
                ui.button(
                    "Save As…",
                    icon="save_as",
                    on_click=lambda: _download_scenario(vm),
                ).props("flat color=white")

            ui.space()

            ui.button(
                "Solve",
                icon="play_arrow",
                on_click=lambda: _do_explicit_solve(vm),
            ).props("color=indigo")

        # ── Tab strip ───────────────────────────────────────────────────────
        with ui.tabs().classes(
            "w-full bg-gray-800 border-b border-gray-700 text-white"
        ) as tabs:
            tab_decisions = ui.tab("Decisions")
            tab_alts = ui.tab("Alternatives")
            tab_props = ui.tab("Properties")
            tab_coefs = ui.tab("Coefficients")
            tab_constraints = ui.tab("Constraints")
            tab_results = ui.tab("Results")
            tab_crit_dec = ui.tab("Critical decisions")
            tab_crit_con = ui.tab("Critical constraints")

        # ── Tab panels ──────────────────────────────────────────────────────
        main_content = (
            ui.tab_panels(tabs, value=tab_results if vm.scenario else tab_decisions)
            .classes("flex-1 w-full bg-gray-900 overflow-auto")
            .props("dark")
        )

        with main_content:
            with ui.tab_panel(tab_decisions):
                dec_container: Any = ui.column().classes("w-full p-4")
                with dec_container:
                    _render_decisions_tab(vm, dec_container)

            with ui.tab_panel(tab_alts):
                alt_container: Any = ui.column().classes("w-full p-4")
                with alt_container:
                    _render_alternatives_tab(vm, alt_container)

            with ui.tab_panel(tab_props):
                prop_container: Any = ui.column().classes("w-full p-4")
                with prop_container:
                    _render_properties_tab(vm, prop_container)

            with ui.tab_panel(tab_coefs):
                coef_container: Any = ui.column().classes("w-full p-4")
                with coef_container:
                    _render_coefficients_tab(vm, coef_container)

            with ui.tab_panel(tab_constraints):
                constr_container: Any = ui.column().classes("w-full p-4")
                with constr_container:
                    _render_constraints_tab(vm, constr_container)

            with ui.tab_panel(tab_results):
                res_container: Any = ui.column().classes("w-full p-4")
                with res_container:
                    _render_results_tab(vm, res_container)

            with ui.tab_panel(tab_crit_dec):
                crit_dec_container: Any = ui.column().classes("w-full p-4")
                with crit_dec_container:
                    _render_critical_decisions_tab(vm, crit_dec_container)

            with ui.tab_panel(tab_crit_con):
                crit_con_container: Any = ui.column().classes("w-full p-4")
                with crit_con_container:
                    _render_critical_constraints_tab(vm, crit_con_container)

        # ── Status bar ──────────────────────────────────────────────────────
        status_label = ui.label(_status_text(vm)).classes(
            "w-full px-4 py-1 bg-gray-950 text-gray-400 text-xs border-t border-gray-800 font-mono"
        )

    # ── Reactive updates ─────────────────────────────────────────────────────

    def _on_vm_change(prop: str) -> None:
        status_label.set_text(_status_text(vm))
        if prop in ("candidates", "selected_candidate_index"):
            res_container.clear()
            with res_container:
                _render_results_tab(vm, res_container)
        if prop in ("candidates", "critical_decisions_result"):
            crit_dec_container.clear()
            with crit_dec_container:
                _render_critical_decisions_tab(vm, crit_dec_container)
        if prop in ("candidates", "critical_constraints_result"):
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
            # Update Save button enabled state
            if vm.scenario and vm.file_path:
                save_btn.props(remove="disabled")
            else:
                save_btn.props(add="disabled")

    vm.property_changed.subscribe(on_next=_on_vm_change)

    # ── 100 ms debounce timer for solve ──────────────────────────────────────
    def _tick() -> None:
        global _solve_pending
        if _solve_pending:
            _solve_pending = False
            try:
                vm.solve_cmd.execute()
            except Exception as exc:
                ui.notify(f"Solve error: {exc}", color="negative")
                traceback.print_exc()

    ui.timer(0.1, _tick)


# ---------------------------------------------------------------------------
# Action helpers wired to toolbar
# ---------------------------------------------------------------------------


def _do_new(vm: ScenarioVM) -> None:
    vm.new_cmd.execute()
    _schedule_solve()


def _do_open_native(vm: ScenarioVM) -> None:
    _open_file_native(vm)


def _do_open_by_path(vm: ScenarioVM, path: str, dialog: Any) -> None:
    path = path.strip()
    if path:
        vm.open_cmd.execute(path)
    dialog.close()


def _do_open_upload(vm: ScenarioVM, event: Any, dialog: Any) -> None:
    """Handle uploaded file content (web mode)."""
    try:
        content = event.content.read()
        # Write to a temp file so open_cmd can read it
        import tempfile

        with tempfile.NamedTemporaryFile(
            suffix=".json", delete=False, mode="wb"
        ) as tmp:
            tmp.write(content)
            tmp_path = tmp.name
        vm.open_cmd.execute(tmp_path)
    except Exception as exc:
        ui.notify(f"Upload failed: {exc}", color="negative")
    finally:
        dialog.close()


def _do_save(vm: ScenarioVM) -> None:
    if vm.file_path:
        vm.save_cmd.execute()
        ui.notify("Saved.", color="positive")
    else:
        ui.notify("No file path — use Save As.", color="warning")


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
