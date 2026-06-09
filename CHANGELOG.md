# Changelog

All notable changes to GuideArch (the monorepo — TypeScript / C# / Python impls released together) are recorded here. Versioning follows the single-monorepo-version policy of [ADR-0005](spec/ADRs/0005-single-monorepo-version.md): a release pushes one tag and all three impls share its version number.

The format is loosely [Keep a Changelog](https://keepachangelog.com/en/1.1.0/). Dates are ISO 8601.

## [Unreleased]

Post-v1.0.0 maintenance focused on cross-impl parity and CI hardening; no
behavior change a user-facing release note would call out.

### Security
- TypeScript `vitest` devDependency bumped from `^2.0.0` to `^3.2.6` to
  close GHSA-4w7w-66w2-5vf9 (vite path-traversal, **critical**) and
  two transitive moderates (esbuild, vite-node). Devtools-only blast
  radius, but the only critical-severity advisory in the tree. The
  remaining low-severity `cookie<0.7.0` is in `@sveltejs/kit`'s
  upstream-pinned transitive; @sveltejs/kit is on the current ^2.x.

### Fixed
- Python `ScenarioVM.add_property` now enforces `weight > 0` at the
  Add boundary, matching `update_property` and C#'s `AddProperty`. The
  schema `$defs/Property.weight` is `exclusiveMinimum: 0`; the prior
  state accepted any float at Add-time and failed only at save-time
  schema validation.
- TypeScript `addProperty(name?, kind?, weight?)` now takes the full
  three-arg surface that Python `add_property` and C# `AddProperty`
  expose, with the same `weight > 0` guard. The prior maintenance pass
  added the C# overload explicitly for Python parity; TS was left
  narrower than both impls.
- TypeScript `FuzzyInput.svelte` resync now tracks the previous prop
  value in separate state instead of comparing `Number(localStr)` to
  the prop. The earlier `Number(lStr) !== lower` guard regressed
  per-keystroke editing: `bind:value={lStr}` updates the local string
  on every input event but `lower` only updates on `change` (blur),
  so typing `1` over `0.5` resolved as `Number("1") !== 0.5` → reset
  to `"0.5"` mid-keystroke, making coefficient cells uneditable. Now
  the resync only fires when the prop differs from a separately
  tracked `prev*` value — external prop changes still resync, local
  keystrokes don't trigger the reset.
- All three solvers now emit the M=0 diagnostic exactly once per VM
  solve cycle, not twice. The pass-1 dedup inlined the warning in
  `ComputeNormalizer`, which both `Solver.Solve` and
  `CriticalDecisions.Analyze` call — so the "warn once per (property,
  solve)" guarantee was actually firing twice. Split into a pure
  `Compute…` and a sibling `WarnDegenerateNormalizers`; only `Solve`
  calls the warner, so `CriticalDecisions.Analyze`'s recomputation is
  silent. Same `Property 'X' has M=0…` text on stderr.
- TypeScript Chart A bar opacity now floors at 0.5 at the last
  rendered bar — matching Python + C# and `spec/charts.md` §2. TS was
  dividing by `Math.max(data.length - 1, 30)`, so a 10-candidate render
  bottomed out at 0.85 instead of 0.5 (a visible mismatch with the
  other two impls at small candidate counts).
- TypeScript `FuzzyInput.svelte` now resyncs its local `lStr` / `mStr`
  / `uStr` strings when the prop diverges from the parsed local. Before,
  the local strings were set once at mount and never resynced; because
  `CoefficientsTab` keys `{#each}` by `(alt.id, prop.id)` and component
  instances are reused across re-solves, loading a new scenario left
  every visible fuzzy cell still showing the first-mount numbers until
  the user refocused.
- TypeScript `confirmDialog()` now settles any in-flight request as
  cancel before installing a new one. Without that, two close-together
  calls would replace the pending `ConfirmRequest` in the store and
  the first promise would never resolve — leaking both the promise and
  the closure that called `await confirmDialog(...)`.
- All three solvers now emit the "Property 'X' has M=0; skipping to
  avoid division by zero" diagnostic **once per (property, solve)**.
  Previously the warn fired from inside `altContribution()` per
  (alternative × candidate) call — O(N × C) duplicate warnings per
  solve that drowned browser devtools, Tauri logs, pytest captured
  warnings, and C# stderr. Lifted to `computeNormalizer()` /
  `_compute_normalizer()` / `ComputeNormalizer()`, which runs once
  per solve and visits each property once.
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
- `.github/workflows/vmx-bump.yml` now declares a workflow-scope
  `permissions: { contents: read }` default; the `check` job retains
  its explicit `contents: write` / `issues: write` override. Future
  jobs added without an explicit `permissions:` block will inherit
  read-only rather than the repo's default GITHUB_TOKEN write scopes.
- `langs/python/src/guidearch/models/topsis/solve.py` comment block
  uses ASCII `x` (was U+00D7 `×`) so `uv run ruff check` (RUF003) stays
  green — caught by pass-2 verify after the pass-1 M=0 dedup landed.
- `.github/dependabot.yml` now covers `langs/python/Dockerfile`
  (`package-ecosystem: docker`). The Python base image and the
  `ghcr.io/astral-sh/uv` stage will now receive automated bump PRs.
- `langs/typescript/package.json` `lint` script dropped the dead
  `--ext .ts,.svelte` flag; ESLint v10 silently ignores it (flat
  config governs file matching via `files:` globs).
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
- TypeScript `tests/integration/vm-mvvm.test.ts` docstring (line 8) and
  section comment (line 46) updated from "produces 1336 candidates" to
  "produces 720 candidates" — pass-1's stale-1336 sweep missed these two
  test-file comments (the actual assertion already used 720).
- TypeScript `critical-decisions.ts` merged two consecutive
  `import … from './solve.js'` statements into a single combined import.
- C# `Solver.CartesianProduct` early-returns on the first empty pool
  instead of checking `pools.Any(p => p.Count == 0)` after building the
  full intermediate product list. Matches the sibling
  `CriticalConstraints.CartesianProduct` early-exit pattern.
- Python `critical_decisions.py` and TypeScript `critical-decisions.ts`
  now reuse `_normalize_candidates` / `normalizeCandidates` from
  `solve.py` / `solve.ts` instead of inlining the §3.7-3.8 PIS/NIS
  pipeline. Mirrors the pass-1 C# refactor in
  `CriticalDecisions.Analyze`; future PIS/NIS tweaks now land in one
  place per language. Conformance still passes byte-for-byte.
- Python `viewmodels/__init__.py` now also re-exports
  `ScenarioMutationError` (from `scenario_vm.py`) and `register_theme`
  (from `app_vm.py`) — pass-1's symmetry pass with TypeScript's
  `viewmodels/index.ts` stopped two symbols short.
- TypeScript `app-vm.ts` now exports `registerTheme(name)` (idempotent
  add to `KNOWN_THEMES`), mirroring Python `register_theme` and C#
  `AppVMFactory.RegisterTheme`. Re-exported from `viewmodels/index.ts`.
- C# `tools/screenshot-all-tabs/Program.cs` `tabNames` array still had
  lowercase `"Critical decisions"` / `"Critical constraints"`; pass-1
  TitleCased the Python harness and missed this. Cross-impl screenshot
  filenames now match.
- Python `tests/unit/test_editor_cascades.py` ships
  `test_update_property_raises_on_non_positive_weight` (parity with
  the TS+C# regression guards added in pass 1).
- `spec/editors.md` §8 "Out of scope for M3" bullet TitleCased
  `Critical Decisions / Critical Constraints panels (M4)` for full
  document-wide consistency.
- C# `CriticalDecisions.Analyze` no longer reimplements the §3.7 PIS/NIS
  block and the §3.8 clip01-normalize loop verbatim from
  `Solver.NormalizeCandidates` (plus a private `Clip01` shadow). Replaced
  with a single call to the shared helper; conformance still passes
  byte-for-byte against `spec/conformance/expected/{sas,eds}.critical-decisions.json`.
- C# `ScenarioVMFactory.UpdateDecisionName` replaced the
  `IndexOf(FirstOrDefault(...)!)` two-walk-plus-null-forgive pattern with
  a straight single-pass for-loop (mirrors the existing
  `UpdateCoefficient`).
- C# `AddDecision(string? name = null)`, `AddAlternative(string, string? = null)`,
  and `AddProperty(string? name = null, PropertyKind? = null, double? = null)`
  now accept optional defaults, matching Python's `add_decision(name=…)` and
  TypeScript's `addDecision(name?)`. The C# methods previously hardcoded
  `"New decision"` / `"New alternative"` / `"New property"`.
- Python `viewmodels/__init__.py` now re-exports `AppVM`, `Mode`,
  `make_app_vm`, `DEFAULT_THEME`, and `known_themes` so the package
  surface mirrors TypeScript's `viewmodels/index.ts`. Module docstring
  shows AppVM at the top of the VM tree.
- C# `EditorCascadesTests` now ships the three Add-cascade tests that
  Python and TypeScript both have: `AddAlternative_CreatesZeroFuzzyCoefficients_ForEveryProperty`,
  `AddProperty_CreatesZeroFuzzyCoefficients_ForEveryAlternative`, and
  `UpdateProperty_ThrowsForNonPositiveWeight`. The production code
  already upheld each contract; the regression guards are new.
- TypeScript `Toolbar.svelte` removed dead `filePathStore = vmxToStore(vm, 'filePath')`
  whose value was unused after the canSave logic simplified to depend
  only on `$scenarioStore`.
- TypeScript: deleted ten unreferenced Windows-Store tile PNGs from
  `langs/typescript/src-tauri/icons/` (`Square30x30Logo.png` through
  `Square310x310Logo.png` + `StoreLogo.png`). Same scaffold-residue class
  as the `static/` SVGs removed in commit `b25e1bd`; `tauri.conf.json`
  only references the five icons actually bundled.
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
- `CONTRIBUTING.md` visual-harness setup now reads
  `uv sync --all-extras --group visual` (was missing `--all-extras`;
  per the project's documented invariant, plain `uv sync` strips the
  dev group and tanks ruff/mypy/pytest availability).
- `CONTRIBUTING.md` "Verify (CI)" recipes for all three languages now
  include their test runs: TS `pnpm check && pnpm test`, C# `dotnet
  test --nologo`, Python `uv run pytest tests/ -q`. The prior recipes
  exercised only the formatter/linter side, so contributors who "ran
  verify" locally still shipped without exercising the unit suites.
- `SECURITY.md` "Supported Versions" milestone range tightened from
  `v0.0.0-bootstrap through the M1–M4 milestone tags` (imprecise — M0
  is the start, not M1) to `v0.0.0-bootstrap (M0) through v0.4.0-m4 (M4)`.
- `langs/csharp/src/GuideArch.View/MainWindow.axaml.cs` comment
  pointing at `main.py:1042` / `ResultsTab.svelte:14` for the top-50
  cap (both line numbers had drifted) now uses symbolic anchors
  (`top50 = candidates[:50]` / `top50 = $candidatesStore.slice(0, 50)`)
  that don't rot on subsequent edits.
- `langs/python/src/guidearch/models/constraint.py` docstring removed
  the dangling "Space.cs line 975" reference (Space.cs doesn't exist
  in the repo; the legacy XML/C# pre-implementation is not vendored
  per ADR-0002). The substantive "biconditional, not the legacy
  implication form" callout is retained.
- `CONTRIBUTING.md` visual-harness bullet now spells out the Python
  Playwright first-run setup (`uv sync --group visual` and
  `uv run playwright install chromium`) so contributors hitting
  `ModuleNotFoundError: playwright` aren't sent to `langs/python/README.md`.
- `spec/viewmodels.md` §4.1 and `spec/editors.md` §3 had a stale
  `"Solved: 1336 candidates"` status example — SAS yields 720 candidates
  and EDS yields 2280; the 1336 literal predated the current sample
  corpus. Updated to `"Solved: 720 candidates"` (after loading SAS).
- `README.md` §5.5 lower-cased "Critical decisions tab" / "Critical
  constraints tab"; TitleCased to match the actual UI labels (cross-impl
  unification already shipped in `[Unreleased]` above).
- `spec/charts.md` §7 trailing editorial sentence about the C#-only
  `ScenarioState` rename removed (was fix rationale, not spec).
- `spec/charts.md` §8 replaced the misleading
  `tests/unit/chart-data.{py,cs,ts}` filename pattern with per-impl
  pointers to the real files; no impl uses that exact form.
- `spec/editors.md` §4 trimmed the dangling `docs/design/` reference;
  the no-pre-impl-spec-docs policy lives in maintainer practice, not in
  the repo.
- Spec milestone-tense `(M3)` / `(M4)` / `(M5)` suffixes dropped from
  `spec/editors.md`, `spec/charts.md`, and `spec/release.md` H1s — all
  M0–M5 shipped in v1.0; the suffix made each H1 read as in-progress.
- `spec/README.md` doc-index bullets are now actual markdown links to
  each file; the six ADRs are listed individually so the spec subtree
  has the same doc hub structure as the top-level README.
- `CONTRIBUTING.md` test guide now documents the three visual snapshot
  harnesses (Python `tests/visual/snapshot_all_tabs.py`, TS
  `tests/visual/snapshot-all-tabs.mjs`, C# `tools/screenshot-all-tabs/`).
- Python `tests/visual/snapshot_all_tabs.py` `_TAB_NAMES` list was still
  lowercase `"Critical decisions"` / `"Critical constraints"` while the
  UI tabs are TitleCase; the strict-mode `get_by_role` lookup was
  silently missing both. TitleCased the list, docstring, and inline
  comment.
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
