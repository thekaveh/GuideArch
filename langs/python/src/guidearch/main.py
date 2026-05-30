"""GuideArch Python entrypoint — NiceGUI hello-VMx UI."""

from __future__ import annotations

import argparse

from nicegui import ui

from .hello_vmx import hello_vmx


def index() -> None:
    """Render the root page."""
    with ui.column().classes("max-w-3xl mx-auto p-12 gap-3"):
        ui.label("GuideArch").classes("text-4xl font-bold")
        ui.label("Python + NiceGUI 3.x implementation — bootstrap").classes("text-gray-500")
        ui.label(hello_vmx()).classes("p-4 bg-gray-100 rounded font-mono text-sm")


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
