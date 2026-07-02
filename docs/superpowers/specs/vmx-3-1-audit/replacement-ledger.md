# VMx 3.1 Replacement Ledger

## LOC Accounting Rules

- Baseline counts come from [loc-baseline.txt](/Users/kaveh/repos/GuideArch/docs/superpowers/specs/vmx-3-1-audit/loc-baseline.txt).
- ViewModel production, view/adapter production, and test LOC stay separated exactly as they were counted in that baseline artifact.
- `deleted production LOC` and `added production LOC` are conservative slice estimates for the named pattern only; they are not whole-file rewrite claims.
- `net production LOC` is `deleted - added`. Negative values mean the VMx 3.1 candidate likely adds scaffolding before later cleanups pay it back.
- `test LOC delta` is the expected net test movement needed to preserve or improve behavior coverage after the replacement.
- Docs, generated assets, lockfiles, caches, and formatting-only churn remain out of scope for these savings numbers.

## Summary

| Area | Baseline LOC | Projected replacement LOC | Net saved | Confidence |
| --- | ---: | ---: | ---: | --- |
| ViewModel production | 5436 | 4616 | 820 | medium |
| View/adapter production | 9109 | 8884 | 225 | low |
| Total production | 14545 | 13500 | 1045 | medium |
| Tests | 10241 | 10571 | -330 | medium |

Tests show negative `net saved` because the replacement path is expected to add parity coverage around forms, observable collections, and selection behavior.

## Ledger

| ID | Current pattern | Replacement decision | Languages | Files | Deleted production LOC | Added production LOC | Net production LOC | Test LOC delta | Behavior coverage | Risk |
| --- | --- | --- | --- | --- | ---: | ---: | ---: | ---: | --- | --- |
| R1 | Root `ScenarioVM` orchestration | partial | Python, TypeScript, C# | `langs/python/src/guidearch/viewmodels/scenario_vm.py`; `langs/typescript/src/viewmodels/scenario-vm.ts`; `langs/csharp/src/GuideArch.ViewModels/ScenarioVMFactory.cs` | 260 | 120 | 140 | 45 | root lifecycle, solve entrypoints, child ownership | high |
| R2 | Leaf VM factory/class repetition | replace | Python, TypeScript, C# | `alternative_vm.py`, `decision_vm.py`, `property_vm.py`, `coefficient_vm.py`, `constraint_vm.py`; `alternative-vm.ts`, `decision-vm.ts`, `property-vm.ts`, `coefficient-vm.ts`, `constraint-vm.ts`; `AlternativeVMFactory.cs`, `DecisionVMFactory.cs`, `PropertyVMFactory.cs`, `CoefficientVMFactory.cs`, `ConstraintVMFactory.cs` | 210 | 70 | 140 | 24 | editable leaf construction and naming | medium |
| R3 | Candidate/result readonly wrappers | replace | Python, TypeScript, C# | `candidate_vm.py`, `critical_decision_vm.py`, `critical_constraint_vm.py`; `candidate-vm.ts`, `critical-decision-vm.ts`, `critical-constraint-vm.ts`; `CandidateVMFactory.cs`, `CriticalDecisionVMFactory.cs`, `CriticalConstraintVMFactory.cs` | 160 | 40 | 120 | 18 | readonly result projections | low |
| R4 | Coefficient grid indexing and updates | partial | Python, TypeScript, C# | `scenario_vm.py`; `main.py`; `scenario-vm.ts`; `routes/lib/CoefficientsTab.svelte`; `ScenarioState.cs`; `MainWindow.axaml.cs` | 145 | 100 | 45 | 36 | coefficient matrix projection and update flow | high |
| R5 | Constraint collections and per-kind views | partial | Python, TypeScript, C# | `scenario_vm.py`; `main.py`; `scenario-vm.ts`; `routes/lib/ConstraintsTab.svelte`; `routes/lib/CriticalConstraintsTab.svelte`; `ScenarioState.cs`; `MainWindow.axaml.cs` | 90 | 70 | 20 | 18 | threshold/dependency/conflict slicing | medium |
| R6 | Dirty tracking and re-solve triggers | replace | Python, TypeScript, C# | `scenario_vm.py`; `scenario-vm.ts`; `ScenarioVMFactory.cs`; `ScenarioState.cs` | 250 | 70 | 180 | 42 | dirty state, save eligibility, solve trigger matrix | high |
| R7 | Property/value change subscriptions | partial | Python, TypeScript, C# | `app_vm.py`; `scenario_vm.py`; `view/adapters/vmx_to_nicegui.py`; `main.py`; `app-vm.ts`; `view/adapters/vmx-to-svelte.ts`; `routes/+page.svelte`; `StatusBar.svelte`; `Toolbar.svelte`; `AppVMFactory.cs`; `MainWindow.axaml.cs` | 210 | 90 | 120 | 33 | property notifications, subscription fan-out, UI refresh hooks | high |
| R8 | Add/update/delete command surfaces | replace | Python, TypeScript, C# | `scenario_vm.py`; `scenario-vm.ts`; `ScenarioVMFactory.cs` | 185 | 70 | 115 | 30 | CRUD entrypoints and cascades | medium |
| R9 | Save/open/new command workflows | partial | Python, TypeScript, C# | `scenario_vm.py`; `main.py`; `scenario-vm.ts`; `routes/lib/Toolbar.svelte`; `ScenarioVMFactory.cs`; `MainWindow.axaml.cs` | 140 | 85 | 55 | 24 | file lifecycle, sample loading, failure surfacing | medium |
| R10 | Dialog and confirmation handling | partial | Python, TypeScript, C# | `main.py`; `routes/lib/ConfirmDialog.svelte`; `routes/lib/confirm-dialog.ts`; `MainWindow.axaml.cs` | 60 | 80 | -20 | 18 | destructive confirmations and dialog routing | medium |
| R11 | Candidate selection state | replace | Python, TypeScript, C# | `scenario_vm.py`; `main.py`; `scenario-vm.ts`; `routes/lib/ResultsTab.svelte`; `ScenarioVMFactory.cs`; `MainWindow.axaml.cs` | 70 | 25 | 45 | 12 | selected candidate tracking and defaulting | low |
| R12 | View adapter/store glue | partial | Python, TypeScript, C# | `langs/python/src/guidearch/view/adapters/vmx_to_nicegui.py`; `langs/python/src/guidearch/main.py`; `langs/typescript/src/view/adapters/vmx-to-svelte.ts`; `langs/typescript/src/routes/+page.svelte`; `langs/typescript/src/routes/lib/ResultsTab.svelte`; `langs/typescript/src/routes/lib/StatusBar.svelte`; `langs/typescript/src/routes/lib/Toolbar.svelte`; `langs/csharp/src/GuideArch.View/MainWindow.axaml`; `langs/csharp/src/GuideArch.View/MainWindow.axaml.cs` | 180 | 80 | 100 | 21 | adapter/store contracts and broad rerender glue | high |
| R13 | `AppVM` theme persistence and warnings | partial | Python, TypeScript, C# | `langs/python/src/guidearch/viewmodels/app_vm.py`; `langs/typescript/src/viewmodels/app-vm.ts`; `langs/csharp/src/GuideArch.ViewModels/AppVMFactory.cs` | 55 | 70 | -15 | 9 | theme persistence, warnings, stable child reference | low |
| R14 | Conceptual VM tree composites/aggregates | defer | Python, TypeScript, C# | `langs/python/src/guidearch/viewmodels/__init__.py`; `langs/typescript/src/viewmodels/index.ts`; `langs/csharp/src/GuideArch.ViewModels/ScenarioState.cs` | 0 | 0 | 0 | 0 | future tree ownership abstractions only | low |

## Notes By Replacement

### R1

- Decision: `partial`.
- Rationale: `FormVM` can absorb lifecycle scaffolding, but GuideArch still needs custom solve orchestration, scenario serialization boundaries, and domain-specific mutators.
- Migration sketch: wrap scenario state in a `FormVM`/builder-backed root, keep `RelayCommand` for solve and file entrypoints, and move child ownership behind the form state rather than a bespoke mutable box.
- Test strategy: keep the existing scenario root and VM-tree suites passing, then add explicit form-lifecycle tests before trimming bespoke orchestration code.

### R2

- Decision: `replace`.
- Rationale: the editable leaf VMs are mostly naming plus a small set of modeled property hooks, which maps cleanly onto shared builder helpers in all three languages.
- Migration sketch: introduce one cross-language helper per editable leaf family, keep file-local names for readability, and collapse repeated service wiring and constructor boilerplate.
- Test strategy: rewrite VM-tree assertions to check the shared helper output and keep cascade/integration suites as the behavior gate.

### R3

- Decision: `replace`.
- Rationale: readonly result wrappers are a direct fit for `ReadonlyComponentVMOf`/`ReadonlyComponentVMBuilder`, and they already avoid command or hub ownership.
- Migration sketch: convert candidate and criticality wrappers first, keep their public property names stable, then delete the bespoke readonly shells.
- Test strategy: keep result-readonly assertions from the comprehensive VM-tree suites and add one focused parity file per language for readonly result construction.

### R4

- Decision: `partial`.
- Rationale: `ObservableList` can remove a chunk of manual grid refresh plumbing, but the coefficient matrix still has GuideArch-specific grouping and fuzzy-value editing semantics.
- Migration sketch: represent rows/cells as observable collections first, preserve the current grouping API, then trim index rebuilding in the views once list diffs are available.
- Test strategy: keep coefficient edit and VM-MVVM tests green, then add observable-collection tests that assert row/cell diffs instead of whole-scenario rerenders.

### R5

- Decision: `partial`.
- Rationale: per-kind constraint slices fit `DerivedProperty` today; `FilteredCompositeVM` is better treated as a second-wave candidate after collection ownership is normalized.
- Migration sketch: replace ad hoc threshold/dependency/conflict filtering with derived slices while keeping the existing constraint models and tab layouts.
- Test strategy: preserve constraint cascade tests and add one new suite per language for derived constraint collections.

### R6

- Decision: `replace`.
- Rationale: dirty tracking and solve-trigger gating line up directly with `FormVM`, form builders, and property-value change helpers; this is the highest-leverage root cleanup.
- Migration sketch: move dirty/save eligibility into the form layer, encode solve-trigger inclusion/exclusion once, and let root commands depend on form state instead of bespoke booleans.
- Test strategy: keep solve-trigger matrix coverage, add dedicated form dirty/approve/deny tests, and only then delete the current manual dirty-state paths.

### R7

- Decision: `partial`.
- Rationale: VMx 3.1 gives better reactive primitives, but each UI still needs a framework-native last-mile bridge and some state fan-out remains view-owned.
- Migration sketch: convert warning/status/selection subscriptions to derived or observable VMx surfaces first, then shrink adapter code to translation rather than orchestration.
- Test strategy: rewrite adapter/store tests around the new event surfaces and keep integration tests that prove theme, status, and results refresh still propagate.

### R8

- Decision: `replace`.
- Rationale: `ModeledCrudCommands` matches GuideArch's repeated add/update/delete flows more closely than the hand-written mutator entrypoints do.
- Migration sketch: introduce CRUD command groups for decisions, alternatives, properties, and constraints while preserving current cascade semantics and error messages.
- Test strategy: keep the existing cascade suites as the source of truth and add one new command-surface suite per language for the CRUD wrappers.

### R9

- Decision: `partial`.
- Rationale: `FormVM` and `AsyncRelayCommand` can simplify save/open/new gating, but platform file pickers and browser/native split points remain custom.
- Migration sketch: move enablement and dirty/reset policy into VMx commands first, then adapt the NiceGUI, Svelte, and Avalonia shells around the slimmer workflow surface.
- Test strategy: keep open/save roundtrip and failure-path tests passing and add form-workflow tests that assert approve/discard sequencing.

### R10

- Decision: `partial`.
- Rationale: confirmation decorators and dialog services improve consistency, but each frontend still needs platform presentation code; the first pass likely adds a thin abstraction layer.
- Migration sketch: lift destructive-confirm policy into VMx command decorators, leave branded dialog rendering in the view layer until a second pass proves the abstraction boundary.
- Test strategy: rewrite dialog tests to assert service invocation and confirm/cancel outcomes instead of scanning current helper implementations.

### R11

- Decision: `replace`.
- Rationale: the selected-candidate workflow maps well to VMx selection capabilities and does not require GuideArch-specific infrastructure beyond rank validation.
- Migration sketch: replace manual selected-index state with selectable behavior on the candidate surface, then update results views to bind to selection rather than raw index mutation.
- Test strategy: keep selection-state suites passing and add observable-selection tests where the result tabs or charts currently depend on raw integer state.

### R12

- Decision: `partial`.
- Rationale: adapter/store glue is one of the biggest maintenance hotspots, but it can only shrink after roots expose cleaner list, form, and derived-state surfaces.
- Migration sketch: first swap broad whole-model rebroadcasts for granular list and derived-property updates, then delete per-view refresh plumbing that becomes redundant.
- Test strategy: keep adapter/store integration tests until the old bridges are gone, and add replacement suites that assert the new wrappers over forms and observable lists.

### R13

- Decision: `partial`.
- Rationale: `AppVM` is already smaller than `ScenarioVM`; theme persistence stays partly bespoke even after `DerivedProperty` and builder cleanup are adopted.
- Migration sketch: keep persistence I/O local, move warnings/theme projections onto derived state, and preserve the stable child-scenario reference that all three implementations already test.
- Test strategy: rewrite the property-change assertions for the new AppVM surface, but keep the persistence and stable-child behaviors unchanged.

### R14

- Decision: `defer`.
- Rationale: `CompositeVM`, `CompositeVMOf`, and the aggregate family are plausible for a later tree-shape refactor, but they are not needed to land the high-value 3.1 replacements above.
- Migration sketch: revisit once forms, CRUD commands, derived properties, and observable collections are already in place and the remaining tree pain points are easier to see.
- Test strategy: keep current VM-tree suites as regression coverage and avoid adding speculative composite tests until a concrete design exists.
