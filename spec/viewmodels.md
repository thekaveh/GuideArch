# ViewModel layer — formal specification

**Status:** Authoritative. All three impls must realize this tree with the names and shapes below. M0 through M5 all shipped in `v1.0.0`; "M2 skeleton UI", "M3 lets …", "M4 gives …" milestone-tense passages below describe the milestone scope as authored, not in-progress work.

## 1. Purpose

The ViewModel layer wraps the M1 Models in **observable** types that the View can data-bind to. It introduces no domain logic — every algorithmic call goes through the `Models` and `Topsis` modules unchanged. Its single job is to translate "user edited a property weight" into "rerun TOPSIS and notify subscribers that `RankedCandidates` changed."

VMs are built on **VMx** (the submodule at `vendor/vmx/`). Per the project ADRs:

- C# uses `ComponentVM<M>` with the builder pattern (sealed; cannot subclass — VMs are factory-built).
- Python uses `ComponentVMOf[M]` with `.builder()`.
- TypeScript uses `ComponentVMOf<M>` with `.builder()`.

All require a `MessageHub` and `Dispatcher` from VMx. M0 used the null variants for the smoke samples. At v1.0 ship, TypeScript and Python use a live `MessageHub` so adapter code can subscribe to `PropertyChangedMessage`; C# still constructs its `ComponentVM<ScenarioState>` with `NullMessageHub.Instance` (equalising the C# impl with the other two is on the v1.1 backlog).

## 2. The VM tree

> **v1.0 status.** The leaf VMs (`DecisionVM`, `AlternativeVM`, `PropertyVM`,
> `CandidateVM`, `CriticalDecisionVM`, `CriticalConstraintVM`, the three
> `*ConstraintVM`s, `CoefficientCellVM`, `AppVM`, `ScenarioVM`) ship in all three
> impls. The intermediate **composite/aggregate** nodes drawn in the tree
> (`DecisionsVM`, `PropertiesVM`, `AlternativesVM`, `CoefficientsVM`,
> `ConstraintsVM (AggregateVM3)`, the three `*ConstraintsVM`s, `CandidatesVM`,
> `CriticalDecisionsVM`, `CriticalConstraintsVM`, `AnalysisVM (AggregateVM2)`)
> are **aspirational** — the v1.0 UIs read directly from
> `ScenarioVM.model.scenario.*` and from `ScenarioVM`'s observable lists.
> Landing real composite wrappers is on the v1.1 backlog.

The conceptual tree is the same in every impl. Names below are the canonical names (each language adapts case: `ScenarioVM` in C#, `scenario_vm.py` filename + `ScenarioVM` class in Python, `scenario-vm.ts` filename + `ScenarioVM` exported type in TypeScript). The TypeScript filename convention is kebab-case to match the rest of the TS source tree, not camelCase as earlier drafts of this spec sketched.

```
AppVM (root — app-shell concerns)
└── ScenarioVM (per-scenario state)
    ├── DecisionsVM             (CompositeVM<DecisionVM>)
    │   └── DecisionVM (×N)     (ComponentVMOf<DecisionM>)
    │       └── AlternativesVM  (CompositeVM<AlternativeVM>)
    │           └── AlternativeVM (×N) (ComponentVMOf<AlternativeM>)
    ├── PropertiesVM            (CompositeVM<PropertyVM>)
    │   └── PropertyVM (×N)     (ComponentVMOf<PropertyM>)
    ├── CoefficientsVM          (specialized — see §5.4)
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

## 3. AppVM — root

The root VM owns concerns that live above any single scenario: the active theme, the runtime mode, and a child `ScenarioVM`. Every View binds to `AppVM` and navigates down to `AppVM.scenario` for scenario-specific state.

### 3.1 Observable properties

| Property | Type | Description |
|---|---|---|
| `theme` | `string` | Currently active theme. `"dark"` and `"light"` are mandated cross-impl; impls may extend the registered set with framework-specific themes at startup (before the first `AppVM` is constructed). Defaults to `"dark"` on fresh launch. |
| `mode` | `"web" \| "native" \| "tauri"` | Runtime mode. Set at construction and **immutable** on the public surface — there is no `setMode` command. Per impl: TypeScript detects from `window.__TAURI__`; C# defaults to `"native"` (Avalonia desktop); Python defaults to `"native"` when launched with `--native`, else `"web"`. |
| `scenario` | `ScenarioVM` | Child VM. The reference is stable across the AppVM's lifetime — only its internal state mutates. |
| `warnings` | `readonly string[]` | Append-only log of soft failures (e.g. `setTheme` called with an unknown name). Persistence errors are not recorded here; the in-memory state is always authoritative. |

### 3.2 Commands

| Command | Shape | Behavior |
|---|---|---|
| `setTheme` / `setThemeCmd` | `(name: string) -> void` | Validates `name` against the registered theme set. An unknown name appends `"Unknown theme: {name}"` to `warnings` and leaves state unchanged — it never throws, so a theme picker can feed it arbitrary user input. A known name replaces `theme` and writes through the persistence layer. |

### 3.3 Theme persistence

Each impl round-trips the active theme through its native preferences location, with an atomic write (write to a `.tmp` sibling, then rename). A missing, unreadable, or unknown persisted value silently falls back to `"dark"` and the next successful `setTheme` rewrites the store.

| Impl | Path |
|---|---|
| TypeScript | `localStorage["guidearch.theme"]` |
| C# | `{LocalApplicationData}/GuideArch/settings.json` (JSON `{ "theme": "<name>" }`) |
| Python | `platformdirs.user_config_dir("guidearch")/settings.json` (same JSON shape) |

### 3.4 Conformance requirements

Every impl ships at least the five mandatory unit tests below (Python adds a sixth on the read-only `mode` property):

1. Default theme = `"dark"` when persistence is empty.
2. Theme round-trips through the persistence layer (write via one `AppVM`, read by a fresh one).
3. Unknown theme is non-fatal — appends a warning, leaves the active theme and persistence untouched.
4. `PropertyChanged` (TS: hub `PropertyChangedMessage`; C#: `INotifyPropertyChanged`; Python: `property_changed` Subject) fires when the theme changes.
5. `mode` is immutable on the public surface (no `setMode` command, no public setter).

Plus a sixth probe across all impls: `AppVM.scenario` is a stable child reference (the same `ScenarioVM` instance before and after any `setTheme` call).

## 4. ScenarioVM

The root VM owns the loaded scenario and brokers re-solve when its children change.

### 4.1 Observable properties

| Property | Type | Description |
|---|---|---|
| `scenario` | `ScenarioM \| undefined` | The currently loaded scenario, or `undefined` if no file is open. |
| `filePath` | `string \| undefined` | Path of the currently open file, or `undefined` for an unsaved new scenario. |
| `isDirty` | `boolean` | `true` if the model has changed since the last save. |
| `candidates` | `readonly CandidateM[]` | Latest `solve(scenario)` output. Empty array if scenario undefined. |
| `criticalDecisions` | `readonly CriticalDecisionM[]` | Latest `criticalDecisions(scenario)` output. |
| `criticalConstraints` | `readonly CriticalConstraintM[]` | Latest `criticalConstraints(scenario)` output. |
| `status` | `string` | Human-readable status line for the View (e.g. `"Solved: 720 candidates"` after loading SAS). |
| `warnings` | `readonly string[]` | Non-fatal warnings from scenario load. |
| `selectedCandidateIndex` | `int \| null` | Index into `candidates` of the currently selected candidate, or `null` when nothing is selected. The candidates table, Chart A, Chart B, and Chart C all read it; editing it from any of the four updates the others. Defined formally in `spec/charts.md` §7 — added here for completeness. |

### 4.2 Commands

| Command | Shape | Behavior |
|---|---|---|
| `NewCmd` | `() -> void` | Replace `scenario` with a fresh empty `ScenarioM`. Clear `filePath`. Set `isDirty = false`. Re-solve (will be empty). |
| `OpenCmd` | `(path: string) -> void` | Call `loadScenario(path)`. On success: set `scenario`, `filePath`, clear `isDirty`, re-solve. On failure: do not mutate the *scenario state* (`scenario` / `filePath` / `isDirty` stay as they were), but DO set `status` to `"Open failed: {error}"` and append the same message to `warnings` so the user gets a visible signal in both the status bar and the warnings tray. |
| `SaveCmd` | `() -> void` | Write current `scenario` to `filePath` as JSON via the impl's scenario serializer. Output must be `JSON.stringify`-compatible (any compliant JSON reader can round-trip it). Clear `isDirty`. Disabled (`canExecute = false`) if `filePath` undefined. **Note:** key ordering inside objects is not currently normalized across impls — Python emits alphabetical (`json.dumps(sort_keys=True)`), TypeScript and C# preserve schema insertion order. Cross-impl byte-equality is not a v1.0 guarantee; load-then-resolve equality is. |
| `SaveAsCmd` | `(path: string) -> void` | Write `scenario` to `path` using the same serializer as `SaveCmd`. On success: set `filePath = path` and clear `isDirty`. On failure: set `status` + append to `warnings` (`"Save failed: {error}"`) and leave `filePath` UNCHANGED so the next `SaveCmd` retries against the last known-good destination, not the bad path. |
| `SolveCmd` | `() -> void` | Re-run `solve` + the two analyses; update `candidates`, `criticalDecisions`, `criticalConstraints`, `status`. Always enabled when `scenario` is defined. |

`SolveCmd` is **implicitly invoked** by every mutating operation on children (edit a property, add an alternative, change config, etc.), via property-change subscriptions. Explicit `SolveCmd` is for the View's "Re-solve" button (M4).

### 4.3 Solve trigger semantics

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

## 5. Per-child VMs (briefly)

### 5.1 `DecisionVM`

- Wraps a `DecisionM`.
- Observable: `id` (read-only), `name` (read-write).
- Mutating `name` updates the underlying `ScenarioM.decisions[i].name`. Does not trigger a solve.

### 5.2 `AlternativeVM`

- Wraps an `AlternativeM`.
- Observable: `id` (read-only), `decisionId` (read-write — moves the alt between decisions), `name` (read-write).
- Changing `decisionId` triggers a solve.

### 5.3 `PropertyVM`

- Wraps a `PropertyM`.
- Observable: `id` (read-only), `name` (read-write), `kind` (read-write `'min' | 'max'`), `weight` (read-write `> 0`).
- Changing `kind` or `weight` triggers a solve.
- **Mutator boundary:** the `weight > 0` invariant is enforced on **both**
  the Add and Update paths in every impl (`addProperty`/`add_property`/
  `AddProperty` and `updateProperty`/`update_property`/`UpdateProperty`).
  A caller passing `weight <= 0` to any of these gets `ScenarioMutationError`
  (TS+Py) or `ScenarioMutationException` (C#) at the mutation boundary —
  not silently accepted to fail later at save-time schema validation. The
  add-side surface in all three impls takes the same three optional
  parameters: `name`, `kind`, `weight`.
- The same boundary rejects non-finite values (`NaN`, `+Infinity`,
  `-Infinity`) on `weight`. The literal `>0` predicate alone is not
  sufficient: in all three target languages `NaN <= 0` evaluates to
  false (all NaN comparisons are), so a bare `> 0` check lets `NaN`
  through and poisons every downstream score with a `Solved` status.
  Implementations use `Number.isFinite` (TS), `math.isfinite` (Py), and
  `double.IsFinite` (C#) before the `> 0` check.

### 5.4 `CoefficientsVM`

A 2-D grid (`alternative × property`). Implementation: a flat list `CoefficientCellVM[]`, indexed by `(alternativeId, propertyId)`. Each cell is a `ComponentVM<CoefficientM>` exposing `lower`, `modal`, `upper` as read-write doubles. Editing any cell triggers a solve.

- **Mutator boundary:** `updateCoefficient`/`update_coefficient`/
  `UpdateCoefficient` rejects non-finite components (`NaN`, `+Infinity`,
  `-Infinity`) on any of `lower`, `modal`, `upper`. JSON cannot encode
  these values, so accepting them solves "successfully" into NaN scores
  and then fails at save-time with a schema error that points at the
  wrong place. Triangular ordering (`lower <= modal <= upper`) remains a
  *warning*, not a fatal error — that is invariant 4.1 and the loader
  also treats it as a warning. Finiteness is fatal.

### 5.5 Constraint VMs

Three flavors, each `ComponentVM<XxxConstraint>`. Edits trigger a solve.

**Mutator surface and indexing.** Mutators on `ScenarioVM` that target an
existing constraint take a **global** index into `scenario.constraints` — the
same index space used by `CriticalConstraintM.constraintIndex` in
`spec/algorithms/critical-constraints.md`. Two equivalent surfaces are
permitted:

- **Typed surface (Python, C#).** Per-kind add/update method triplets that
  take the constraint fields as positional/keyword arguments —
  `add_threshold_constraint(property_id, min, max)` /
  `update_threshold_constraint(index, …)` and the dependency/conflict
  equivalents — plus the kind-agnostic `delete_constraint(index)`. The
  `update_*_constraint` methods assert the constraint at `index` is of the
  expected kind and raise `ScenarioMutationError` (Python) /
  `ScenarioMutationException` (C#) otherwise.
- **Generic surface (TypeScript).** `addConstraint(c: ConstraintM)` /
  `updateConstraint(index, c)` / `deleteConstraint(index)` — the runtime
  type of `c` carries the kind.

Both surfaces target the same global index space. Per-kind sub-indexing
(e.g. "the third threshold constraint") is **not** a supported addressing
mode — views that want to render a single-kind sub-tab should derive the
global index from the constraint reference, not pass a filtered-list offset.

### 5.6 Result VMs

`CandidateVM`, `CriticalDecisionVM`, `CriticalConstraintVM` are **read-only** wrappers — their constructors take the spec-shaped output of M1's `Solver`. No commands. No mutation paths.

## 6. Adapters (per language)

### 6.1 Python (NiceGUI)

`langs/python/src/guidearch/view/adapters/vmx_to_nicegui.py` exposes:

- `bind(vm: ComponentVMOf[M], property: str, ui_element)` — sets up a two-way binding using NiceGUI's `.bind_value()` and the VMx hub's `PropertyChangedMessage` stream.
- `bind_command(cmd: RelayCommand, button: ui.button)` — invokes `cmd` on click; disables button when `cmd.can_execute` is false.

Size: ~140 LOC including type hints and docstrings (count grew through M3-M4 as cascade and command bindings landed). Tested with a hand-written demo VM in `tests/unit/test_vmx_to_nicegui_adapter.py`. The shipped `main.py` v1.0 UI does not currently route through this adapter — it wires NiceGUI elements to VM properties through ad-hoc `vm.subscribe(...)` callbacks. Converging `main.py` onto `bind` / `bind_command` is on the v1.1 backlog; the adapter remains the canonical pattern.

### 6.2 TypeScript (Svelte)

`langs/typescript/src/view/adapters/vmx-to-svelte.ts` exposes:

- `vmxToStore<M, K extends keyof M>(vm, propName): Readable<M[K]>` — wraps a single VMx property as a Svelte store; subscribes to `PropertyChangedMessage` from the hub.
- `vmxStoreAll<M>(vm): Readable<M>` — same shape but returns the whole model for any property change.
- `commandToButton(cmd: RelayCommand): { onClick: () => void; disabled: Readable<boolean> }` — wrap a parameterless command for direct use on a `<button>`.
- `commandOfToButton<T>(cmd: RelayCommandOf<T>, parameter): { onClick; disabled }` — the same for `RelayCommandOf<T>` with the parameter bound at call site.

Size: ~165 LOC including types and JSDoc (count grew through M3-M4). Tested in `tests/unit/vmx-to-svelte.test.ts`.

### 6.3 C# (Avalonia)

No adapter needed. Avalonia binds to `INotifyPropertyChanged` and `ICommand` natively, which is exactly what VMx-C# emits. The `View/` namespace contains XAML + minimal code-behind only.

## 7. M2 skeleton UI

Each impl ships a minimal app at M2 that demonstrates the VM tree end-to-end:

1. App opens with no scenario loaded; shows an "Open scenario..." button.
2. User picks a `.json` scenario file (via OS-native dialog).
3. `ScenarioVM.OpenCmd(path)` runs.
4. App shows a single table: the ranked candidates (top 50 rows) with columns: rank, score, alternativeIds. The right-rail ranked-bar **Chart A** alongside the table is hard-fixed to top 30 (see `spec/charts.md` §2) — the table window and the chart window are deliberately different sizes.
5. Status bar shows scenario name + candidate count.

Editors (M3), charts (M4), and full multi-pane layout (M4) are out of scope for M2.

## 8. Conformance

The M2 conformance gate is structural, not numerical:

- Each impl ships unit tests proving the VM tree exists with the right names, properties, and commands.
- Each impl ships an integration test that loads `sas.json`, calls `OpenCmd`, observes `candidates` updating, and asserts `candidates[0].score` matches the M1 expected (`spec/conformance/expected/sas.candidates.json` rank 0 score). This guards against VM-layer bugs corrupting the algorithm output.

The CI `conformance` workflow remains M1-numerical-only. M2 unit tests are run by each language's per-impl workflow (`typescript.yml`, `csharp.yml`, `python.yml`).
