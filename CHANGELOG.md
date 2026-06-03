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
- New `.editorconfig` at repo root forces LF line endings and consistent
  indent across platforms so `dotnet format --verify-no-changes` agrees
  with Windows runners (previously every line scored as a whitespace
  violation; `.gitattributes` already forces `*.cs eol=lf` on checkout).
- `typescript.yml` builds VMx-TS `dist/` before `pnpm install` — the
  submodule's `.gitignore` excludes `dist/`, so without this step a
  fresh CI checkout left svelte-check resolving `vmx` exports as
  `unknown` and cascading into ~164 type errors.
- `release.yml`: hoisted `secrets.PYPI_API_TOKEN` to a job-level `env:`.
  Step-level `if:` cannot reference `secrets.*` (validation error
  `"Unrecognized named-value: 'secrets'"`), which was creating phantom
  failed runs on every push to `main`.
- `release.yml`: added a `concurrency` group with
  `cancel-in-progress: false` so two close tag pushes serialize through
  the Tauri / PyPI / GHCR upload phases instead of racing.

### Refactored
- C# `Program.cs` no longer starts with a UTF-8 BOM, and
  `App.axaml.cs` ends with a trailing newline — both surfaced by the
  new `.editorconfig` strict-format checks.
- TypeScript: `AppVM`, `AppState`, `AppMode`, `MakeAppVmOptions`,
  `makeAppVm`, `KNOWN_THEMES`, and `DEFAULT_THEME` are now re-exported
  from `viewmodels/index.ts` for API symmetry with the other VMs.
- Python: removed unused `_status_text()` flat-string status assembler;
  the structured `_render_statusbar()` (segment-for-segment parity
  with the TS and C# bars) is the sole renderer.
- C#: removed unused `StringJoinConverter` (declared in `Converters.cs`
  and registered in `MainWindow.axaml`'s `Window.Resources`, but no
  binding ever referenced the `StringJoin` key). The class was a
  speculative future-DataGrid helper; if a v1.1 surface needs it, one
  commit reintroduces it.
- TypeScript Svelte view: a11y nudges — `ConfirmDialog` and the error
  modal autofocus their primary action so keyboard users can confirm
  with Enter/Space without tabbing; the status-bar warning chip is
  wrapped in `aria-live="polite"` `aria-atomic="true"` so the count is
  announced when it changes; the tab strip's `Author` / `Analysis`
  group labels carry `role="heading"` `aria-level="3"` so screen
  readers can jump between the two groups.
- C# `Solver.AltContribution` now emits `Console.Error.WriteLine` when
  a per-property normalizer is zero, with the same message text as the
  Python `warnings.warn` and TS `console.warn` (invariants.md §10.1).
  Previously C# was the only impl that skipped the property silently.

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
- The C# light theme switches `RequestedThemeVariant` so the FluentTheme chrome (window border, scrollbars, native dialogs) flips correctly, but the custom palette in `Resources/Colors.axaml` defines dark-mode tokens only. TS and Python ship explicit light-theme palettes; restructuring Avalonia's tokens into `ThemeDictionaries` for full parity is on the v1.1 backlog. Dark mode (the default) renders identically across all three.
- Cross-impl a11y parity is partial in v1.0: the TS Svelte view has autofocus on the primary action of `ConfirmDialog` and the error modal, `aria-live="polite"` on the status-bar warning chip, and `role="heading"` on the tab strip's `Author` / `Analysis` group labels. The C# Avalonia (`AutomationProperties.LiveSetting`, `FocusManager`) and Python NiceGUI (Quasar `q-notify` + per-control `aria-*`) equivalents are framework-specific and land in v1.1.
