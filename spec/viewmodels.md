# ViewModel layer — formal specification

**Status:** Authoritative. All three impls must realize this tree with the names and shapes below.

## 1. Purpose

The ViewModel layer wraps the M1 Models in **observable** types that the View can data-bind to. It introduces no domain logic — every algorithmic call goes through the `Models` and `Topsis` modules unchanged. Its single job is to translate "user edited a property weight" into "rerun TOPSIS and notify subscribers that `RankedCandidates` changed."

VMs are built on **VMx** (the submodule at `vendor/vmx/`). Per the project ADRs:

- C# uses `ComponentVM<M>` with the builder pattern (sealed; cannot subclass — VMs are factory-built).
- Python uses `ComponentVMOf[M]` with `.builder()`.
- TypeScript uses `ComponentVMOf<M>` with `.builder()`.

All require a `MessageHub` and `Dispatcher` from VMx. M0 used the null variants for the smoke samples; M2 still uses the null variants because there's no inter-VM messaging yet beyond the standard property-change hub.

## 2. The VM tree

The conceptual tree is the same in every impl. Names below are the canonical names (each language adapts case: `ScenarioVM` in C#, `scenario_vm` in Python source filenames but `ScenarioVM` as the class identifier, `scenarioVm.ts` filename / `ScenarioVM` exported type for TS).

```
ScenarioVM
├── DecisionsVM             (CompositeVM<DecisionVM>)
│   └── DecisionVM (×N)     (ComponentVMOf<DecisionM>)
│       └── AlternativesVM  (CompositeVM<AlternativeVM>)
│           └── AlternativeVM (×N) (ComponentVMOf<AlternativeM>)
├── PropertiesVM            (CompositeVM<PropertyVM>)
│   └── PropertyVM (×N)     (ComponentVMOf<PropertyM>)
├── CoefficientsVM          (specialized — see §3.6)
├── ConstraintsVM           (AggregateVM3)
│   ├── ThresholdConstraintsVM   (CompositeVM<ThresholdConstraintVM>)
│   ├── DependencyConstraintsVM  (CompositeVM<DependencyConstraintVM>)
│   └── ConflictConstraintsVM    (CompositeVM<ConflictConstraintVM>)
├── CandidatesVM            (CompositeVM<CandidateVM>) — result of solve, read-only
├── CriticalDecisionsVM     (CompositeVM<CriticalDecisionVM>) — read-only
├── CriticalConstraintsVM   (CompositeVM<CriticalConstraintVM>) — read-only
└── AnalysisVM              (AggregateVM2 — charts, M4)
```

**Where VMx-C# is sealed (no inheritance):** the C# impl realizes each "VM" above as a static-factory function returning a `ComponentVM<XxxM>` configured with the necessary observable properties. There is no `class DecisionVM : ComponentVM<DecisionM>`.

## 3. ScenarioVM — root

The root VM owns the loaded scenario and brokers re-solve when its children change.

### 3.1 Observable properties

| Property | Type | Description |
|---|---|---|
| `scenario` | `ScenarioM \| undefined` | The currently loaded scenario, or `undefined` if no file is open. |
| `filePath` | `string \| undefined` | Path of the currently open file, or `undefined` for an unsaved new scenario. |
| `isDirty` | `boolean` | `true` if the model has changed since the last save. |
| `candidates` | `readonly CandidateM[]` | Latest `solve(scenario)` output. Empty array if scenario undefined. |
| `criticalDecisions` | `readonly CriticalDecisionM[]` | Latest `criticalDecisions(scenario)` output. |
| `criticalConstraints` | `readonly CriticalConstraintM[]` | Latest `criticalConstraints(scenario)` output. |
| `status` | `string` | Human-readable status line for the View (e.g. `"Solved: 1336 candidates"`). |
| `warnings` | `readonly string[]` | Non-fatal warnings from scenario load. |
| `selectedCandidateIndex` | `int \| null` | Index into `candidates` of the currently selected candidate, or `null` when nothing is selected. The candidates table, Chart A, and Chart B all read it; editing it from any of the three updates the other two. Defined formally in `spec/charts.md` §6 — added here for completeness. |

### 3.2 Commands

| Command | Shape | Behavior |
|---|---|---|
| `NewCmd` | `() -> void` | Replace `scenario` with a fresh empty `ScenarioM`. Clear `filePath`. Set `isDirty = false`. Re-solve (will be empty). |
| `OpenCmd` | `(path: string) -> void` | Call `loadScenario(path)`. On success: set `scenario`, `filePath`, clear `isDirty`, re-solve. On failure: do not mutate state; emit warning. |
| `SaveCmd` | `() -> void` | Write current `scenario` to `filePath` as JSON via the impl's scenario serializer. Output must be `JSON.stringify`-compatible (any compliant JSON reader can round-trip it). Clear `isDirty`. Disabled (`canExecute = false`) if `filePath` undefined. **Note:** key ordering inside objects is not currently normalized across impls — Python emits alphabetical (`json.dumps(sort_keys=True)`), TypeScript and C# preserve schema insertion order. Cross-impl byte-equality is not a v1.0 guarantee; load-then-resolve equality is. |
| `SaveAsCmd` | `(path: string) -> void` | Set `filePath = path`, then `SaveCmd()`. |
| `SolveCmd` | `() -> void` | Re-run `solve` + the two analyses; update `candidates`, `criticalDecisions`, `criticalConstraints`, `status`. Always enabled when `scenario` is defined. |

`SolveCmd` is **implicitly invoked** by every mutating operation on children (edit a property, add an alternative, change config, etc.), via property-change subscriptions. Explicit `SolveCmd` is for the View's "Re-solve" button (M4).

### 3.3 Solve trigger semantics

The view-model layer MUST re-solve when any of these change:

- `scenario.decisions` (add / remove / rename — though rename doesn't change scores)
- `scenario.alternatives` (add / remove / rename / change decisionId)
- `scenario.properties` (add / remove / kind / weight)
- `scenario.coefficients` (any value change)
- `scenario.constraints` (any addition / removal / value change)
- `scenario.config` (aggregation mode or weights)

It MUST NOT re-solve when:

- `scenario.name` or `description` change.
- `filePath` changes (Save As without edits).

Implementation guidance for v1.0: re-solve **synchronously** on each mutation (see the v1.0 status note at the top of `spec/editors.md`). At SAS/EDS scale (≤ 25 alternatives × 7 properties) a single solve is < 10 ms and below human perception, so the adapter-level 100 ms debounce design from earlier drafts was not necessary at ship time. If a future scenario size makes synchronous resolve perceptible, debounce lands in the View adapter (not the VM) per the original guidance — the contract here is that the VM exposes a deterministic re-solve trigger; whether the adapter chooses to coalesce calls is the adapter's policy.

## 4. Per-child VMs (briefly)

### 4.1 `DecisionVM`

- Wraps a `DecisionM`.
- Observable: `id` (read-only), `name` (read-write).
- Mutating `name` updates the underlying `ScenarioM.decisions[i].name`. Does not trigger a solve.

### 4.2 `AlternativeVM`

- Wraps an `AlternativeM`.
- Observable: `id` (read-only), `decisionId` (read-write — moves the alt between decisions), `name` (read-write).
- Changing `decisionId` triggers a solve.

### 4.3 `PropertyVM`

- Wraps a `PropertyM`.
- Observable: `id` (read-only), `name` (read-write), `kind` (read-write `'min' | 'max'`), `weight` (read-write `> 0`).
- Changing `kind` or `weight` triggers a solve.

### 4.4 `CoefficientsVM`

A 2-D grid (`alternative × property`). Implementation: a flat list `CoefficientCellVM[]`, indexed by `(alternativeId, propertyId)`. Each cell is a `ComponentVM<CoefficientM>` exposing `lower`, `modal`, `upper` as read-write doubles. Editing any cell triggers a solve.

### 4.5 Constraint VMs

Three flavors, each `ComponentVM<XxxConstraint>`. Edits trigger a solve.

### 4.6 Result VMs

`CandidateVM`, `CriticalDecisionVM`, `CriticalConstraintVM` are **read-only** wrappers — their constructors take the spec-shaped output of M1's `Solver`. No commands. No mutation paths.

## 5. Adapters (per language)

### 5.1 Python (NiceGUI)

`langs/python/src/guidearch/view/adapters/vmx_to_nicegui.py` exposes:

- `bind(vm: ComponentVMOf[M], property: str, ui_element)` — sets up a two-way binding using NiceGUI's `.bind_value()` and the VMx hub's `PropertyChangedMessage` stream.
- `bind_command(cmd: RelayCommand, button: ui.button)` — invokes `cmd` on click; disables button when `cmd.can_execute` is false.

Size: ~140 LOC including type hints and docstrings (count grew through M3-M4 as cascade and command bindings landed). Tested with a hand-written demo VM in `tests/unit/test_vmx_to_nicegui_adapter.py`.

### 5.2 TypeScript (Svelte)

`langs/typescript/src/view/adapters/vmx-to-svelte.ts` exposes:

- `vmxToStore<T>(vm, propName): Readable<T>` — wraps a VMx property as a Svelte store; subscribes to `PropertyChangedMessage` from the hub.
- `commandAction(cmd: RelayCommand): { onClick: () => void; disabled: Readable<boolean> }` — for use with Svelte's `use:` action or directly on buttons.

Size: ~165 LOC including types and JSDoc (count grew through M3-M4). Tested in `tests/unit/vmx-to-svelte.test.ts`.

### 5.3 C# (Avalonia)

No adapter needed. Avalonia binds to `INotifyPropertyChanged` and `ICommand` natively, which is exactly what VMx-C# emits. The `View/` namespace contains XAML + minimal code-behind only.

## 6. M2 skeleton UI

Each impl ships a minimal app at M2 that demonstrates the VM tree end-to-end:

1. App opens with no scenario loaded; shows an "Open scenario..." button.
2. User picks a `.json` scenario file (via OS-native dialog).
3. `ScenarioVM.OpenCmd(path)` runs.
4. App shows a single table: the ranked candidates (top 50 rows) with columns: rank, score, alternativeIds.
5. Status bar shows scenario name + candidate count.

Editors (M3), charts (M4), and full multi-pane layout (M4) are out of scope for M2.

## 7. Conformance

The M2 conformance gate is structural, not numerical:

- Each impl ships unit tests proving the VM tree exists with the right names, properties, and commands.
- Each impl ships an integration test that loads `sas.json`, calls `OpenCmd`, observes `candidates` updating, and asserts `candidates[0].score` matches the M1 expected (`spec/conformance/expected/sas.candidates.json` rank 0 score). This guards against VM-layer bugs corrupting the algorithm output.

The CI `conformance` workflow remains M1-numerical-only. M2 unit tests are run by each language's per-impl workflow (`typescript.yml`, `csharp.yml`, `python.yml`).
