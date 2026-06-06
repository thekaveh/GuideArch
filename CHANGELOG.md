# Changelog

All notable changes to GuideArch (the monorepo — TypeScript / C# / Python impls released together) are recorded here. Versioning follows the single-monorepo-version policy of [ADR-0005](spec/ADRs/0005-single-monorepo-version.md): a release pushes one tag and all three impls share its version number.

The format is loosely [Keep a Changelog](https://keepachangelog.com/en/1.1.0/). Dates are ISO 8601.

## [Unreleased]

Post-v1.0.0 maintenance focused on cross-impl parity and CI hardening; no
behavior change a user-facing release note would call out.

### Fixed
- TypeScript `updateConstraint(index, c)` now asserts the existing
  constraint at `index` is the same kind as `c` and throws
  `ScenarioMutationError` on a kind change. The Python+C# typed
  surfaces (`update_threshold_constraint` etc.) already enforced this
  per `spec/viewmodels.md` §5.5; the TS generic shim skipped the check,
  so a caller could silently flip a threshold constraint into a
  dependency at a global index and break the
  `CriticalConstraintM.constraintIndex` invariant the typed surfaces
  uphold.
- C# Chart C `PrepComparisonSeries` now caps `N` at
  `ChartData.ComparisonPalette.Length` in addition to `topN` and
  `candidates.Length` — matching `spec/charts.md` §4 and the
  TypeScript + Python implementations. Without the palette bound a
  caller passing `topN > 10` would receive more series than the
  palette had entries; the View then wrapped
  `PaletteIndex % palette.Length` and silently re-used colors,
  breaking the stable per-rank-color contract. The 10-entry
  Tableau-10 palette has also been lifted from `MainWindow.axaml.cs`
  into `ChartData.ComparisonPalette` so the contract is now testable
  from `GuideArch.ViewModels.Tests` without instantiating Avalonia.
- Python Chart A bar opacity now floors at 0.5 at the bottom of the
  list, matching `spec/charts.md` §2 and the C# implementation; was
  0.55, which read noticeably crisper than the C# render.
- Python Chart B (fuzzy-decomposition triangle) now emits **one triangle
  per property** as `spec/charts.md` §3 requires — matching TypeScript's
  `buildTriangleSeriesData` and C#'s `PrepTriangleSeries`. The Python
  rendering was previously a single aggregate-triangle series read off
  `CandidateM.triangular_value`, which lost the per-property
  decomposition the chart exists to show.
- Python `OpenCmd` no longer emits a transient `"Loaded: {name}"`
  status before the auto-solve overwrites it with `"Solved: N candidates"`
  — TS and C# go straight to the solved-status line in one step. Only
  Python subscribers used to see the intermediate value.
- C# Coefficients tab cells are now click-to-edit (previously the
  DataGrid columns were init-only because they were bound to record
  properties; click selected the row but edit never started). Chart B
  and Chart C now refresh on coefficient edits with a 50 ms debounce so
  the Results tab no longer flickers under rapid edits.
- Python `--native` mode now boots reliably on every platform. The
  console-script entry point detects the flag and re-execs itself as
  `python -m guidearch.main`, giving the multiprocessing spawn child
  that drives pywebview a stable package-qualified `__main__` to
  import. Without that handoff, on some platforms the HTTP server
  started but the pywebview window never surfaced.
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
- All read-only CI workflows (`conformance`, `csharp`, `python`, `spec`,
  `typescript`) now declare `permissions: { contents: read }` at workflow
  scope so the `GITHUB_TOKEN` they receive is locked to read-only
  contents access. `release.yml` and `vmx-bump.yml` retain their
  explicit write scopes.
- `conformance.yml`'s TypeScript job now builds VMx-TS's `dist/` before
  `pnpm install`, mirroring `typescript.yml`. Defensive parity: the
  conformance runner currently imports only from `../models/` (which
  doesn't touch vmx), so the job passes without this step today — but
  any later vmx-touching code added to the runner would silently break
  a fresh-checkout CI run.
- Workflow job names re-aligned with `main`'s required status checks.
  `spec.yml`'s only job renamed `validate → spec`, and `conformance.yml`
  gained a small aggregator job `conformance` that `needs: [python,
  csharp, typescript]` and fails iff any sub-job failed. Without these,
  the protection's `spec` + `conformance` gates sat in `expected` state
  forever and blocked every PR merge (even `--admin`) once
  `enforce_admins=true` landed. PR #14 surfaced this when its initial
  admin merge was rejected.
- `release.yml`: `dtolnay/rust-toolchain@stable` (a mutable branch
  ref) replaced with a SHA pin
  (`@29eef336d9b2848a0b548edc03f92a220660cdb8 # stable`) plus an
  explicit `toolchain: stable` input. A force-push or repo compromise
  on the action's `stable` branch can no longer silently swap the
  toolchain action this release job runs with `contents: write` /
  `packages: write`. Dependabot's github-actions rule will bump the
  SHA the same way it bumps every other action pin.
- `release.yml`: the PyPI publish step now passes the token through
  `TWINE_USERNAME` / `TWINE_PASSWORD` env-vars instead of a
  `--password "$PYPI_API_TOKEN"` CLI flag, so the token isn't visible
  in the runner's process listing.

### Refactored
- C#: constraint mutators now take the **global** index into
  `scenario.constraints` (the same index space used by
  `CriticalConstraintM.constraintIndex` and the Python + TypeScript
  APIs). Replaced the three per-kind `Delete{Threshold,Dependency,
  Conflict}Constraint(int)` methods with a single kind-agnostic
  `DeleteConstraint(int)`; added typed `UpdateDependencyConstraint` /
  `UpdateConflictConstraint` (with the same invariant-2.5 + 7.1 guards
  the Add* path enforces) for symmetry with the existing
  `UpdateThresholdConstraint`, which itself moved from per-kind to
  global indexing with a kind-assertion. `spec/viewmodels.md` §5.5
  pins the canonical surface.
- Python: dropped unused `numpy>=1.26` from `langs/python/pyproject.toml`;
  no source or test imports it and it isn't a transitive of any other
  dependency.
- TypeScript: deleted unreferenced Tauri scaffold SVGs from
  `langs/typescript/static/` (`svelte.svg`, `tauri.svg`, `vite.svg`).
  Only `favicon.png` is referenced by `app.html`.
- Python `theme.py`: the Quasar `ui.colors(...)` call now references
  the `TOKENS` dict (`TOKENS["accent"]` etc.) instead of duplicating
  six hex literals already declared above. A future palette tweak in
  `TOKENS` won't silently leave Quasar's accent colors stale.
- Deleted `spec/conformance/.keep` — empty marker from the original
  scaffold; `spec/conformance/` long ago acquired `scenarios/`,
  `expected/`, and `tolerances.json`.
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
- README's documentation hub (§3) now links to `CHANGELOG.md` at the
  head of §3.3 (renamed *Release history & governance*). Previously
  the most actively-maintained doc outside `spec/` was orphaned from
  the top-level entry point.
- `langs/csharp/README.md`: documented the `tools/screenshot-all-tabs/`
  console tool that runs the Avalonia app under `Avalonia.Headless`
  and writes per-tab PNGs to `tests/visual/snapshots/` (mirrors the
  Python + TS visual harnesses). Was undocumented despite building
  with the solution.
- `spec/algorithms/critical-decisions.md` edge-case for
  `|candidates| == 1` previously said impls return decisions in
  `scenario.decisions` order. All three impls have always sorted by
  `(score asc, decisionId lex)` — matching the candidate-side
  lexicographic tie-break in `topsis.md` §3.10. Spec card was the
  inconsistent one; corrected to describe shipped behavior.
- `spec/charts.md` §7 selection-state heading anchored on the C#-only
  `ScenarioState` record name; renamed to `ScenarioVM.selectedCandidate
  Index` to match `viewmodels.md` §4.1 (both sections now describe the
  same observable).
- `spec/release.md` §1.3 Docker bullet said the image installs deps
  via `uv sync` and runs `uv run guidearch`. Shipped Dockerfile
  actually runs `uv sync --no-dev` and
  `uv run guidearch --port 8080`; spec aligned to shipped behavior.
- README, SECURITY, `spec/README`, `CONTRIBUTING`, and all per-impl
  READMEs aligned with v1.0.0 reality.
- C# WebAssembly target marked deferred to v1.1 (was claimed in README
  but not built).
- Spec self-contradictions (debounce wording, threshold-bound + self-edge
  severity, tab-count, broken cross-refs) resolved.
- Added `CHANGELOG.md`.
- `README.md` and per-impl READMEs now say the toolbar buttons are
  **Sample SAS** / **Sample EDS** (the actual labels in all three impls);
  the longer **Open Sample SAS** wording is the first-launch hero CTA.
- `spec/viewmodels.md` §5.5 pins the canonical constraint-mutator surface
  (typed `update*Constraint` triplets + kind-agnostic `deleteConstraint`,
  all on the global `scenario.constraints` index space — the same one
  `CriticalConstraintM.constraintIndex` uses). §2 admonition flags that
  the intermediate composite/aggregate VMs (DecisionsVM, PropertiesVM,
  ...) are aspirational at v1.0; §7 clarifies the M2 table is top-50
  while the alongside Chart A is top-30 (deliberately different windows).
- `spec/editors.md` §2.4: call out the deliberate asymmetry between the
  warning-level coefficient ordering rule (invariants §4.1, non-fatal)
  and the fatal threshold `min ≤ max` rule (invariants §6).
- `spec/release.md` §1.1: list the Windows NSIS `.exe` Tauri target
  alongside `.msi` (was already shipped via `tauri.conf.json
  "targets": "all"`); soften §4's "must agree" to advisory since the
  release workflow derives the version from the tag.
- `spec/algorithms/topsis.md` §3.2: preface `Space.cs` line references
  as historical pointers into the legacy code (ADR-0003 — not committed
  here); a `grep` returns nothing.
- `spec/ADRs/0006`: updated the pre-impl "30–50 LOC adapter" estimate
  to match the shipped ~140 LOC `vmx_to_nicegui.py`.
- `spec/viewmodels.md`/`editors.md`/`charts.md`: preface notes that
  M0–M5 all shipped in v1.0.0 — milestone-tense passages describe scope
  as authored, not in-progress work.
- `CONTRIBUTING.md`: prepend `uv sync --all-extras` to the Python verify
  and apply recipes; bare `uv sync` strips the dev group.

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
