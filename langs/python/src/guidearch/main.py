"""GuideArch Python entrypoint — M2 skeleton UI (NiceGUI).

Layout
------
- Top bar: scenario name + "Open scenario..." button (path input for web mode).
- Status row: scenario name + candidate count.
- Main area: table of top 50 ranked candidates (rank, score, alternativeIds).
- Empty state: centred prompt when no scenario is loaded.

Native mode (--native) opens a pywebview window.  Web mode (default) serves
on --port (default 8080).
"""

from __future__ import annotations

import argparse
from typing import Any

from nicegui import ui

from guidearch.viewmodels.scenario_vm import ScenarioVM, make_scenario_vm

# Module-level VM (one per server instance; suitable for a single-user desktop app)
_vm: ScenarioVM | None = None


def _get_vm() -> ScenarioVM:
    global _vm
    if _vm is None:
        _vm = make_scenario_vm()
    return _vm


def index() -> None:
    """Render the root page."""
    vm = _get_vm()

    with ui.column().classes("w-full h-screen gap-0"):
        # Top bar
        with ui.row().classes(
            "w-full items-center bg-gray-800 text-white px-4 py-3 gap-4"
        ):
            ui.label("GuideArch").classes("text-xl font-bold")
            ui.space()

            path_input = ui.input(
                placeholder="Path to scenario JSON...",
            ).classes("w-96 bg-white rounded text-black")

            ui.button(
                "Open scenario...",
                on_click=lambda: _on_open(vm, path_input.value),
            ).classes("bg-blue-500 text-white px-4 py-2 rounded")

        # Status row
        status_label = ui.label(vm.status).classes(
            "w-full px-4 py-2 bg-gray-100 text-gray-700 text-sm border-b"
        )

        # Main content area
        content: Any = ui.column().classes("flex-1 w-full overflow-auto p-4")

        # Initial render
        _render_content(vm, content)

        # Subscribe to VM's property_changed to refresh UI reactively
        def _on_vm_change(prop: str) -> None:
            if prop in ("candidates", "status", "scenario", "warnings"):
                status_label.set_text(vm.status)
                content.clear()
                with content:
                    _render_content(vm, content)

        vm.property_changed.subscribe(on_next=_on_vm_change)


def _on_open(vm: ScenarioVM, path: str) -> None:
    """Fire open_cmd with the given path."""
    path = path.strip()
    if path:
        vm.open_cmd.execute(path)


def _render_content(vm: ScenarioVM, container: Any) -> None:
    """Render either the empty state or the candidates table into container."""
    with container:
        if vm.scenario is None:
            # Empty state
            with ui.column().classes(
                "flex-1 items-center justify-center w-full py-20"
            ):
                ui.label("Open a scenario JSON to begin.").classes(
                    "text-2xl text-gray-400"
                )
            return

        candidates = vm.candidates
        if not candidates:
            ui.label("No candidates — check scenario constraints.").classes(
                "text-gray-500 py-8"
            )
            return

        # Top 50 candidates table
        top50 = candidates[:50]

        columns = [
            {"name": "rank", "label": "Rank", "field": "rank", "align": "right"},
            {"name": "score", "label": "Score", "field": "score", "align": "right"},
            {
                "name": "alternatives",
                "label": "Alternatives",
                "field": "alternatives",
                "align": "left",
            },
        ]

        rows = []
        for cand in top50:
            alt_ids = cand.alternative_ids
            # Truncate to first 3 with "..." if more
            alt_display = (
                ", ".join(alt_ids[:3]) + ", ..."
                if len(alt_ids) > 3
                else ", ".join(alt_ids)
            )
            rows.append(
                {
                    "rank": cand.rank,
                    "score": f"{cand.score:.4g}",
                    "alternatives": alt_display,
                }
            )

        ui.label(f"Top {len(top50)} ranked candidates").classes(
            "text-lg font-semibold mb-2"
        )
        ui.table(columns=columns, rows=rows).classes("w-full")

        if vm.warnings:
            with ui.expansion("Warnings", icon="warning").classes(
                "w-full mt-4 text-yellow-700"
            ):
                for w in vm.warnings:
                    ui.label(w).classes("text-sm")


def main() -> None:
    parser = argparse.ArgumentParser(prog="guidearch")
    parser.add_argument(
        "--native",
        action="store_true",
        help="open in a native pywebview window instead of a browser",
    )
    parser.add_argument("--port", type=int, default=8080)
    args = parser.parse_args()

    ui.page("/")(index)
    ui.run(
        title="GuideArch",
        native=args.native,
        port=args.port,
        reload=False,
        show=not args.native,
    )


if __name__ in {"__main__", "__mp_main__"}:
    main()
