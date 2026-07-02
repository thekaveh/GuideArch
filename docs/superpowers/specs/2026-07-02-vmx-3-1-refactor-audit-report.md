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
- GuideArch Python baseline: `fail` during collection because GuideArch still imports `RelayCommandOfT`, which VMx 3.1.0 no longer exports.

Those failures matter for planning because they establish that VMx 3.1 compatibility pressure is already visible in Python command imports and TypeScript property-changed assertions before any broad refactor starts.

## Current Usage Findings

Source-of-truth inventory: [current-usage.md](vmx-3-1-audit/current-usage.md)

### Cross-language summary

| Language | Primary VMx usage | Largest bespoke areas | Audit takeaway |
| --- | --- | --- | --- |
| Python | Hybrid: leaf editors subclass `ComponentVMOf`, but `ScenarioVM` and `AppVM` hand-roll command wiring, `property_changed`, and `MessageHub` publication. | `langs/python/src/guidearch/viewmodels/scenario_vm.py`, `langs/python/src/guidearch/viewmodels/app_vm.py`, `langs/python/src/guidearch/view/adapters/vmx_to_nicegui.py`, `langs/python/src/guidearch/main.py` | Highest bespoke VMx surface and the earliest 3.1 compatibility break (`RelayCommandOfT`). |
| TypeScript | Broad direct VMx usage with `ComponentVMOf` builders, `RelayCommand` / `RelayCommandOf`, and a Svelte store bridge. | `langs/typescript/src/viewmodels/scenario-vm.ts`, `langs/typescript/src/viewmodels/app-vm.ts`, `langs/typescript/src/view/adapters/vmx-to-svelte.ts`, route components under `langs/typescript/src/routes/lib/` | Closest to VMx primitives, but broad store coupling magnifies changes in property publication semantics. |
| C# | Factory-first `ComponentVM<T>` usage with root factories attaching `RelayCommand` commands and Avalonia views binding through open-generic component VMs. | `langs/csharp/src/GuideArch.ViewModels/ScenarioVMFactory.cs`, `langs/csharp/src/GuideArch.ViewModels/AppVMFactory.cs`, `langs/csharp/src/GuideArch.View/MainWindow.axaml`, `langs/csharp/src/GuideArch.View/MainWindow.axaml.cs` | Structurally aligned with VMx, but the view layer amplifies migration cost. |

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

Full decision matrix: [replacement-ledger.md](vmx-3-1-audit/replacement-ledger.md)

### Highest-value recommendations

| ID | Decision | Net production LOC | Test LOC delta | Why it matters |
| --- | --- | ---: | ---: | --- |
| `R6` | replace | 180 | 42 | Replaces bespoke dirty tracking and solve-trigger gating with `FormVM`, form builders, and property-value change helpers. |
| `R1` | partial | 140 | 45 | Lets `FormVM` absorb lifecycle scaffolding while preserving GuideArch-specific solve orchestration and serialization boundaries. |
| `R2` | replace | 140 | 24 | Collapses repeated editable leaf construction into shared builder helpers. |
| `R7` | partial | 120 | 33 | Shrinks property notification and subscription fan-out by moving more state onto reactive VMx surfaces. |
| `R3` | replace | 120 | 18 | Converts readonly result wrappers to the purpose-built readonly modeled component surface. |
| `R8` | replace | 115 | 30 | Moves repeated CRUD entrypoints toward `ModeledCrudCommands`. |
| `R12` | partial | 100 | 21 | Simplifies the broadest adapter/store glue after roots expose cleaner state. |

### Lower-leverage or deferred recommendations

| ID | Decision | Net production LOC | Why it is not first |
| --- | --- | ---: | --- |
| `R10` | partial | -20 | Confirmation and dialog handling likely need a thin abstraction layer before savings appear. |
| `R13` | partial | -15 | `AppVM` is smaller already; theme persistence remains partly bespoke even after cleanup. |
| `R14` | defer | 0 | Composite and aggregate tree-shape refactors should wait until forms, CRUD commands, derived properties, and observable collections are already adopted. |

The ledger strongly supports starting with replacements that have both cross-language parity and clear behavior anchors, then using the test-impact matrix to control risk as the refactor moves outward from the VM roots into adapters and views.

## LOC Savings Metrics

LOC accounting rules and raw counts: [replacement-ledger.md](vmx-3-1-audit/replacement-ledger.md), [loc-baseline.txt](vmx-3-1-audit/loc-baseline.txt)

| Area | Baseline LOC | Projected replacement LOC | Net saved | Confidence |
| --- | ---: | ---: | ---: | --- |
| ViewModel production | 5436 | 4616 | 820 | medium |
| View/adapter production | 9109 | 8884 | 225 | low |
| Total production | 14545 | 13500 | 1045 | medium |
| Tests | 10241 | 10571 | -330 | medium |

Counting method and exclusions:

- Baseline counts were captured with explicit `wc -l` file lists in [loc-baseline.txt](vmx-3-1-audit/loc-baseline.txt).
- ViewModel production, view/adapter production, and test LOC remain separated exactly as they were counted in the baseline artifact.
- Docs, build outputs, generated assets, lockfiles, caches, and formatting-only churn are out of scope for these savings numbers.
- Negative test savings are expected here because the replacement path adds parity coverage around forms, observable collections, and selection behavior.

## Test Coverage Changes

Source-of-truth matrix: [test-impact.md](vmx-3-1-audit/test-impact.md)

### Counts by language

| Category | Python | TypeScript | C# |
| --- | ---: | ---: | ---: |
| must keep passing | 14 | 14 | 13 |
| rewrite | 4 | 4 | 4 |
| add | 3 | 3 | 3 |
| remove | 1 | 1 | 0 |

### Tests that remain the behavioral gate

- VM construction and lifecycle:
  `test_vm_tree.py`, `vm-tree.test.ts`, `VMTreeTests.cs`, plus the comprehensive VM-tree suites in all three languages.
- Dirty tracking and re-solve behavior:
  `test_scenario_vm.py`, `scenario-vm.test.ts`, `ScenarioVMTests.cs`, and the VM-MVVM/integration suites.
- Add/update/delete cascades:
  `test_editor_cascades.py`, `editor-cascades.test.ts`, `EditorCascadesTests.cs`.
- Save/open failure behavior:
  `test_save_roundtrip.py`, `save-roundtrip.test.ts`, `SaveRoundtripTests.cs`, plus the scenario loader coverage where applicable.
- Candidate selection:
  `test_selection_state.py`, `selection-state.test.ts`, `SelectionStateTests.cs`.
- Dialog and confirmation behavior:
  `test_confirm_dialog.py`, `confirm-dialog.test.ts`, `DiscardConfirmTests.cs`, `DialogScrimCancelTests.cs`, and related toolbar/markup tests.
- Cross-language conformance:
  `tests/conformance/test_conformance.py`, `src/conformance/runner.ts`, `ConformanceTests.cs`.

### Tests that should be rewritten

- App/root property-change tests in all three languages because they currently assert old message shapes or old factory transitions.
- Adapter/store tests in Python and TypeScript because they are tightly coupled to raw property-notification and whole-model rebroadcast behavior.
- Broad VM-tree suites because the replacement surface will mix `FormVM`, readonly modeled components, observable collections, and selection capabilities.
- C# discard-confirmation tests because they currently inspect the old code-behind seam.

### Tests that should be added

- Scenario form lifecycle suites:
  `test_scenario_form_vm.py`, `scenario-form-vm.test.ts`, `ScenarioFormVMTests.cs`.
- CRUD command parity suites:
  `test_scenario_crud_commands.py`, `scenario-crud-commands.test.ts`, `ScenarioCrudCommandTests.cs`.
- Observable collection and selection suites:
  `test_scenario_observable_lists.py`, `scenario-observable-lists.test.ts`, `ObservableCollectionProjectionTests.cs`.

### Tests that may be removed

- Python `test_vmx_to_nicegui_adapter.py` only if `vmx_to_nicegui.py` is fully retired and its replacement coverage lands in the new form and observable-list suites.
- TypeScript `vmx-to-svelte.test.ts` only if `vmx-to-svelte.ts` is deleted and replacement bridge coverage moves into the new observable-list suites.
- No outright C# suite deletion is required in the first wave.

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
- **Primary files:** `langs/python/src/guidearch/viewmodels/alternative_vm.py`, `decision_vm.py`, `property_vm.py`, `coefficient_vm.py`, `constraint_vm.py`, `candidate_vm.py`, `critical_decision_vm.py`, `critical_constraint_vm.py`; `langs/typescript/src/viewmodels/alternative-vm.ts`, `decision-vm.ts`, `property-vm.ts`, `coefficient-vm.ts`, `constraint-vm.ts`, `candidate-vm.ts`, `critical-decision-vm.ts`, `critical-constraint-vm.ts`, `app-vm.ts`; `langs/csharp/src/GuideArch.ViewModels/AlternativeVMFactory.cs`, `DecisionVMFactory.cs`, `PropertyVMFactory.cs`, `CoefficientVMFactory.cs`, `ConstraintVMFactory.cs`, `CandidateVMFactory.cs`, `CriticalDecisionVMFactory.cs`, `CriticalConstraintVMFactory.cs`, `AppVMFactory.cs`
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
