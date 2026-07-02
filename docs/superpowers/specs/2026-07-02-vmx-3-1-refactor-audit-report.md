# VMx 3.1 Refactor Audit Report

**Date:** 2026-07-02  
**Branch:** `codex/vmx-3-1-refactor-audit`  
**VMx Python package:** `vmx==3.1.0`  
**VMx source commit for TypeScript/C#:** `a77b25aa28078eb3d56fba011d4fe32e9a2a4d12`

Primary evidence for this report:

- [baseline.md](vmx-3-1-audit/baseline.md)
- [loc-baseline.txt](vmx-3-1-audit/loc-baseline.txt)
- [current-usage.md](vmx-3-1-audit/current-usage.md)
- [vmx-3-1-capabilities.md](vmx-3-1-audit/vmx-3-1-capabilities.md)
- [replacement-ledger.md](vmx-3-1-audit/replacement-ledger.md)
- [test-impact.md](vmx-3-1-audit/test-impact.md)

## Executive Summary

GuideArch already uses VMx deeply across Python, TypeScript, and C#, but the current shape is uneven: Python has the most bespoke root and adapter code, TypeScript stays closest to VMx primitives while still carrying a broad Svelte bridge, and C# is structurally aligned with VMx but pays for that alignment in view-layer binding and code-behind glue. The strongest VMx 3.1 replacement candidates are `FormVM` and form builders, `ModeledCrudCommands`, `ObservableList`, `DerivedProperty`, readonly modeled components, and selection capabilities. The largest risks remain root scenario orchestration, property/subscription fan-out, and adapter/store glue rather than the thin leaf VMs.

The replacement ledger projects a net production reduction of **1,045 LOC**: **820 LOC** from ViewModel production files and **225 LOC** from view/adapter production files. Projected test impact is **+330 LOC**, reflecting added parity coverage around forms, observable collections, and selection behavior rather than test deletion. Confidence is **medium** for ViewModel and total production savings, **low** for view/adapter savings, and **medium** for the test delta. See [replacement-ledger.md](vmx-3-1-audit/replacement-ledger.md) and [loc-baseline.txt](vmx-3-1-audit/loc-baseline.txt).

Migration risk is not uniform. The highest-value cleanup areas (`R1`, `R6`, `R7`, `R12`) are also the highest-risk because they touch scenario lifecycle, dirty tracking, property propagation, and UI integration seams. The report therefore recommends a phased path that lands low-risk mechanical replacements first, then collection and form cleanup, then adapter simplification, with cross-language parity gates at each step. Test-impact counts to preserve are **14/14/13 must-keep suites**, **4/4/4 rewrites**, **3/3/3 additions**, and **1/1/0 removals** for Python, TypeScript, and C# respectively. See [test-impact.md](vmx-3-1-audit/test-impact.md).

## Dependency And Source State

Source-of-truth baseline: [baseline.md](vmx-3-1-audit/baseline.md)

| Area | Evidence |
| --- | --- |
| GuideArch branch | `codex/vmx-3-1-refactor-audit` |
| GuideArch commits | Pre-alignment `a80ee10ba511788db311f466f72134bd3ed6526c`; post-alignment `c0446ba032f8684c0b93eec5d274496a6dfb9b14` |
| Python dependency state | `langs/python/pyproject.toml` pins `vmx==3.1.0`; `langs/python/uv.lock` resolves `3.1.0` |
| Vendor source state | `vendor/vmx` moved from `e2b23f879b262eb03db0e85b861a1324a2379d94` to `a77b25aa28078eb3d56fba011d4fe32e9a2a4d12` |
| TypeScript VMx metadata | `vendor/vmx/langs/typescript/package.json` and `vendor/vmx/langs/typescript/src/version.ts` report `3.1.0` |
| C# VMx metadata | `vendor/vmx/langs/csharp/src/VMx/VMx.csproj` reports `Version=3.1.0` and `MinSpecVersion=3.1.0` |
| Python VMx metadata | `vendor/vmx/langs/python/src/vmx/__about__.py` reports `__version__ = "3.1.0"` and `__min_spec_version__ = "3.1.0"` |

Baseline verification recorded in [baseline.md](vmx-3-1-audit/baseline.md):

- Python smoke import against VMx 3.1.0: `pass`.
- C# test baseline: `pass` (`GuideArch.Models.Tests` 30/30, `GuideArch.ViewModels.Tests` 269/269).
- Vendor TypeScript package verification: `fail` because `pnpm install` exited with `ERR_PNPM_IGNORED_BUILDS` for `esbuild@0.27.7`.
- GuideArch TypeScript baseline: `fail` with 1 failing test in `tests/unit/app-vm.test.ts`; the observed property names were `['model', 'modeledHint']` instead of including `Model`.
- GuideArch Python baseline: initially `fail` during collection because GuideArch still imported `RelayCommandOfT`, which VMx 3.1.0 no longer exports. A post-baseline compatibility fix now uses `RelayCommandOf`; fresh Python verification passes (`268 passed, 1 skipped`).

Those failures matter for planning because they establish that VMx 3.1 compatibility pressure was already visible in Python command imports and remains visible in TypeScript property-changed assertions before any broad refactor starts.

## Current Usage Findings

Source-of-truth inventory: [current-usage.md](vmx-3-1-audit/current-usage.md)

### Cross-language summary

| Language | Primary VMx usage | Largest bespoke areas | Audit takeaway |
| --- | --- | --- | --- |
| Python | Hybrid: leaf editors subclass `ComponentVMOf`, but `ScenarioVM` and `AppVM` hand-roll command wiring, `property_changed`, and `MessageHub` publication. | `langs/python/src/guidearch/viewmodels/scenario_vm.py`, `langs/python/src/guidearch/viewmodels/app_vm.py`, `langs/python/src/guidearch/view/adapters/vmx_to_nicegui.py`, `langs/python/src/guidearch/main.py` | Highest bespoke VMx surface and the earliest 3.1 compatibility break (`RelayCommandOfT`). |
| TypeScript | Broad direct VMx usage with `ComponentVMOf` builders, `RelayCommand` / `RelayCommandOf`, and a Svelte store bridge. | `langs/typescript/src/viewmodels/scenario-vm.ts`, `langs/typescript/src/viewmodels/app-vm.ts`, `langs/typescript/src/view/adapters/vmx-to-svelte.ts`, route components under `langs/typescript/src/routes/lib/` | Closest to VMx primitives, but broad store coupling magnifies changes in property publication semantics. |
| C# | Factory-first `ComponentVM<T>` usage with root factories attaching `RelayCommand` commands and Avalonia views binding through open-generic component VMs. | `langs/csharp/src/GuideArch.ViewModels/ScenarioVMFactory.cs`, `langs/csharp/src/GuideArch.ViewModels/AppVMFactory.cs`, `langs/csharp/src/GuideArch.View/MainWindow.axaml`, `langs/csharp/src/GuideArch.View/MainWindow.axaml.cs` | Structurally aligned with VMx, but the view layer amplifies migration cost. |

Post-baseline Python compatibility note: `langs/python/tests/unit/test_vmx31_command_compat.py` now explicitly guards the VMx 3.1 parameterized command name by asserting `AppVM.set_theme_cmd`, `ScenarioVM.open_cmd`, and `ScenarioVM.save_as_cmd` are `RelayCommandOf` instances. This immediate guard is separate from the larger replacement-test additions projected below.

### Hot spots with the most replacement pressure

- Root scenario orchestration is high pressure in all three languages:
  `scenario_vm.py`, `scenario-vm.ts`, and `ScenarioVMFactory.cs`.
- Leaf VM repetition is medium pressure across the editable and readonly leaf families.
- Collections and filtered views are high pressure because scenario roots recompute candidates, criticality data, and slices manually.
- Commands and confirmation flows are high pressure because add/update/delete, save/open/new, and destructive confirmation behavior remain partly bespoke.
- View bindings and adapters are high pressure in every stack:
  `vmx_to_nicegui.py`, `vmx-to-svelte.ts`, `MainWindow.axaml`, and `MainWindow.axaml.cs`.

The practical reading is that GuideArch does not need a new VM architecture from scratch. It needs the current VMx usage to be normalized around the stronger 3.1 form, command, collection, and derived-state primitives already available across all three implementations.

## VMx 3.1 Capability Findings

Source-of-truth capability inventory: [vmx-3-1-capabilities.md](vmx-3-1-audit/vmx-3-1-capabilities.md)

### Strongest candidates

1. `FormVM` and form builders: strongest fit for dirty tracking, approve/deny flows, validation, and persistence semantics in the Python `ScenarioVM`, TypeScript `scenario-vm.ts`, and C# `ScenarioVMFactory`.
2. `ModeledCrudCommands`: strongest fit for repeated add/update/delete flows around alternatives, decisions, properties, and constraints.
3. `ObservableList`: strongest fit for replacing coarse rerender behavior with granular list mutation signals in NiceGUI, Svelte, and Avalonia.
4. `DerivedProperty`: strongest fit for warnings, dirty flags, status text, selected result projections, and other computed values that are currently republished manually.
5. readonly modeled components and builder conveniences: good fit for result-only wrappers and repeated leaf construction.
6. selection capabilities: strong fit for selected candidate state and other current-selection behaviors that are already explicit in GuideArch.

### Useful but second-wave or weaker candidates

- `CompositeVM`, `CompositeVMOf`, `FilteredCompositeVM`, and `ScoredFilteredCompositeVM` are credible later options, but the capability inventory classifies them as better second-wave candidates after forms, CRUD commands, derived properties, and observable collections are in place.
- `GroupVM` and `AggregateVM1` through `AggregateVM6` are available cross-language but weak fits for the current maintenance burden.
- paging, token paging, and paging capabilities are complete but weak fits because GuideArch data sets are local and small.
- search and filter capabilities are plausible future UX additions, not top migration targets.
- notification services have weaker cross-language parity than the form, command, and collection surfaces.

## Replacement Recommendations

Full decision matrix source: [replacement-ledger.md](vmx-3-1-audit/replacement-ledger.md)

The audit still points to the same highest-value first wave: `R6`, `R1`, `R2`, `R7`, `R3`, `R8`, and `R12` carry the largest positive net production savings, while `R10` and `R13` are smaller partial cleanups and `R14` remains explicitly deferred. For the user-facing report, the full replacement ledger is embedded below so the rationale, scope, and risk can be reviewed without leaving this document.

| ID | Current pattern | Candidate replacement/Decision | Languages | Files | Deleted production LOC | Added production LOC | Net production LOC | Test LOC delta | Behavior coverage | Risk |
| --- | --- | --- | --- | --- | ---: | ---: | ---: | ---: | --- | --- |
| `R1` | Root `ScenarioVM` orchestration | partial | Python, TypeScript, C# | `langs/python/src/guidearch/viewmodels/scenario_vm.py`; `langs/typescript/src/viewmodels/scenario-vm.ts`; `langs/csharp/src/GuideArch.ViewModels/ScenarioVMFactory.cs` | 260 | 120 | 140 | 45 | root lifecycle, solve entrypoints, child ownership | high |
| `R2` | Leaf VM factory/class repetition | replace | Python, TypeScript, C# | `langs/python/src/guidearch/viewmodels/alternative_vm.py`, `decision_vm.py`, `property_vm.py`, `coefficient_vm.py`, `constraint_vm.py`; `langs/typescript/src/viewmodels/alternative-vm.ts`, `decision-vm.ts`, `property-vm.ts`, `coefficient-vm.ts`, `constraint-vm.ts`; `langs/csharp/src/GuideArch.ViewModels/AlternativeVMFactory.cs`, `DecisionVMFactory.cs`, `PropertyVMFactory.cs`, `CoefficientVMFactory.cs`, `ConstraintVMFactory.cs` | 210 | 70 | 140 | 24 | editable leaf construction and naming | medium |
| `R3` | Candidate/result readonly wrappers | replace | Python, TypeScript, C# | `langs/python/src/guidearch/viewmodels/candidate_vm.py`, `critical_decision_vm.py`, `critical_constraint_vm.py`; `langs/typescript/src/viewmodels/candidate-vm.ts`, `critical-decision-vm.ts`, `critical-constraint-vm.ts`; `langs/csharp/src/GuideArch.ViewModels/CandidateVMFactory.cs`, `CriticalDecisionVMFactory.cs`, `CriticalConstraintVMFactory.cs` | 160 | 40 | 120 | 18 | readonly result projections | low |
| `R4` | Coefficient grid indexing and updates | partial | Python, TypeScript, C# | `langs/python/src/guidearch/viewmodels/scenario_vm.py`; `langs/python/src/guidearch/main.py`; `langs/typescript/src/viewmodels/scenario-vm.ts`; `langs/typescript/src/routes/lib/CoefficientsTab.svelte`; `langs/csharp/src/GuideArch.ViewModels/ScenarioState.cs`; `langs/csharp/src/GuideArch.View/MainWindow.axaml.cs` | 145 | 100 | 45 | 36 | coefficient matrix projection and update flow | high |
| `R5` | Constraint collections and per-kind views | partial | Python, TypeScript, C# | `langs/python/src/guidearch/viewmodels/scenario_vm.py`; `langs/python/src/guidearch/main.py`; `langs/typescript/src/viewmodels/scenario-vm.ts`; `langs/typescript/src/routes/lib/ConstraintsTab.svelte`; `langs/typescript/src/routes/lib/CriticalConstraintsTab.svelte`; `langs/csharp/src/GuideArch.ViewModels/ScenarioState.cs`; `langs/csharp/src/GuideArch.View/MainWindow.axaml.cs` | 90 | 70 | 20 | 18 | threshold/dependency/conflict slicing | medium |
| `R6` | Dirty tracking and re-solve triggers | replace | Python, TypeScript, C# | `langs/python/src/guidearch/viewmodels/scenario_vm.py`; `langs/typescript/src/viewmodels/scenario-vm.ts`; `langs/csharp/src/GuideArch.ViewModels/ScenarioVMFactory.cs`; `langs/csharp/src/GuideArch.ViewModels/ScenarioState.cs` | 250 | 70 | 180 | 42 | dirty state, save eligibility, solve trigger matrix | high |
| `R7` | Property/value change subscriptions | partial | Python, TypeScript, C# | `langs/python/src/guidearch/viewmodels/app_vm.py`; `langs/python/src/guidearch/viewmodels/scenario_vm.py`; `langs/python/src/guidearch/view/adapters/vmx_to_nicegui.py`; `langs/python/src/guidearch/main.py`; `langs/typescript/src/viewmodels/app-vm.ts`; `langs/typescript/src/view/adapters/vmx-to-svelte.ts`; `langs/typescript/src/routes/+page.svelte`; `langs/typescript/src/routes/lib/StatusBar.svelte`; `langs/typescript/src/routes/lib/Toolbar.svelte`; `langs/csharp/src/GuideArch.ViewModels/AppVMFactory.cs`; `langs/csharp/src/GuideArch.View/MainWindow.axaml.cs` | 210 | 90 | 120 | 33 | property notifications, subscription fan-out, UI refresh hooks | high |
| `R8` | Add/update/delete command surfaces | replace | Python, TypeScript, C# | `langs/python/src/guidearch/viewmodels/scenario_vm.py`; `langs/typescript/src/viewmodels/scenario-vm.ts`; `langs/csharp/src/GuideArch.ViewModels/ScenarioVMFactory.cs` | 185 | 70 | 115 | 30 | CRUD entrypoints and cascades | medium |
| `R9` | Save/open/new command workflows | partial | Python, TypeScript, C# | `langs/python/src/guidearch/viewmodels/scenario_vm.py`; `langs/python/src/guidearch/main.py`; `langs/typescript/src/viewmodels/scenario-vm.ts`; `langs/typescript/src/routes/lib/Toolbar.svelte`; `langs/csharp/src/GuideArch.ViewModels/ScenarioVMFactory.cs`; `langs/csharp/src/GuideArch.View/MainWindow.axaml.cs` | 140 | 85 | 55 | 24 | file lifecycle, sample loading, failure surfacing | medium |
| `R10` | Dialog and confirmation handling | partial | Python, TypeScript, C# | `langs/python/src/guidearch/main.py`; `langs/typescript/src/routes/lib/ConfirmDialog.svelte`; `langs/typescript/src/routes/lib/confirm-dialog.ts`; `langs/csharp/src/GuideArch.View/MainWindow.axaml.cs` | 60 | 80 | -20 | 18 | destructive confirmations and dialog routing | medium |
| `R11` | Candidate selection state | replace | Python, TypeScript, C# | `langs/python/src/guidearch/viewmodels/scenario_vm.py`; `langs/python/src/guidearch/main.py`; `langs/typescript/src/viewmodels/scenario-vm.ts`; `langs/typescript/src/routes/lib/ResultsTab.svelte`; `langs/csharp/src/GuideArch.ViewModels/ScenarioVMFactory.cs`; `langs/csharp/src/GuideArch.View/MainWindow.axaml.cs` | 70 | 25 | 45 | 12 | selected candidate tracking and defaulting | low |
| `R12` | View adapter/store glue | partial | Python, TypeScript, C# | `langs/python/src/guidearch/view/adapters/vmx_to_nicegui.py`; `langs/python/src/guidearch/main.py`; `langs/typescript/src/view/adapters/vmx-to-svelte.ts`; `langs/typescript/src/routes/+page.svelte`; `langs/typescript/src/routes/lib/ResultsTab.svelte`; `langs/typescript/src/routes/lib/StatusBar.svelte`; `langs/typescript/src/routes/lib/Toolbar.svelte`; `langs/csharp/src/GuideArch.View/MainWindow.axaml`; `langs/csharp/src/GuideArch.View/MainWindow.axaml.cs` | 180 | 80 | 100 | 21 | adapter/store contracts and broad rerender glue | high |
| `R13` | `AppVM` theme persistence and warnings | partial | Python, TypeScript, C# | `langs/python/src/guidearch/viewmodels/app_vm.py`; `langs/typescript/src/viewmodels/app-vm.ts`; `langs/csharp/src/GuideArch.ViewModels/AppVMFactory.cs` | 55 | 70 | -15 | 9 | theme persistence, warnings, stable child reference | low |
| `R14` | Conceptual VM tree composites/aggregates | defer | Python, TypeScript, C# | `langs/python/src/guidearch/viewmodels/__init__.py`; `langs/typescript/src/viewmodels/index.ts`; `langs/csharp/src/GuideArch.ViewModels/ScenarioState.cs` | 0 | 0 | 0 | 0 | future tree ownership abstractions only | low |

The ledger still supports starting with replacements that have both cross-language parity and clear behavior anchors, then using the exact-file test-impact matrix below to control risk as the refactor moves outward from VM roots into adapters and views.

## LOC Savings Metrics

LOC accounting rules and raw counts: [replacement-ledger.md](vmx-3-1-audit/replacement-ledger.md), [loc-baseline.txt](vmx-3-1-audit/loc-baseline.txt)

| Area | Baseline LOC | Projected replacement LOC | Net LOC delta | Confidence |
| --- | ---: | ---: | ---: | --- |
| ViewModel production | 5436 | 4616 | 820 | medium |
| View/adapter production | 9109 | 8884 | 225 | low |
| Total production | 14545 | 13500 | 1045 | medium |
| Tests | 10944 | 11274 | +330 | medium |

Counting method and exclusions:

- Baseline counts were captured with explicit `wc -l` file lists in [loc-baseline.txt](vmx-3-1-audit/loc-baseline.txt).
- ViewModel production, view/adapter production, and test LOC remain separated exactly as they were counted in the baseline artifact.
- Docs, build outputs, generated assets, lockfiles, caches, and formatting-only churn are out of scope for these savings numbers.
- Test LOC increases are expected here because the replacement path adds parity coverage around forms, observable collections, and selection behavior.

## Test Coverage Changes

Source-of-truth matrix: [test-impact.md](vmx-3-1-audit/test-impact.md)

### Counts by language

| Category | Python | TypeScript | C# |
| --- | ---: | ---: | ---: |
| must keep passing | 14 | 14 | 13 |
| rewrite | 4 | 4 | 4 |
| add | 3 | 3 | 3 |
| remove | 1 | 1 | 0 |

### Exact-file must-keep matrix

| Behavior | Python | TypeScript | C# |
| --- | --- | --- | --- |
| VM construction and lifecycle | `langs/python/tests/unit/test_vm_tree.py`, `langs/python/tests/integration/test_vm_tree_comprehensive.py` | `langs/typescript/tests/unit/vm-tree.test.ts`, `langs/typescript/tests/integration/vm-tree-comprehensive.test.ts` | `langs/csharp/tests/GuideArch.ViewModels.Tests/VMTreeTests.cs`, `langs/csharp/tests/GuideArch.ViewModels.Tests/VMTreeComprehensiveTests.cs` |
| Property change propagation | `langs/python/tests/unit/test_app_vm.py`, `langs/python/tests/unit/test_selection_state.py`, `langs/python/tests/unit/test_vmx_to_nicegui_adapter.py` | `langs/typescript/tests/unit/app-vm.test.ts`, `langs/typescript/tests/unit/vmx-to-svelte.test.ts` | `langs/csharp/tests/GuideArch.ViewModels.Tests/AppVMTests.cs`, `langs/csharp/tests/GuideArch.ViewModels.Tests/SelectionStateTests.cs`, `langs/csharp/tests/GuideArch.ViewModels.Tests/VMTreeComprehensiveTests.cs` |
| Dirty tracking and re-solve triggers | `langs/python/tests/unit/test_scenario_vm.py`, `langs/python/tests/unit/test_vm_mvvm.py`, `langs/python/tests/integration/test_vm_tree_comprehensive.py` | `langs/typescript/tests/unit/scenario-vm.test.ts`, `langs/typescript/tests/integration/vm-mvvm.test.ts`, `langs/typescript/tests/integration/vm-tree-comprehensive.test.ts` | `langs/csharp/tests/GuideArch.ViewModels.Tests/ScenarioVMTests.cs`, `langs/csharp/tests/GuideArch.ViewModels.Tests/VMMvvmIntegrationTests.cs`, `langs/csharp/tests/GuideArch.ViewModels.Tests/VMTreeComprehensiveTests.cs` |
| Add/update/delete cascades | `langs/python/tests/unit/test_editor_cascades.py` | `langs/typescript/tests/unit/editor-cascades.test.ts` | `langs/csharp/tests/GuideArch.ViewModels.Tests/EditorCascadesTests.cs` |
| Save/open failure behavior | `langs/python/tests/unit/test_scenario_vm.py`, `langs/python/tests/unit/test_save_roundtrip.py`, `langs/python/tests/unit/test_scenario_loader.py` | `langs/typescript/tests/unit/scenario-vm.test.ts`, `langs/typescript/tests/unit/save-roundtrip.test.ts`, `langs/typescript/tests/unit/scenario-loader.test.ts` | `langs/csharp/tests/GuideArch.ViewModels.Tests/ScenarioVMTests.cs`, `langs/csharp/tests/GuideArch.ViewModels.Tests/SaveRoundtripTests.cs` |
| Candidate selection | `langs/python/tests/unit/test_selection_state.py`, `langs/python/tests/unit/test_results_subtabs.py` | `langs/typescript/tests/unit/selection-state.test.ts` | `langs/csharp/tests/GuideArch.ViewModels.Tests/SelectionStateTests.cs` |
| Collection/filter behavior | `langs/python/tests/unit/test_vm_tree.py`, `langs/python/tests/integration/test_vm_tree_comprehensive.py` | `langs/typescript/tests/unit/vm-tree.test.ts`, `langs/typescript/tests/integration/vm-tree-comprehensive.test.ts` | `langs/csharp/tests/GuideArch.ViewModels.Tests/VMTreeComprehensiveTests.cs`, `langs/csharp/tests/GuideArch.ViewModels.Tests/ChartDataTests.cs` |
| Dialog/confirmation behavior | `langs/python/tests/unit/test_confirm_dialog.py`, `langs/python/tests/unit/test_toolbar.py` | `langs/typescript/tests/unit/confirm-dialog.test.ts`, `langs/typescript/tests/unit/confirm-dialog-motion.test.ts`, `langs/typescript/tests/unit/toolbar-markup.test.ts` | `langs/csharp/tests/GuideArch.ViewModels.Tests/DiscardConfirmTests.cs`, `langs/csharp/tests/GuideArch.ViewModels.Tests/DialogScrimCancelTests.cs`, `langs/csharp/tests/GuideArch.ViewModels.Tests/DialogStyleTests.cs` |
| Cross-language conformance | `langs/python/tests/conformance/test_conformance.py` | `langs/typescript/src/conformance/runner.ts` | `langs/csharp/tests/GuideArch.Models.Tests/ConformanceTests.cs` |

### Exact-file rewrite matrix

| Exact test file | Language | Reason to rewrite | Replacement assertion |
| --- | --- | --- | --- |
| `langs/python/tests/unit/test_app_vm.py` | Python | Current assertions assume manual `property_changed` semantics and root-specific theme wiring. | Assert derived theme/warning state and stable child reference on the replacement AppVM surface. |
| `langs/python/tests/unit/test_vmx_to_nicegui_adapter.py` | Python | Tight to the current `vmx_to_nicegui.py` proxy and raw string property notifications. | Assert the replacement NiceGUI bridge reacts to `FormVM`/`ObservableList` updates and command decorators. |
| `langs/python/tests/unit/test_vm_tree.py` | Python | Type and shape checks assume bespoke roots and plain result wrappers. | Assert the new VM tree uses `FormVM`/readonly component helpers while keeping public model fields stable. |
| `langs/python/tests/integration/test_vm_tree_comprehensive.py` | Python | The solve-trigger matrix and property-change probes are anchored to the current hand-rolled root. | Re-assert the same behavior through the replacement root lifecycle and selection abstractions. |
| `langs/typescript/tests/unit/app-vm.test.ts` | TypeScript | The current hub assertion is already sensitive to VMx 3.1 property-name behavior and will shift again under derived AppVM state. | Assert the replacement AppVM publishes the expected theme/warning outcomes without depending on the old message shape. |
| `langs/typescript/tests/unit/vmx-to-svelte.test.ts` | TypeScript | This suite is hard-wired to `vmxToStore` whole-model rebroadcast behavior. | Assert the replacement Svelte bridge updates from observable collections and derived properties. |
| `langs/typescript/tests/unit/vm-tree.test.ts` | TypeScript | Factory/type checks assume `ComponentVMOf` everywhere, including result-only wrappers. | Assert the new mix of form, readonly, and selection-capable VMs with unchanged public model data. |
| `langs/typescript/tests/integration/vm-tree-comprehensive.test.ts` | TypeScript | Comprehensive matrix coverage is coupled to the present root and adapter contracts. | Re-assert lifecycle, solve triggers, readonly results, and selection through the replacement VMx 3.1 surface. |
| `langs/csharp/tests/GuideArch.ViewModels.Tests/AppVMTests.cs` | C# | Property-changed and theme-warning assertions are tied to the current `AppVMFactory` state transitions. | Assert the replacement AppVM preserves persistence and stable child behavior while publishing the new derived state. |
| `langs/csharp/tests/GuideArch.ViewModels.Tests/VMTreeTests.cs` | C# | Current type checks assume factory-per-leaf `ComponentVM<T>` outputs only. | Assert the replacement helper mix while keeping the public ViewModel tree available to Avalonia. |
| `langs/csharp/tests/GuideArch.ViewModels.Tests/VMTreeComprehensiveTests.cs` | C# | The broad matrix is anchored to `ScenarioVMFactory` and manual mutator plumbing. | Re-assert the same lifecycle, CRUD, readonly-result, and solve-trigger behavior through the replacement surface. |
| `langs/csharp/tests/GuideArch.ViewModels.Tests/DiscardConfirmTests.cs` | C# | This suite scans code-behind text for the current confirm implementation. | Assert dialog-service and confirmation-decorator behavior at the new VM/view seam instead of source text. |

### Exact-file additions

| Proposed exact test file | Language | Behavior to cover | Replacement IDs |
| --- | --- | --- | --- |
| `langs/python/tests/unit/test_scenario_form_vm.py` | Python | `FormVM` dirty-state, approve/deny, and save-eligibility behavior for the scenario root. | `R1`, `R6`, `R9` |
| `langs/python/tests/unit/test_scenario_crud_commands.py` | Python | `ModeledCrudCommands` parity for decision, alternative, property, and constraint mutations. | `R2`, `R8` |
| `langs/python/tests/unit/test_scenario_observable_lists.py` | Python | `ObservableList`/derived collection behavior for coefficients, constraints, and selected candidate state. | `R4`, `R5`, `R7`, `R11`, `R12` |
| `langs/typescript/tests/unit/scenario-form-vm.test.ts` | TypeScript | Replacement root form lifecycle and dirty/save gating. | `R1`, `R6`, `R9` |
| `langs/typescript/tests/unit/scenario-crud-commands.test.ts` | TypeScript | CRUD-command parity for editable scenario children. | `R2`, `R8` |
| `langs/typescript/tests/unit/scenario-observable-lists.test.ts` | TypeScript | Observable list, derived property, and selection-capability behavior in the Svelte-facing surface. | `R4`, `R5`, `R7`, `R11`, `R12` |
| `langs/csharp/tests/GuideArch.ViewModels.Tests/ScenarioFormVMTests.cs` | C# | Replacement form lifecycle, dirty flag, and save gating for the Avalonia root. | `R1`, `R6`, `R9` |
| `langs/csharp/tests/GuideArch.ViewModels.Tests/ScenarioCrudCommandTests.cs` | C# | CRUD-command parity for the current mutator-heavy editor flows. | `R2`, `R8` |
| `langs/csharp/tests/GuideArch.ViewModels.Tests/ObservableCollectionProjectionTests.cs` | C# | Observable collection, derived projection, and selection behavior feeding `ScenarioState`-like view data. | `R4`, `R5`, `R7`, `R11`, `R12` |

### Exact-file removals

| Exact test file or `None` | Language | Removal condition |
| --- | --- | --- |
| `langs/python/tests/unit/test_vmx_to_nicegui_adapter.py` | Python | Remove only if `langs/python/src/guidearch/view/adapters/vmx_to_nicegui.py` is fully retired and its replacement coverage lives in `test_scenario_form_vm.py` plus `test_scenario_observable_lists.py`. |
| `langs/typescript/tests/unit/vmx-to-svelte.test.ts` | TypeScript | Remove only if `langs/typescript/src/view/adapters/vmx-to-svelte.ts` is deleted and Svelte bridge coverage moves to the replacement observable-list suites. |
| `None` | C# | Prefer rewrites of existing xUnit suites; no outright deletion is required for the first VMx 3.1 refactor wave. |

## Recommended Refactor Phases

### Phase 1: Dependency And Compatibility

- **Replacement IDs:** prerequisite for `R1`-`R14`
- **Primary files:** `langs/python/pyproject.toml`, `langs/python/uv.lock`, `vendor/vmx`, `vendor/vmx/langs/typescript/package.json`, `vendor/vmx/langs/typescript/src/version.ts`, `vendor/vmx/langs/csharp/src/VMx/VMx.csproj`, `vendor/vmx/langs/python/src/vmx/__about__.py`
- **Verification commands:**
  - `cd langs/python && uv sync --all-extras && uv run python - <<'PY' ... PY`
  - `cd vendor/vmx/langs/typescript && pnpm install && pnpm build`
  - `cd langs/typescript && pnpm install && pnpm test`
  - `cd langs/csharp && dotnet test --nologo`
  - `cd langs/python && uv run pytest tests/ -q`
- **Stop/go criteria:** go only after the branch is pinned to Python `vmx==3.1.0`, the vendor source commit is recorded as `a77b25aa28078eb3d56fba011d4fe32e9a2a4d12`, and the known compatibility failures from [baseline.md](vmx-3-1-audit/baseline.md) are reproduced or intentionally resolved rather than rediscovered mid-refactor.

### Phase 2: Safe Mechanical Replacements

- **Replacement IDs:** `R2`, `R3`, `R11`, `R13`
- **Primary files:** `langs/python/src/guidearch/viewmodels/alternative_vm.py`, `decision_vm.py`, `property_vm.py`, `coefficient_vm.py`, `constraint_vm.py`, `candidate_vm.py`, `critical_decision_vm.py`, `critical_constraint_vm.py`, `app_vm.py`; `langs/typescript/src/viewmodels/alternative-vm.ts`, `decision-vm.ts`, `property-vm.ts`, `coefficient-vm.ts`, `constraint-vm.ts`, `candidate-vm.ts`, `critical-decision-vm.ts`, `critical-constraint-vm.ts`, `app-vm.ts`; `langs/csharp/src/GuideArch.ViewModels/AlternativeVMFactory.cs`, `DecisionVMFactory.cs`, `PropertyVMFactory.cs`, `CoefficientVMFactory.cs`, `ConstraintVMFactory.cs`, `CandidateVMFactory.cs`, `CriticalDecisionVMFactory.cs`, `CriticalConstraintVMFactory.cs`, `AppVMFactory.cs`
- **Verification commands:**
  - Python VM tests from [test-impact.md](vmx-3-1-audit/test-impact.md):
    `cd langs/python && uv run pytest tests/unit/test_app_vm.py tests/unit/test_selection_state.py tests/unit/test_vm_tree.py tests/integration/test_vm_tree_comprehensive.py -q`
  - TypeScript tests:
    `cd langs/typescript && pnpm test -- tests/unit/app-vm.test.ts tests/unit/selection-state.test.ts tests/unit/vm-tree.test.ts tests/integration/vm-tree-comprehensive.test.ts`
  - C# tests:
    `cd langs/csharp && dotnet test tests/GuideArch.ViewModels.Tests/GuideArch.ViewModels.Tests.csproj --nologo --filter "(FullyQualifiedName~AppVMTests|FullyQualifiedName~SelectionStateTests|FullyQualifiedName~VMTreeTests|FullyQualifiedName~VMTreeComprehensiveTests)"`
- **Stop/go criteria:** go only if public model field names, stable child references, and candidate-selection behavior stay intact while the repeated leaf and readonly wrapper scaffolding shrinks.

### Phase 3: Collection And Composite Cleanup

- **Replacement IDs:** `R4`, `R5`, `R11`, `R12`; keep `R14` deferred unless a concrete tree-shape problem remains after this phase
- **Primary files:** `langs/python/src/guidearch/viewmodels/scenario_vm.py`, `langs/python/src/guidearch/main.py`, `langs/python/src/guidearch/view/adapters/vmx_to_nicegui.py`; `langs/typescript/src/viewmodels/scenario-vm.ts`, `langs/typescript/src/view/adapters/vmx-to-svelte.ts`, `langs/typescript/src/routes/lib/CoefficientsTab.svelte`, `ConstraintsTab.svelte`, `CriticalConstraintsTab.svelte`, `ResultsTab.svelte`, `StatusBar.svelte`; `langs/csharp/src/GuideArch.ViewModels/ScenarioState.cs`, `ScenarioVMFactory.cs`, `langs/csharp/src/GuideArch.View/MainWindow.axaml.cs`
- **Verification commands:**
  - Python:
    `cd langs/python && uv run pytest tests/unit/test_editor_cascades.py tests/unit/test_results_subtabs.py tests/unit/test_selection_state.py tests/unit/test_vmx_to_nicegui_adapter.py tests/unit/test_vm_tree.py tests/integration/test_vm_tree_comprehensive.py -q`
  - TypeScript:
    `cd langs/typescript && pnpm test -- tests/unit/editor-cascades.test.ts tests/unit/selection-state.test.ts tests/unit/vmx-to-svelte.test.ts tests/unit/vm-tree.test.ts tests/integration/vm-tree-comprehensive.test.ts`
  - C#:
    `cd langs/csharp && dotnet test tests/GuideArch.ViewModels.Tests/GuideArch.ViewModels.Tests.csproj --nologo --filter "(FullyQualifiedName~ChartDataTests|FullyQualifiedName~SelectionStateTests|FullyQualifiedName~VMTreeComprehensiveTests)"`
- **Stop/go criteria:** go only if collection mutation and filtered-view behavior move onto observable or derived VMx surfaces without breaking coefficient grids, constraint slices, selected candidate state, or results projections.

### Phase 4: Form And Command Cleanup

- **Replacement IDs:** `R1`, `R6`, `R8`, `R9`, `R10`
- **Primary files:** `langs/python/src/guidearch/viewmodels/scenario_vm.py`, `langs/python/src/guidearch/main.py`; `langs/typescript/src/viewmodels/scenario-vm.ts`, `langs/typescript/src/routes/lib/Toolbar.svelte`, `langs/typescript/src/routes/lib/ConfirmDialog.svelte`, `langs/typescript/src/routes/lib/confirm-dialog.ts`; `langs/csharp/src/GuideArch.ViewModels/ScenarioVMFactory.cs`, `langs/csharp/src/GuideArch.View/MainWindow.axaml.cs`
- **Verification commands:**
  - Python VM tests:
    `cd langs/python && uv run pytest tests/unit/test_scenario_vm.py tests/unit/test_editor_cascades.py tests/unit/test_confirm_dialog.py tests/unit/test_toolbar.py tests/unit/test_vm_mvvm.py tests/integration/test_vm_tree_comprehensive.py -q`
  - TypeScript tests:
    `cd langs/typescript && pnpm test -- tests/unit/scenario-vm.test.ts tests/unit/editor-cascades.test.ts tests/unit/confirm-dialog.test.ts tests/unit/confirm-dialog-motion.test.ts tests/unit/toolbar-markup.test.ts tests/integration/vm-mvvm.test.ts tests/integration/vm-tree-comprehensive.test.ts`
  - C# tests:
    `cd langs/csharp && dotnet test tests/GuideArch.ViewModels.Tests/GuideArch.ViewModels.Tests.csproj --nologo --filter "(FullyQualifiedName~ScenarioVMTests|FullyQualifiedName~EditorCascadesTests|FullyQualifiedName~DiscardConfirmTests|FullyQualifiedName~DialogScrimCancelTests|FullyQualifiedName~VMMvvmIntegrationTests|FullyQualifiedName~VMTreeComprehensiveTests)"`
- **Stop/go criteria:** go only if dirty/save gating, approve/deny flows, CRUD cascades, file lifecycle behavior, and destructive confirmations are covered through the replacement form and command surface before bespoke branches are deleted.

### Phase 5: View Adapter Simplification

- **Replacement IDs:** `R7`, `R10`, `R12`
- **Primary files:** `langs/python/src/guidearch/view/adapters/vmx_to_nicegui.py`, `langs/python/src/guidearch/main.py`; `langs/typescript/src/view/adapters/vmx-to-svelte.ts`, `langs/typescript/src/routes/+page.svelte`, `langs/typescript/src/routes/lib/ResultsTab.svelte`, `StatusBar.svelte`, `Toolbar.svelte`; `langs/csharp/src/GuideArch.View/MainWindow.axaml`, `langs/csharp/src/GuideArch.View/MainWindow.axaml.cs`
- **Verification commands:**
  - Python:
    `cd langs/python && uv run pytest tests/unit/test_app_vm.py tests/unit/test_toolbar.py tests/unit/test_vmx_to_nicegui_adapter.py tests/unit/test_vm_mvvm.py -q`
  - TypeScript:
    `cd langs/typescript && pnpm test -- tests/unit/app-vm.test.ts tests/unit/vmx-to-svelte.test.ts tests/unit/toolbar-markup.test.ts tests/integration/vm-mvvm.test.ts`
  - C#:
    `cd langs/csharp && dotnet test tests/GuideArch.ViewModels.Tests/GuideArch.ViewModels.Tests.csproj --nologo --filter "(FullyQualifiedName~AppVMTests|FullyQualifiedName~DiscardConfirmTests|FullyQualifiedName~DialogStyleTests|FullyQualifiedName~ToolbarMarkupTests|FullyQualifiedName~VMMvvmIntegrationTests)"`
- **Stop/go criteria:** go only if UI adapters are reduced to framework-native translation work and no longer depend on whole-model rebroadcast assumptions or the old code-behind confirmation seam.

### Phase 6: Parity And Cleanup

- **Replacement IDs:** all landed replacements from `R1` through `R13`; keep `R14` deferred unless a later design explicitly promotes it
- **Primary files:** all touched production files above plus the rewrite/add/remove test files named in [test-impact.md](vmx-3-1-audit/test-impact.md)
- **Verification commands:**
  - Python VM tests:
    `cd langs/python && uv run pytest tests/unit/test_app_vm.py tests/unit/test_confirm_dialog.py tests/unit/test_editor_cascades.py tests/unit/test_results_subtabs.py tests/unit/test_save_roundtrip.py tests/unit/test_scenario_loader.py tests/unit/test_scenario_vm.py tests/unit/test_selection_state.py tests/unit/test_toolbar.py tests/unit/test_vmx_to_nicegui_adapter.py tests/unit/test_vm_mvvm.py tests/unit/test_vm_tree.py tests/integration/test_vm_tree_comprehensive.py -q`
  - Python conformance:
    `cd langs/python && uv run pytest tests/conformance/test_conformance.py -q`
  - TypeScript tests:
    `cd langs/typescript && pnpm test -- tests/unit/app-vm.test.ts tests/unit/confirm-dialog-motion.test.ts tests/unit/confirm-dialog.test.ts tests/unit/editor-cascades.test.ts tests/unit/save-roundtrip.test.ts tests/unit/scenario-loader.test.ts tests/unit/scenario-vm.test.ts tests/unit/selection-state.test.ts tests/unit/toolbar-markup.test.ts tests/unit/vm-tree.test.ts tests/unit/vmx-to-svelte.test.ts tests/integration/vm-mvvm.test.ts tests/integration/vm-tree-comprehensive.test.ts`
  - TypeScript conformance:
    `cd langs/typescript && pnpm conformance`
  - C# tests:
    `cd langs/csharp && dotnet test tests/GuideArch.ViewModels.Tests/GuideArch.ViewModels.Tests.csproj --nologo`
  - C# conformance:
    `cd langs/csharp && dotnet test tests/GuideArch.Models.Tests/GuideArch.Models.Tests.csproj --nologo --filter FullyQualifiedName~ConformanceTests`
- **Stop/go criteria:** go only if the must-keep suites and conformance entrypoints all pass on the replacement surface and any test removals happen only under the removal conditions recorded in [test-impact.md](vmx-3-1-audit/test-impact.md).

## Non-Goals And Rejected Candidates

Design-level non-goals: [2026-07-02-vmx-3-1-refactor-audit-design.md](2026-07-02-vmx-3-1-refactor-audit-design.md)

- This audit does **not** authorize a broad production refactor yet.
- It does **not** change GuideArch's domain model, TOPSIS algorithms, JSON schema, or release versioning policy.
- Any VMx abstraction that would force algorithm or domain changes is out of scope.

Rejected or deferred VMx 3.1 candidates from [vmx-3-1-capabilities.md](vmx-3-1-audit/vmx-3-1-capabilities.md) and [replacement-ledger.md](vmx-3-1-audit/replacement-ledger.md):

- `GroupVM`: valid cross-language, but no clear current hotspot where grouped ownership without selection reduces GuideArch complexity.
- `AggregateVM1` through `AggregateVM6`: available, but GuideArch pressure is around editable collections, derived state, and command orchestration rather than fixed-slot VM bundles.
- `PagedComposition`, token paging, and paging capabilities: well-supported but weak fits for the current local scenario sizes.
- search and filter capabilities: plausible future UX additions, not near-term migration targets.
- notification services: parity is weaker here than for forms, commands, collections, and derived state.
- `CompositeVM`, `CompositeVMOf`, `FilteredCompositeVM`, and `ScoredFilteredCompositeVM`: viable second-wave options, but `R14` remains deferred until the first-wave cleanup makes any remaining tree-shape pain more concrete.

## Appendix: Supporting Artifacts

- [baseline.md](vmx-3-1-audit/baseline.md)
- [loc-baseline.txt](vmx-3-1-audit/loc-baseline.txt)
- [current-usage.md](vmx-3-1-audit/current-usage.md)
- [vmx-3-1-capabilities.md](vmx-3-1-audit/vmx-3-1-capabilities.md)
- [replacement-ledger.md](vmx-3-1-audit/replacement-ledger.md)
- [test-impact.md](vmx-3-1-audit/test-impact.md)
