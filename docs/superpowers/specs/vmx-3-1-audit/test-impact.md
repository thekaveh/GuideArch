# VMx 3.1 Test Coverage Impact Matrix

## Summary

| Category | Python | TypeScript | C# |
| --- | ---: | ---: | ---: |
| must keep passing | 13 | 13 | 12 |
| rewrite | 4 | 4 | 4 |
| add | 3 | 3 | 3 |
| remove | 1 | 1 | 0 |

## Must Keep Passing

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
| Cross-language conformance | `langs/python/tests/conformance/test_conformance.py` | `langs/typescript/tests/conformance/conformance.test.ts` | `langs/csharp/tests/GuideArch.Models.Tests/ConformanceTests.cs` |

## Rewrite

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

## Add

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

## Remove

| Exact test file or `None` | Language | Removal condition |
| --- | --- | --- |
| `langs/python/tests/unit/test_vmx_to_nicegui_adapter.py` | Python | Remove only if `langs/python/src/guidearch/view/adapters/vmx_to_nicegui.py` is fully retired and its replacement coverage lives in `test_scenario_form_vm.py` plus `test_scenario_observable_lists.py`. |
| `langs/typescript/tests/unit/vmx-to-svelte.test.ts` | TypeScript | Remove only if `langs/typescript/src/view/adapters/vmx-to-svelte.ts` is deleted and Svelte bridge coverage moves to the replacement observable-list suites. |
| `None` | C# | Prefer rewrites of existing xUnit suites; no outright deletion is required for the first VMx 3.1 refactor wave. |

## Verification Commands

| Scope | Command | Expected result |
| --- | --- | --- |
| Python VM tests | `cd langs/python && uv run pytest tests/unit/test_app_vm.py tests/unit/test_scenario_vm.py tests/unit/test_editor_cascades.py tests/unit/test_selection_state.py tests/unit/test_vmx_to_nicegui_adapter.py tests/unit/test_vm_mvvm.py tests/unit/test_vm_tree.py tests/integration/test_vm_tree_comprehensive.py -q` | All targeted VM/viewmodel tests pass with no new replacement-regression failures. |
| Python conformance | `cd langs/python && uv run pytest tests/conformance/test_conformance.py -q` | The conformance suite passes against the same scenario corpus after the replacement. |
| TypeScript tests | `cd langs/typescript && pnpm test -- tests/unit/app-vm.test.ts tests/unit/scenario-vm.test.ts tests/unit/editor-cascades.test.ts tests/unit/selection-state.test.ts tests/unit/vmx-to-svelte.test.ts tests/integration/vm-mvvm.test.ts tests/integration/vm-tree-comprehensive.test.ts` | All targeted Vitest suites pass for the replacement surface. |
| TypeScript conformance | `cd langs/typescript && pnpm conformance` | The TypeScript conformance runner exits 0 with the current fixtures. |
| C# tests | `cd langs/csharp && dotnet test tests/GuideArch.ViewModels.Tests/GuideArch.ViewModels.Tests.csproj --nologo` | The ViewModels test project passes with replacement-specific rewrites and additions in place. |
| C# conformance | `cd langs/csharp && dotnet test tests/GuideArch.Models.Tests/GuideArch.Models.Tests.csproj --nologo --filter FullyQualifiedName~ConformanceTests` | The conformance subset passes against the shared scenario corpus. |
