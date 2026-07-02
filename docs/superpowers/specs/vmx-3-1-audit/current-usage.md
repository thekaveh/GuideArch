# Current GuideArch VMx Usage Inventory

## Summary

| Language | Primary VMx usage | Largest bespoke areas | Parity notes |
| --- | --- | --- | --- |
| Python | Hybrid approach: leaf editors subclass `ComponentVMOf`, but `ScenarioVM` and `AppVM` hand-roll command wiring, `property_changed`, and `MessageHub` publication. | `scenario_vm.py`, `app_vm.py`, `view/adapters/vmx_to_nicegui.py`, and the subscription-heavy portions of `main.py`. | Python has the most bespoke VMx surface and already shows 3.1 compatibility pressure around `RelayCommandOfT`. |
| TypeScript | Broad direct VMx usage: `ComponentVMOf` builders for every VM, `RelayCommand` / `RelayCommandOf` for root commands, `MessageHub`-driven Svelte adapters. | `scenario-vm.ts`, `app-vm.ts`, `view/adapters/vmx-to-svelte.ts`, and route components that fan VM state into stores. | TS stays closest to VMx primitives, but the Svelte store bridge creates broad view coupling to `PropertyChangedMessage` semantics. |
| C# | Factory-first usage: every VM is a `ComponentVM<T>`, with root factories attaching `RelayCommand` commands and Avalonia views binding through open-generic `ComponentVM<T>`. | `ScenarioVMFactory.cs`, `AppVMFactory.cs`, `MainWindow.axaml`, and `MainWindow.axaml.cs`. | C# is structurally aligned with VMx, but reflection binding and code-behind refresh logic amplify migration cost in the view layer. |

## Python

### ViewModels

| File | Current VMx abstraction | Bespoke behavior | Replacement pressure |
| --- | --- | --- | --- |
| `langs/python/src/guidearch/viewmodels/__init__.py` | Documentation-only module references `ComponentVMOf` shape in the exported tree. | No runtime coupling; acts as a map of the VM hierarchy. | low |
| `langs/python/src/guidearch/viewmodels/alternative_vm.py` | `AlternativeVM` subclasses `ComponentVMOf[AlternativeM]` and accepts shared `MessageHub` / `Dispatcher`. | Thin editable wrapper around model fields; relies on VMx for property publication. | medium |
| `langs/python/src/guidearch/viewmodels/app_vm.py` | Custom `AppVM` owns a VMx `MessageHub`, emits `PropertyChangedMessage`, and exposes `RelayCommandOfT[str]`. | Reimplements root-VM state, warning accumulation, theme registry, and `property_changed` observable without inheriting `ComponentVMOf`. | high |
| `langs/python/src/guidearch/viewmodels/candidate_vm.py` | No direct VMx primitive; plain read-only wrapper around `CandidateM`. | Result projection is bespoke and intentionally avoids commands / hub wiring. | low |
| `langs/python/src/guidearch/viewmodels/coefficient_vm.py` | `CoefficientVM` subclasses `ComponentVMOf[CoefficientM]` with shared `MessageHub` / `Dispatcher`. | Thin leaf editor; solve-trigger semantics are implicit in upstream hub listeners. | medium |
| `langs/python/src/guidearch/viewmodels/constraint_vm.py` | Three leaf classes subclass `ComponentVMOf` for threshold / dependency / conflict constraints. | File centralizes three constraint flavors and depends on shared hub propagation to drive re-solve behavior. | medium |
| `langs/python/src/guidearch/viewmodels/critical_constraint_vm.py` | No direct VMx primitive; plain read-only wrapper around `CriticalConstraintM`. | Bespoke result projection with no hub or command semantics. | low |
| `langs/python/src/guidearch/viewmodels/critical_decision_vm.py` | No direct VMx primitive; plain read-only wrapper around `CriticalDecisionM`. | Bespoke result projection with no hub or command semantics. | low |
| `langs/python/src/guidearch/viewmodels/decision_vm.py` | `DecisionVM` subclasses `ComponentVMOf[DecisionM]` with shared `MessageHub` / `Dispatcher`. | Thin leaf editor whose mutation notifications are delegated to VMx. | medium |
| `langs/python/src/guidearch/viewmodels/property_vm.py` | `PropertyVM` subclasses `ComponentVMOf[PropertyM]` with shared `MessageHub` / `Dispatcher`. | Thin leaf editor for weight / kind edits; depends on VMx change publication. | medium |
| `langs/python/src/guidearch/viewmodels/scenario_vm.py` | Custom root `ScenarioVM` uses VMx `RelayCommand`, `RelayCommandOfT`, `MessageHub`, and `PropertyChangedMessage`, but does not inherit `ComponentVMOf`. | Largest bespoke VM layer in the repo: command lifecycle, mutable state transitions, solve orchestration, child-VM fan-out, manual `property_changed`, and hub rebroadcasting all live here. | high |

### View/Adapter Glue

| File | Current VMx abstraction | Bespoke behavior | Replacement pressure |
| --- | --- | --- | --- |
| `langs/python/src/guidearch/view/adapters/vmx_to_nicegui.py` | Adapter subscribes to `vm.property_changed` and binds NiceGUI buttons to `RelayCommand` / `RelayCommandOfT`. | Hand-written bridge from VMx observable and command semantics into NiceGUI callbacks and refreshes. | high |
| `langs/python/src/guidearch/main.py` | View layer subscribes directly to `app_vm.property_changed` and `vm.property_changed` to drive theme, save-button state, tab rerenders, and results refresh. | Process-global singleton VMs, per-client subscription cleanup, and full panel rerender logic sit outside the VM layer and depend on current property names. | high |

## TypeScript

### ViewModels

| File | Current VMx abstraction | Bespoke behavior | Replacement pressure |
| --- | --- | --- | --- |
| `langs/typescript/src/viewmodels/alternative-vm.ts` | `ComponentVMOf<AlternativeM>` builder with shared `MessageHub` and `NullDispatcher`. | Thin editable leaf; custom behavior is mostly naming and model hinting. | medium |
| `langs/typescript/src/viewmodels/app-vm.ts` | Root `AppVM` is `ComponentVMOf<AppState>` plus `RelayCommandOf<string>` and injected `MessageHub`. | Theme registration, persistence callbacks, root-mode metadata, and app-shell composition are layered on top of VMx. | high |
| `langs/typescript/src/viewmodels/candidate-vm.ts` | `ComponentVMOf<CandidateM>` builder with `NullMessageHub` and `NullDispatcher`. | Read-only result VM still depends on VMx construction and modeled hints. | medium |
| `langs/typescript/src/viewmodels/coefficient-vm.ts` | `ComponentVMOf<CoefficientM>` builder with shared `MessageHub`. | Thin editable leaf with minimal bespoke logic beyond naming. | medium |
| `langs/typescript/src/viewmodels/constraint-vm.ts` | `ComponentVMOf<ConstraintM>` builder with shared `MessageHub`. | Thin editable leaf; constraint flavor differences stay in model data rather than VM code. | medium |
| `langs/typescript/src/viewmodels/critical-constraint-vm.ts` | `ComponentVMOf<CriticalConstraintM>` builder with `NullMessageHub` / `NullDispatcher`. | Read-only criticality projection depends on VMx construction but little else. | medium |
| `langs/typescript/src/viewmodels/critical-decision-vm.ts` | `ComponentVMOf<CriticalDecisionM>` builder with `NullMessageHub` / `NullDispatcher`. | Read-only criticality projection depends on VMx construction but little else. | medium |
| `langs/typescript/src/viewmodels/decision-vm.ts` | `ComponentVMOf<DecisionM>` builder with shared `MessageHub`. | Thin editable leaf with low bespoke logic. | medium |
| `langs/typescript/src/viewmodels/index.ts` | Re-export barrel for VM types and factories; no direct `vmx` import. | No runtime coupling beyond organizing the VM surface. | low |
| `langs/typescript/src/viewmodels/property-vm.ts` | `ComponentVMOf<PropertyM>` builder with shared `MessageHub`. | Thin editable leaf with low bespoke logic. | medium |
| `langs/typescript/src/viewmodels/scenario-vm.ts` | Root `ScenarioVM` is `ComponentVMOf<ScenarioState>` extended with `RelayCommand`, `RelayCommandOf<string>`, a live `MessageHub`, and command fields. | Largest TS VMx concentration: mutable scenario workflow, solve orchestration, command predicates, child VM creation, and state rebasing all depend on VMx APIs. | high |

### View/Adapter Glue

| File | Current VMx abstraction | Bespoke behavior | Replacement pressure |
| --- | --- | --- | --- |
| `langs/typescript/src/view/adapters/vmx-to-svelte.ts` | `vmxToStore` and helpers translate `ComponentVMOf`, `PropertyChangedMessage`, and `RelayCommand` semantics into Svelte stores and button handlers. | The adapter assumes VMx property publication details, especially whole-model `"model"` rebroadcast behavior. | high |
| `langs/typescript/src/routes/+page.svelte` | Root page consumes `vmxToStore(app, 'theme')` and `vmxToStore(vm, 'scenario')`. | Theme synchronization and empty-state gating are wired directly to VMx-backed stores. | high |
| `langs/typescript/src/routes/lib/AlternativesTab.svelte` | Consumes `vmxToStore(vm, 'scenario')`. | Derives decisions and alternatives from the VM-backed scenario store. | medium |
| `langs/typescript/src/routes/lib/CoefficientsTab.svelte` | Consumes `vmxToStore(vm, 'scenario')`. | Entire coefficients editor reads through the VM-backed scenario store. | medium |
| `langs/typescript/src/routes/lib/ConstraintsTab.svelte` | Consumes `vmxToStore(vm, 'scenario')`. | Constraint editor derives all rows from a VM-backed scenario store. | medium |
| `langs/typescript/src/routes/lib/CriticalConstraintsTab.svelte` | Consumes `vmxToStore(vm, 'criticalConstraints')` and `vmxToStore(vm, 'scenario')`. | Criticality table depends on VM-owned computed collections plus scenario presence checks. | medium |
| `langs/typescript/src/routes/lib/CriticalDecisionsTab.svelte` | Consumes `vmxToStore(vm, 'criticalDecisions')` and `vmxToStore(vm, 'scenario')`. | Criticality ranking view depends on VM-owned computed collections plus scenario-derived labels. | medium |
| `langs/typescript/src/routes/lib/DecisionsTab.svelte` | Consumes `vmxToStore(vm, 'scenario')`. | Decision editor derives rows from a VM-backed scenario store. | medium |
| `langs/typescript/src/routes/lib/PropertiesTab.svelte` | Consumes `vmxToStore(vm, 'scenario')`. | Property editor derives rows from a VM-backed scenario store. | medium |
| `langs/typescript/src/routes/lib/ResultsTab.svelte` | Consumes `vmxToStore(vm, 'candidates')`, `vmxToStore(vm, 'scenario')`, and `vmxToStore(vm, 'selectedCandidateIndex')`. | Results view has broad coupling to VM-owned solved collections, selection state, and chart inputs. | high |
| `langs/typescript/src/routes/lib/StatusBar.svelte` | Consumes `vmxToStore` for `scenario`, `status`, `warnings`, `isDirty`, `filePath`, and `candidates`. | Dense fan-out from VM state into multiple UI chips and counters. | high |
| `langs/typescript/src/routes/lib/Toolbar.svelte` | Consumes `vmxToStore(app, 'theme')` and `vmxToStore(vm, 'scenario')`. | Command enablement, sample gating, and theme toggle all read current VM-backed state while dispatching root commands. | high |

## C#

### ViewModels

| File | Current VMx abstraction | Bespoke behavior | Replacement pressure |
| --- | --- | --- | --- |
| `langs/csharp/src/GuideArch.ViewModels/ScenarioVMFactory.cs` | Root factory builds `ComponentVM<ScenarioState>`, attaches `RelayCommand` / `RelayCommand<string>`, and uses `NullMessageHub` / `NullDispatcher`. | Biggest C# VM hotspot: state rebasing, solve workflow, file lifecycle, mutator plumbing, and command cache attachment all sit around VMx builder APIs. | high |
| `langs/csharp/src/GuideArch.ViewModels/AppVMFactory.cs` | Root factory builds `ComponentVM<AppState>`, attaches theme `RelayCommand<string>`, and composes a child `ComponentVM<ScenarioState>`. | Owns theme registry, app-shell state, command cache, and rebase from app root to scenario child. | high |
| `langs/csharp/src/GuideArch.ViewModels/AlternativeVMFactory.cs` | Leaf factory builds `ComponentVM<AlternativeM>` with `NullMessageHub` / `NullDispatcher`. | Thin wrapper, but one of several repetitive factory sites that would all need a replacement path. | medium |
| `langs/csharp/src/GuideArch.ViewModels/CandidateVMFactory.cs` | Leaf factory builds `ComponentVM<CandidateM>` with `NullMessageHub` / `NullDispatcher`. | Simple result wrapper with low bespoke logic but direct VMx construction. | medium |
| `langs/csharp/src/GuideArch.ViewModels/CoefficientVMFactory.cs` | Leaf factory builds `ComponentVM<CoefficientM>` with `NullMessageHub` / `NullDispatcher`. | Thin editable wrapper repeated across leaf VM factories. | medium |
| `langs/csharp/src/GuideArch.ViewModels/ConstraintVMFactory.cs` | Leaf factory builds `ComponentVM<ConstraintM>` with `NullMessageHub` / `NullDispatcher`. | Thin editable wrapper whose naming convention and indexing are bespoke. | medium |
| `langs/csharp/src/GuideArch.ViewModels/CriticalConstraintVMFactory.cs` | Leaf factory builds `ComponentVM<CriticalConstraintM>` with `NullMessageHub` / `NullDispatcher`. | Simple result wrapper repeated across criticality VMs. | medium |
| `langs/csharp/src/GuideArch.ViewModels/CriticalDecisionVMFactory.cs` | Leaf factory builds `ComponentVM<CriticalDecisionM>` with `NullMessageHub` / `NullDispatcher`. | Simple result wrapper repeated across criticality VMs. | medium |
| `langs/csharp/src/GuideArch.ViewModels/DecisionVMFactory.cs` | Leaf factory builds `ComponentVM<DecisionM>` with `NullMessageHub` / `NullDispatcher`. | Thin editable wrapper repeated across leaf VM factories. | medium |
| `langs/csharp/src/GuideArch.ViewModels/PropertyVMFactory.cs` | Leaf factory builds `ComponentVM<PropertyM>` with `NullMessageHub` / `NullDispatcher`. | Thin editable wrapper repeated across leaf VM factories. | medium |
| `langs/csharp/src/GuideArch.ViewModels/ScenarioState.cs` | State record is not a VMx type, but its comments and binding accommodations explicitly target `ComponentVM<ScenarioState>` consumption. | Includes view-facing derived properties and notes to support open-generic Avalonia binding limitations. | medium |

### View Glue

| File | Current VMx abstraction | Bespoke behavior | Replacement pressure |
| --- | --- | --- | --- |
| `langs/csharp/src/GuideArch.View/MainWindow.axaml` | Avalonia view binds against open-generic `ComponentVM<ScenarioState>` data context via `ReflectionBinding`. | Binding paths, theme toggle assumptions, and UI structure are all arranged around VMx-wrapped state rather than plain view models. | high |
| `langs/csharp/src/GuideArch.View/MainWindow.axaml.cs` | Code-behind holds `ComponentVM<AppState>` and `ComponentVM<ScenarioState>`, subscribes to `PropertyChanged`, and routes commands via `AppVMFactory.GetCommands` / `ScenarioVMFactory.GetCommands`. | Theme application, chart refresh, status updates, file dialogs, and result-grid synchronization all depend on current VMx event and state semantics. | high |

## Cross-Language Hot Spots

| Area | Python | TypeScript | C# | Replacement pressure |
| --- | --- | --- | --- | --- |
| Root scenario orchestration | `ScenarioVM` is mostly bespoke and manually republishes change events through VMx messages. | `scenario-vm.ts` stays inside `ComponentVMOf`, but layers a large command-and-state workflow on top. | `ScenarioVMFactory` centralizes orchestration around `ComponentVM<ScenarioState>` and cached command attachments. | high |
| Leaf VM repetition | Mixed: some plain wrappers, some `ComponentVMOf` subclasses with shared hub / dispatcher args. | Consistent builder pattern across all leaf VMs, including read-only result VMs. | Repetitive factory-per-leaf pattern across nine files. | medium |
| Collections and filtered views | Scenario root recomputes candidates, critical decisions, and critical constraints, then pushes manual `property_changed` names. | Scenario root exposes solved collections through VMx state; Svelte routes slice and sort via stores. | Scenario root owns solved collections; code-behind and AXAML project them into tables and charts. | high |
| Commands and confirmation | VM layer uses `RelayCommand` / `RelayCommandOfT`, while NiceGUI glue adds toolbar and discard-confirm behavior. | Commands remain VMx-native and UI components gate interaction off store state. | Commands remain VMx-native and code-behind routes picker / theme actions around them. | high |
| Form/edit lifecycle | NiceGUI rerenders panels based on string property names from `property_changed`. | Svelte components react through `vmxToStore` to scenario and selection changes. | Avalonia relies on `PropertyChanged` from open-generic component VMs plus manual redraw logic. | high |
| View bindings and adapters | `vmx_to_nicegui.py` and `main.py` create a custom bridge from observables to imperative UI refresh. | `vmx-to-svelte.ts` is a dedicated store bridge consumed widely across route components. | AXAML reflection binding and `MainWindow.axaml.cs` glue the view directly to VMx component VMs. | high |
