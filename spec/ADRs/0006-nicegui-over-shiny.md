# ADR-0006: NiceGUI 3.x as the Python view layer (not Shiny, not Streamlit)

**Status:** Accepted
**Date:** 2026-05-29

## Context

We need a Python web/desktop UI framework that pairs well with VMx-Python and yields a professional-looking application. Candidates considered: Streamlit, Shiny for Python, NiceGUI, Flet, PySide6, Textual.

- **Streamlit** reruns the entire script on every interaction; state lives in `st.session_state`. Fundamentally incompatible with MVVM observable semantics.
- **Shiny for Python** has a reactive graph and persistent state but accesses inputs anonymously (`input.x()`) rather than as bindable properties on objects.
- **NiceGUI 3.x** ships `bindable_property`, `@bindable_dataclass`, and a new `Event` class explicitly designed to "synchronize long-living objects with short-living UI." This is the MVVM bridge VMx wants.
- NiceGUI's `ui.run(native=True)` opens a pywebview window using the OS webview (not bundled Chromium), enabling the same Python code to ship as desktop and web — symmetric with the TS+Tauri and C#+Avalonia stories.
- Quasar (Vue 3 / Material Design 3) + Tailwind 4 produce a polished baseline.

## Decision

Adopt NiceGUI 3.x as the Python view layer. Build a small VMx ↔ NiceGUI binding adapter at `langs/python/src/guidearch/view/adapters/`.

## Consequences

- The Python impl reaches feature parity with TS+Tauri and C#+Avalonia on deployment surface (desktop + web).
- We write a ~30–50 LOC adapter mapping VMx `PropertyChangedMessage` to NiceGUI binding propagation; contributable back to VMx as `vmx.bindings.nicegui`.
- We accept NiceGUI's documentation being less comprehensive than Shiny's — the GitHub Discussions forum is the supplement.
- Standalone executable packaging (PyInstaller `--onefile`) has known multiprocessing edge cases; deferred past v1.0.
