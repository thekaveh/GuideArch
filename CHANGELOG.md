# Changelog

All notable changes to GuideArch (the monorepo — TypeScript / C# / Python impls released together) are recorded here. Versioning follows the single-monorepo-version policy of [ADR-0005](spec/ADRs/0005-single-monorepo-version.md): a release pushes one tag and all three impls share its version number.

The format is loosely [Keep a Changelog](https://keepachangelog.com/en/1.1.0/). Dates are ISO 8601.

## [Unreleased]

Ongoing maintenance after v1.0.0. See `git log` for granular changes.

## [1.0.0] — 2026-05-30

First production release of GuideArch — fuzzy multi-criteria decision analysis for software architecture, shipped as three concurrent implementations (TypeScript + Tauri 2 + Svelte 5, C# + Avalonia 12, Python + NiceGUI 3.x) sharing one language-neutral spec.

### Added
- TOPSIS engine with triangular fuzzy numbers, Z-space PIS/NIS normalization, lower-vertex defuzzification, lexicographic tie-break.
- Editors for decisions, alternatives, properties, coefficients, and the three constraint kinds (threshold, dependency, conflict).
- Critical decisions and critical constraints analyses (`exp(-0.1·rank)` weighting; constraint-elimination counting).
- Results pane: ranked candidates table, fuzzy-decomposition triangle chart, ranked-bar chart.
- Cross-impl conformance corpus (SAS, EDS) with `1e-9` absolute tolerance per scalar and exact ranking under the spec tie-break.
- MIT license; release artifacts for all three impls per `spec/release.md`.

### Decisions captured as ADRs
- [ADR-0001](spec/ADRs/0001-three-impls-vmx-submodule.md): three implementations, VMx as submodule.
- [ADR-0002](spec/ADRs/0002-json-schema-not-xml.md): JSON Schema 2020-12 for scenarios (the legacy XML form is converted via `tools/import-legacy-xml.py`).
- [ADR-0003](spec/ADRs/0003-topsis-no-msf.md): TOPSIS in-repo; no Microsoft Solver Foundation dependency.
- [ADR-0004](spec/ADRs/0004-mit-license.md): MIT license.
- [ADR-0005](spec/ADRs/0005-single-monorepo-version.md): single shared monorepo version.
- [ADR-0006](spec/ADRs/0006-nicegui-over-shiny.md): NiceGUI 3.x for the Python view.

### Known v1.0 status (deferred, not bugs)
- Avalonia WebAssembly target is deferred to v1.1 (`spec/release.md` §1.2). v1.0 ships desktop-only for C#.
- Re-solve is synchronous in all three impls (see the v1.0 status note at the top of `spec/editors.md`). Adapter-level debounce is deferred to v1.1.
- Cross-impl byte-equality of saved JSON files is not a guarantee — Python sorts keys, TypeScript and C# preserve insertion order (`spec/viewmodels.md` §3.2). Load-then-resolve equality is enforced by the conformance corpus.
- Composite ViewModel wrappers (`DecisionsVM`/`PropertiesVM`/…) sketched in `spec/viewmodels.md` §2 are not implemented; the UIs read directly from `ScenarioVM.model.scenario.*`.
- The C# VM uses `NullMessageHub` (no live hub broadcasts) where Python and TypeScript use a live `MessageHub`. Equalising is on the v1.1 backlog.
