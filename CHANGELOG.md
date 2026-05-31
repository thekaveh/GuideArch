# Changelog

All notable changes to GuideArch (the monorepo — TypeScript / C# / Python impls released together) are recorded here. Versioning follows the single-monorepo-version policy of [ADR-0005](spec/ADRs/0005-single-monorepo-version.md): a release pushes one tag and all three impls share its version number.

The format is loosely [Keep a Changelog](https://keepachangelog.com/en/1.1.0/). Dates are ISO 8601.

## [Unreleased]

Post-v1.0.0 maintenance focused on cross-impl parity and CI hardening; no
behavior change a user-facing release note would call out.

### Fixed
- Cross-impl `New` scenario defaults, status strings, error-message
  punctuation, tab labels (TitleCase `Critical Decisions` / `Critical
  Constraints`), mutation-error texts, and the coefficient ordering
  warning format are now identical across TypeScript, C# and Python.
- All three impls' `addX` / `updateX` / `addConstraint` mutators enforce
  invariants 2.5 (referenced ID must exist) and 7.1 (self-edge is fatal)
  at the mutation boundary, not just at the loader.
- `New` scenario now uses the schema-correct `(1/3, 1/3, 1/3)` weights,
  `"1.0.0"` schemaVersion, and `aggregation: max` in all three impls
  (was three different defaults).
- Save errors no longer leave `filePath` pointing at a failed destination
  (Python + TypeScript aligned with C#'s write-then-commit-state order).
- C# release single-file binary can now find `scenario.schema.json`
  (embedded as a manifest resource).
- Python wheel install can find the schema (force-included under
  `guidearch/_data/` with `importlib.resources` fallback).
- Tauri scaffold metadata replaced with `GuideArch` everywhere (was
  shipping installers branded `tauri-app`).

### CI
- All three per-impl workflows run their unit suites (Python `pytest`,
  C# `dotnet test`, TS `vitest`) on every PR — previously CI built but
  never executed.
- Workflows trigger on `spec/**` edits; concurrency-cancel on every
  workflow; `setup-uv@v6` with caching; release.yml derives version from
  the tag; GHCR login `if:` gate corrected.
- `spec.yml` validates conformance scenarios against the schema and
  gates the TS-bundled schema copy against the canonical file.
- Python smoke test polls instead of `sleep 5 && curl` (was flaky).

### Docs
- README, SECURITY, `spec/README`, `CONTRIBUTING`, and all per-impl
  READMEs aligned with v1.0.0 reality.
- C# WebAssembly target marked deferred to v1.1 (was claimed in README
  but not built).
- Spec self-contradictions (debounce wording, threshold-bound + self-edge
  severity, tab-count, broken cross-refs) resolved.
- Added `CHANGELOG.md`.

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
