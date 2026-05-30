# GuideArch — Modernized Rewrite Design Spec

**Status:** Design approved, ready for implementation planning
**Date:** 2026-05-29
**Author:** Kaveh Razavi

---

## 1. Overview

GuideArch is an architecture-decision support tool built on **fuzzy multi-criteria decision analysis (MCDA)** using **TOPSIS** with triangular fuzzy numbers. A user models a decision space — *decisions* (e.g. "Database choice"), *alternatives* per decision (e.g. PostgreSQL, MySQL), quality *properties* with min/max kind and priority weight, and three families of *constraints* (threshold, dependency, conflict) — then the engine generates every feasible candidate architecture, ranks them by a weighted fuzzy score, and identifies which decisions and constraints matter most.

The two legacy implementations (`GuideArch.Old`, `GuideArch.Older`) targeted Silverlight, which is end-of-life. This document describes a modernized rewrite shipped as **three concurrent implementations** sharing one language-neutral spec, all built on the **VMx** MVVM framework.

## 2. Goals & Non-Goals

### Goals

- Preserve the domain (decisions, alternatives, properties, constraints, fuzzy values, TOPSIS) faithfully
- Modernize the UX without rethinking the underlying methodology
- Demonstrate VMx working at scale on a real application across three languages
- Ship a professional-quality OSS repo at `github.com/thekaveh/GuideArch`
- Cross-platform: every implementation targets **desktop + web** from a single codebase
- Spec-driven development: a language-neutral spec gates every implementation through a shared conformance corpus

### Non-Goals (v1)

- Authentication, multi-user collaboration, cloud sync
- Mobile platforms (deferred to v2.0)
- Constraint solver beyond the three legacy constraint kinds (deferred to v1.3)
- Plugin / extension system
- Internationalization
- Theming beyond one tasteful default + dark mode

## 3. Architecture

Three implementations, one spec. Each implementation realizes the same conceptual layering with idiomatic tooling:

```
Models  ──  Topsis  ──  ViewModels  ──  (Adapter?)  ──  View
                          ↑
                          │  built on
                          │
                       VMx (lang port)
                          ↑
                          │  shared
                          │
                  vendor/vmx (submodule)
```

The **spec directory** is the source of truth. It owns: the scenario JSON schema, the formal algorithm descriptions, and the conformance corpus. Every implementation answers to it. CI fails any divergence.

VMx is consumed as a **git submodule** at `vendor/vmx/`, pinned to a stable tag. Each language consumes the submodule via local/editable install for development and via the published package (npm / NuGet / PyPI) for end users. Toggle scripts (`tools/use-vmx-local.{sh,ps1}` and `tools/use-vmx-released.{sh,ps1}`) flip between the two.

## 4. Repository Layout

```
GuideArch/
├── README.md
├── LICENSE                          # MIT
├── CONTRIBUTING.md
├── CODE_OF_CONDUCT.md
├── SECURITY.md
├── .gitignore                       # excludes .claude/, .superpowers/, etc.
├── .gitmodules                      # vendor/vmx
│
├── .github/
│   ├── workflows/
│   │   ├── spec.yml
│   │   ├── conformance.yml          # the keystone: cross-impl conformance gate
│   │   ├── typescript.yml
│   │   ├── csharp.yml
│   │   ├── python.yml
│   │   └── vmx-bump.yml
│   ├── ISSUE_TEMPLATE/
│   │   ├── bug.md
│   │   ├── feature.md
│   │   ├── conformance-divergence.md
│   │   └── question.md
│   ├── PULL_REQUEST_TEMPLATE.md
│   └── dependabot.yml
│
├── spec/                            # language-neutral source of truth
│   ├── README.md
│   ├── domain/
│   │   ├── scenario.schema.json
│   │   ├── glossary.md
│   │   └── invariants.md
│   ├── algorithms/
│   │   ├── topsis.md
│   │   ├── critical-decisions.md
│   │   └── critical-constraints.md
│   ├── viewmodels.md                # the shared VM tree shape
│   ├── conformance/
│   │   ├── scenarios/               # JSON inputs
│   │   ├── expected/                # JSON expected outputs
│   │   └── tolerances.json
│   └── ADRs/
│       ├── 0001-three-impls-vmx-submodule.md
│       ├── 0002-json-schema-not-xml.md
│       ├── 0003-topsis-no-msf.md
│       ├── 0004-mit-license.md
│       ├── 0005-single-monorepo-version.md
│       └── 0006-nicegui-over-shiny.md
│
├── vendor/
│   └── vmx/                         # git submodule → github.com/thekaveh/VMx, pinned tag
│
├── langs/
│   ├── typescript/                  # Tauri 2 + Svelte 5 + VMx-TS
│   ├── csharp/                      # Avalonia 12 + VMx-C#
│   └── python/                      # NiceGUI 3.x + VMx-Python
│
├── tools/
│   ├── import-legacy-xml.py         # one-shot SAS.xml / EDS.xml → JSON
│   ├── use-vmx-local.sh
│   └── use-vmx-released.sh
│
└── docs/
    ├── architecture.md
    ├── getting-started/
    │   ├── typescript.md
    │   ├── csharp.md
    │   └── python.md
    ├── design/                      # design specs (this file lives here)
    └── porting-notes.md
```

## 5. Domain Model (Models layer)

Each implementation places these types under a dedicated `Models/` folder/namespace. **Symmetric naming convention:** every Model class carries the `M` suffix, every ViewModel class carries the `VM` suffix. Folder placement and suffix both signal layer — never one without the other.

### Canonical entities

| Entity | Notes |
|---|---|
| `DecisionM` | A decision point (e.g. "Database choice"). Owns a unique id and a display name. |
| `AlternativeM` | An option for one decision (e.g. "PostgreSQL"). Owns id, decision id, display name. |
| `PropertyM` | A quality attribute (e.g. "Cost"). Owns id, name, **kind** (`min` or `max`), and **weight** (priority). |
| `TriangularFuzzyM` | Triangular fuzzy number `{ lower, modal, upper }`, `lower ≤ modal ≤ upper`. |
| `NormalizedFuzzyM` | Normalized fuzzy representation `{ positive, average, negative }` used internally by TOPSIS. |
| `CoefficientM` | The `(alternative × property) → TriangularFuzzyM` mapping. |
| `ConstraintM` | Tagged union with three kinds: `threshold`, `dependency`, `conflict`. |
| `CandidateM` | A complete architecture (set of selected alternatives), with aggregate per-property values and final score after TOPSIS. |
| `ScenarioM` | Root container. Owns all of the above plus a `ConfigM`. |
| `ConfigM` | `{ aggregation: 'sum' \| 'max', weights: { positive, average, negative } }`. |

### Constraint variants

```typescript
type ConstraintM =
  | { kind: 'threshold';  propertyId: Id; min?: number; max?: number }
  | { kind: 'dependency'; sourceAlternativeId: Id; targetAlternativeId: Id }
  | { kind: 'conflict';   alternativeAId: Id; alternativeBId: Id };
```

### Identifier strategy

All cross-entity references use string ids (`d-database`, `a-postgres`, `p-cost`) — never indices or names. This survives reordering, renaming, and human editing of scenario files.

## 6. Scenario File Format

Format: **JSON**, validated against `spec/domain/scenario.schema.json`. (XML import from legacy files is available via `tools/import-legacy-xml.py` but is not a runtime format.)

```jsonc
{
  "schemaVersion": "1.0.0",
  "name": "Service-Oriented Architecture Scenario",
  "description": "...",
  "decisions": [
    { "id": "d-database", "name": "Database choice" }
  ],
  "alternatives": [
    { "id": "a-postgres", "decisionId": "d-database", "name": "PostgreSQL" }
  ],
  "properties": [
    { "id": "p-cost", "name": "Cost", "kind": "min", "weight": 0.3 }
  ],
  "coefficients": [
    { "alternativeId": "a-postgres", "propertyId": "p-cost",
      "value": { "lower": 2.0, "modal": 3.0, "upper": 4.5 } }
  ],
  "constraints": [
    { "kind": "threshold", "propertyId": "p-cost", "max": 100 }
  ],
  "config": {
    "aggregation": "sum",
    "weights": { "positive": 0.5, "average": 0.3, "negative": 0.2 }
  }
}
```

## 7. Algorithms

Algorithms are part of the spec and live in `Models/Topsis/` (subfolder of Models, not a separate Services layer — pragmatic over textbook DDD for a focused tool). Each language ports the same algorithm; cross-impl numerical conformance is gated at **≤1e-9 absolute** on scalar outputs and **stable ranking under id tie-break** for categorical outputs.

### TOPSIS (fuzzy multi-criteria ranking)

1. **Enumerate raw candidates** as the cartesian product of alternatives across decisions, filtered by dependency and conflict constraints.
2. **Aggregate fuzzy values** per `(candidate, property)` as componentwise triangular sum of constituent coefficients.
3. **Apply threshold constraints** by defuzzifying each per-property aggregate (centroid) and rejecting candidates outside any property's `[min, max]`.
4. **Determine PIS / NIS** per property over the remaining candidate set, respecting `kind` (min vs max).
5. **Compute fuzzy distance** `D(c, p, ideal)` for each candidate-property pair against PIS and NIS.
6. **Per-fuzzy-dimension relative closeness** φ⁺, φᵃ, φ⁻ aggregated by property weight.
7. **Final score** `AGGREGATE(w⁺·φ⁺, wᵃ·φᵃ, w⁻·φ⁻)` where AGGREGATE is `sum` or `max` per `config.aggregation`.
8. **Sort descending** by score; ties broken deterministically by candidate id.

The fuzzy-distance function `D(·,·)` will be pinned during M1 in `spec/algorithms/topsis.md`, with full math and a worked example, by inspecting `.Old/GuideArch.Model/Space.cs` to ensure parity with legacy semantics.

### Critical decisions (sensitivity analysis)

For each decision, perturb its constituent alternatives' coefficients by ±10% (configurable) and measure rank-distance of the top-N candidate set (default N=20) against the baseline. Decisions whose perturbation moves the top set the most are most critical.

### Critical constraints (elimination counting)

For each constraint, count how many otherwise-feasible candidates it eliminates. Highly-eliminating constraints are most binding; zero-eliminating ones are flagged as redundant in the UI.

## 8. ViewModel Layer (shared shape)

Every implementation realizes this VM tree using its language's VMx primitives. The tree shape, command names, and observable property names are part of the spec (`spec/viewmodels.md`) — stricter than necessary but it makes cross-impl review trivial.

| ViewModel | VMx primitive | Role |
|---|---|---|
| `ScenarioVM` | `AggregateVM` | Root; owns children; commands `NewCmd`, `OpenCmd`, `SaveCmd`, `SaveAsCmd`, `SolveCmd` |
| `DecisionsVM` | `CompositeVM<DecisionVM>` | List editor (add/rename/reorder/delete) |
| `DecisionVM` | `ComponentVM<DecisionM>` | One decision |
| `AlternativesVM` | `CompositeVM<AlternativeVM>` | Alternatives within a decision |
| `AlternativeVM` | `ComponentVM<AlternativeM>` | One alternative |
| `PropertiesVM` | `CompositeVM<PropertyVM>` | Properties (kind + weight) |
| `PropertyVM` | `ComponentVM<PropertyM>` | One property |
| `CoefficientsVM` | `CompositeVM<CoefficientVM>` | The `alternative × property` matrix |
| `ConstraintsVM` | `AggregateVM3` | Threshold + dependency + conflict tabs |
| `CandidatesVM` | `CompositeVM<CandidateVM>` | Ranked results |
| `CriticalDecisionsVM` | `CompositeVM<CriticalDecisionVM>` | Sensitivity output |
| `CriticalConstraintsVM` | `CompositeVM<CriticalConstraintVM>` | Elimination-count output |
| `AnalysisVM` | `AggregateVM2` | Charts container |

## 9. Per-Impl Architecture

### TypeScript + Tauri 2 + Svelte 5

| Layer | Tech |
|---|---|
| Build | Vite + TypeScript strict; tsup for shared lib parts |
| Runtime — desktop | Tauri 2 (Rust shell; OS webview, ~10 MB binaries) on Win/macOS/Linux |
| Runtime — web | Same Svelte build as static SPA, no Tauri shell |
| Reactivity | Svelte 5 runes (`$state`, `$derived`); adapter mirrors VMx properties as derived runes |
| Adapter | `vmxToSvelteStore<T>(vm, propName) → Readable<T>` — subscribes to VMx's rxjs hub on `PropertyChangedMessage`, emits new values |
| File I/O | `@tauri-apps/api/fs` on desktop; File System Access API + IndexedDB on web |
| Charts | Visx or LayerChart — decided at first chart |
| Tables | TanStack Table |
| JSON Schema | `ajv` |

### C# + Avalonia 12

| Layer | Tech |
|---|---|
| Build | .NET 8 SDK, Avalonia 12.x; `dotnet publish` for desktop; `Avalonia.Browser` for WASM |
| Runtime — desktop | Native Avalonia on Win/macOS/Linux |
| Runtime — web | Avalonia.Browser (WASM), same XAML |
| Reactivity | XAML data binding to VMx-emitted `INotifyPropertyChanged` and `ICommand` — **native, no adapter** |
| File I/O | `System.IO` for desktop; `Avalonia.Platform.Storage` for browser |
| Charts | ScottPlot 5 (fallback: LiveChartsCore) |
| JSON Schema | `JsonSchema.Net` |

The fact that C# needs no adapter is the cleanest demonstration that VMx-C# is faithful to native .NET MVVM conventions.

### Python + NiceGUI 3.x

| Layer | Tech |
|---|---|
| Build | `hatchling`, `pyproject.toml`; `pip install nicegui pywebview` |
| Runtime — desktop | `ui.run(native=True)` → pywebview window using the OS webview, ~15 MB on top of Python |
| Runtime — web | `ui.run()` → uvicorn ASGI server; deployable as plain process, Docker, or behind nginx |
| Reactivity | NiceGUI's `bindable_property` + `@bindable_dataclass` + 3.0 `Event` class |
| Adapter | `vmx_bindable(vm, prop_name) → bindable proxy` — subscribes to VMx's reactivex `PropertyChangedMessage`, fans into NiceGUI binding propagation; ~30–50 LOC |
| File I/O | `pathlib` for desktop; `ui.upload` / `ui.download` for web |
| Charts | `ui.echart` (Apache ECharts) |
| Tables | `ui.aggrid` (AG Grid) for the coefficient matrix and ranked candidates; `ui.table` for lighter views |
| JSON Schema | `jsonschema` |

NiceGUI was chosen over Shiny because (1) NiceGUI's explicit binding system + `bindable_dataclass` + 3.0 `Event` class semantically matches VMx's ViewModel/property-change paradigm more directly than Shiny's reactive graph, (2) `ui.run(native=True)` gives Python desktop deployment from the same codebase, making the tri-impl story symmetric, and (3) Quasar Material Design + Tailwind 4 + dark mode produce a more polished baseline than Shiny's Bootstrap dashboard. See `spec/ADRs/0006-nicegui-over-shiny.md`.

### Cross-cutting per impl

1. **Scenario loader** — reads JSON, validates against `spec/domain/scenario.schema.json`, builds an in-memory `ScenarioM`. Validation error messages are part of conformance.
2. **Conformance runner** — reads every file in `spec/conformance/scenarios/`, runs TOPSIS + critical-decisions + critical-constraints, compares against `spec/conformance/expected/` within `spec/conformance/tolerances.json`. Fails CI on divergence.

## 10. Conformance Strategy

Three implementations stay honest **only** because of the conformance corpus. Without it, drift is inevitable.

- **Seed corpus:** the legacy `SAS.xml` (`.Old`) and `EDS.xml` (`.Older`) imported via `tools/import-legacy-xml.py`. Plus a small set of hand-crafted synthetic scenarios covering edge cases (empty constraints, single-decision-single-property, all-conflicts-eliminating-everything, near-tied scores).
- **Expected outputs:** generated once by running `.Old/GuideArch.Console` against each scenario, snapshot to JSON. Stored at `spec/conformance/expected/<scenario>.candidates.json` etc. Tolerance against legacy: `1e-6` (looser than the per-impl `1e-9`, accepting some legacy numerical drift).
- **CI gate:** `conformance.yml` runs each impl against the full corpus on every PR. Divergence beyond tolerance = red build = no merge.
- **Adding a feature:** spec change first (schema bump + algorithm doc + new conformance scenarios). Implementations then catch up; CI enforces parity before tag.

## 11. GitHub Plan

| Decision | Choice |
|---|---|
| Repo URL | `github.com/thekaveh/GuideArch` |
| Visibility | Public |
| License | MIT (matches VMx) |
| Default branch | `main` |
| Topics | `mvvm`, `topsis`, `fuzzy-logic`, `decision-analysis`, `software-architecture`, `avalonia`, `tauri`, `svelte`, `nicegui`, `vmx` |
| Versioning | Single monorepo version (`v1.0.0` releases all three) |
| Releases | TS: Tauri installers + web bundle. C#: Avalonia desktop binaries + WASM. Python: PyPI + Docker. |
| Submodule pin | VMx pinned to upstream `main` HEAD at bootstrap (commit `e2b23f8`, described as `python-v1.0.0-66-ge2b23f8`); VMx has no repo-wide semver tag yet, only per-language tags. The `vmx-bump.yml` workflow opens an issue weekly when `main` advances. Re-pin to a stable tag once VMx publishes one. |
| Commit hygiene | No AI/Claude/Co-Authored-By trailers anywhere |
| `.gitignore` | Excludes `.claude/`, `.superpowers/`, `.aider*`, `.cursor/`, `.continue/`, etc. |
| Scaffolding | README, CONTRIBUTING, CODE_OF_CONDUCT (Contributor Covenant 2.1), SECURITY, issue + PR templates, dependabot |

## 12. v1.0.0 Scope

### Must-have (gate for v1 tag)

**Domain & engine:** full Models layer, scenario JSON load/save with schema validation, TOPSIS, critical-decisions, critical-constraints, configurable aggregation (sum/max) and weights.

**File ops:** New, Open, Save, Save As — per-impl native file dialog.

**Editor UI:** decisions, alternatives, properties, coefficients (fuzzy matrix), constraints (all three kinds).

**Results UI:** ranked candidates table, critical decisions list, critical constraints list.

**Analysis UI:** ranked-candidates score chart (bar), fuzzy-value triangle visualizer for the selected candidate.

**Quality:** full conformance corpus green in all three impls; per-impl unit tests; README + per-impl quickstarts.

### Deferred to post-v1

Undo/redo, multi-scenario tabs, in-app XML import, constraint solver, auth/multi-user/collaboration, cloud sync, mobile, plugin system, dark-mode toggle (we ship one tasteful theme), i18n.

## 13. Milestones

| # | Name | Done when |
|---|---|---|
| **M0** | Repo bootstrap | Repo at `thekaveh/GuideArch`, VMx submodule wired, three lang scaffolds, CI green on empty workloads, license + governance files |
| **M1** | Spec + Models + TOPSIS | `spec/` finalized; all three impls Models + TOPSIS pass conformance against `sas.json` + `eds.json`; no UI |
| **M2** | ViewModels + skeleton UI | Full VM tree per impl; minimal "open file → see ranked list" UI per impl |
| **M3** | Full editors | Coefficient grid + constraint editors complete; save round-trips |
| **M4** | Analysis + charts | Both v1 charts in all three impls; critical decisions/constraints views complete |
| **M5** | v1.0.0 release | Installers built, web bundles deployed, smoke-tested, release notes drafted, tag pushed |

## 14. Roadmap Beyond v1

| Version | Theme |
|---|---|
| v1.1 | Undo/redo + multi-scenario tabs + in-app legacy XML import |
| v1.2 | Scenario comparison (diff two scenarios side-by-side) |
| v1.3 | Pluggable solver backend (Z3 / MiniZinc / OR-Tools) for richer constraint kinds |
| v1.4 | Collaboration: cloud-saved scenarios + shareable URLs |
| v2.0 | Mobile targets (Tauri 2 mobile for TS, Avalonia mobile for C#; Python deferred) |

## 15. Initial ADR Set

The following Architecture Decision Records are committed at M0:

1. **ADR-0001** Three implementations sharing one spec, VMx as submodule
2. **ADR-0002** JSON Schema as the scenario format (not legacy XML)
3. **ADR-0003** TOPSIS algorithm is custom in-repo code, not Microsoft Solver Foundation
4. **ADR-0004** MIT license (matches VMx)
5. **ADR-0005** Single monorepo version, all three impls released together
6. **ADR-0006** NiceGUI 3.x as the Python view layer (not Shiny, not Streamlit)

## 16. Open Questions (deferrable; not blockers)

- **Charting choice in TS:** Visx vs LayerChart — decided at first chart implementation in M4.
- **Conformance numerical tolerance against legacy** — starts at `1e-6` but may need adjustment after M1 baseline generation.
- **Whether `vmx-bump.yml` opens PRs automatically or just files issues** — files an issue (decided at M0; chosen because the bump requires a deliberate human review of the diff between the pinned and upstream commits).
- **Standalone Python executable packaging** (PyInstaller) — known multiprocessing-edge-case in NiceGUI native mode; deferred past v1.0.
- **Defuzzification method for threshold constraints** (Step 3 of TOPSIS): proposed as centroid defuzzification, but must be verified during M1 against `.Old/GuideArch.Model/Space.cs` semantics. If the legacy uses a different method (e.g., max-membership or area-balance), the spec adopts the legacy choice to preserve conformance against the seed corpus.

---

## Approval

Design sections all approved during brainstorming session 2026-05-29. Ready to proceed to implementation planning.
