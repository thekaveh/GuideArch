# VMx 3.1 Capability Inventory For GuideArch

## Source State

| Language | Version evidence | Source evidence | Notes |
| --- | --- | --- | --- |
| Python | `vendor/vmx/langs/python/src/vmx/__about__.py` reports `__version__ = "3.1.0"` and `__min_spec_version__ = "3.1.0"`. | `vendor/vmx/langs/python/src/vmx` contains the full capability surface: commands, components, composites, groups, aggregates, collections, dialogs, forms, notifications, properties, and state helpers. | Public API re-export lives in `vendor/vmx/langs/python/src/vmx/__init__.py`. Python uses `RelayCommandOf` in 3.1; the legacy `RelayCommandOfT` alias is gone. |
| TypeScript | `vendor/vmx/langs/typescript/package.json` and `vendor/vmx/langs/typescript/src/version.ts` both report `3.1.0` and min spec `3.1.0`. | `vendor/vmx/langs/typescript/src` ships matching commands, components, composites, groups, aggregates, collections, dialogs, forms, notifications, properties, capabilities, and state helpers. | `npm package not published` for this release line, so TypeScript uses the latest VMx `main` checkout in `vendor/vmx` as the source reference. Public API re-export lives in `vendor/vmx/langs/typescript/src/index.ts`. TypeScript stays closest to the spec naming (`CompositeVMOf`, `RelayCommandOf`, `FormVMBuilder`). |
| C# | `vendor/vmx/langs/csharp/src/VMx/VMx.csproj` reports `Version=3.1.0` and `MinSpecVersion=3.1.0`. | `vendor/vmx/langs/csharp/src/VMx` ships matching commands, components, composites, groups, aggregates, collections, dialogs, forms, properties, capabilities, and state helpers. | `NuGet package not published` for this release line, so C# uses the latest VMx `main` checkout in `vendor/vmx` as the source reference. C# often uses idiomatic names that differ slightly from the spec table, such as `CompositeVMOfM<M, VM>` and `RelayCommand<T>`. |

## Candidate Capabilities

| Capability | Python | TypeScript | C# | GuideArch fit | Notes |
| --- | --- | --- | --- | --- | --- |
| component builder conveniences | yes | yes | yes | strong | GuideArch already leans on builder or factory composition for nearly every VM. This is the safest VMx surface to standardize further. |
| readonly modeled components | yes (`ReadonlyComponentVMOf`, `ReadonlyComponentVMOfBuilder`) | yes (`ReadonlyComponentVMOf`, `ReadonlyComponentVMOfBuilder`) | yes (`ReadonlyComponentVM<M>`, `ReadonlyComponentVMBuilder<M>`) | medium | Good fit for result-only wrappers such as candidates and criticality rows. Useful, but not the highest-leverage refactor because those wrappers are already fairly thin. The C# handle is `ReadonlyComponentVM`, while Python and TypeScript expose the modeled `ReadonlyComponentVMOf` form. |
| CompositeVM | yes | yes | yes | medium | Helpful if GuideArch wants first-class child ownership plus current-selection semantics for editable lists. Current roots mostly hand-roll list orchestration instead. |
| CompositeVMOf | yes | yes | yes | medium | Most relevant if scenario editing is remodeled around child collections built from models rather than bespoke list rebuilding. C# surface is named `CompositeVMOfM<M, VM>`. |
| GroupVM | yes | yes | yes | weak | Group ownership without selection is available everywhere, but GuideArch does not currently show a strong need for grouped lifecycle orchestration distinct from composites or plain state records. |
| AggregateVM1 | yes | yes | yes | weak | Present in all three languages, but a one-slot aggregate does not buy much over the existing root VM patterns. |
| AggregateVM2 | yes | yes | yes | weak | Could bundle a small fixed set of child VMs, yet GuideArch mostly uses record state plus commands instead of fixed-slot VM ownership. |
| AggregateVM3 | yes | yes | yes | weak | Same story as `AggregateVM2`: available, but not a strong answer to the current migration pressure. |
| AggregateVM4 | yes | yes | yes | weak | Fixed-slot aggregates are technically complete across languages, but the GuideArch shape is more collection-heavy than slot-heavy. |
| AggregateVM5 | yes | yes | yes | weak | Available everywhere; no obvious near-term win over existing root factories and builders. |
| AggregateVM6 | yes | yes | yes | weak | Same as the other aggregate variants: present, but not a priority for this codebase. |
| FilteredCompositeVM | yes | yes | yes | medium | Worth considering for derived filtered lists and selection-aware slices, especially in results or criticality views. It becomes more attractive only if GuideArch first adopts composite-backed child collections. |
| ScoredFilteredCompositeVM | yes | yes | yes | medium | Potential fit for ranked candidate and criticality surfaces because scoring is already core domain behavior. Same dependency as `FilteredCompositeVM`: it pays off more once children are real composite members. |
| ObservableList | yes | yes | yes | strong | Strongest collection primitive for GuideArch. It can replace a lot of coarse rerender behavior with granular list mutation signals in all three UIs. |
| ServicedObservableCollection | yes | yes | yes | medium | Useful where local collection events should also hit a hub, but GuideArch more often needs UI-facing collection diffs than hub fan-out alone. |
| ObservableDictionary | yes | yes | yes | medium | Could help keyed caches such as command registries, route lookups, or memoized results, but it is not a direct answer to the current scenario-editor pain points. |
| PagedComposition | yes | yes | yes | weak | Finite index paging exists across all three languages, but GuideArch data sets are local and small enough that paging is not an immediate need. |
| token paging | yes | yes | yes | weak | Token paging is complete in all three flavors, but it targets remote or incremental page sources that GuideArch does not currently expose. |
| RelayCommand | yes | yes | yes | strong | Already central to GuideArch roots in every language. This remains a strong standard surface for command wiring. |
| RelayCommandOf | yes | yes | yes | strong | Strong fit for parameterized open/save/theme actions. Python note: 3.1 uses `RelayCommandOf`, not `RelayCommandOfT`; C# idiom is `RelayCommand<T>`. |
| AsyncRelayCommand | yes | yes | yes | medium | Could reduce bespoke async save, open, confirm, or solve plumbing, especially in UI-facing roots. Current GuideArch code still uses mostly synchronous relay commands. |
| CompositeCommand | yes | yes | yes | medium | Useful when a single UI action should fan out to multiple side effects. Worth keeping in the candidate set, but current GuideArch command graphs are not yet composition-heavy. |
| DecoratorCommand | yes | yes | yes | medium | Attractive for adding pre/post behavior around existing commands without more root-VM branching. Good tactical tool, not a structural migration anchor. |
| fluent command helpers | yes | yes | yes | medium | Low-risk ergonomics for decorator usage. Helpful once command composition is adopted, but not independently transformative. |
| ConfirmationDecoratorCommand | yes | yes | yes | medium | Good fit for destructive actions such as reset, discard, or delete. More compelling once GuideArch normalizes confirmation flows across platforms. |
| ModeledCrudCommands | yes | yes | yes | strong | High fit for scenario child editing because GuideArch already has add/update/delete-style flows around alternatives, decisions, properties, and constraints. |
| FormVM | yes | yes | yes | strong | One of the best candidates for GuideArch. It directly addresses dirty tracking, approve/deny flows, validation, and persistence semantics that are currently bespoke in root editors. |
| form builders | yes (`FormVMBuilder[TM]`) | yes (`FormVMBuilder<TM>`) | yes (`FormVMBuilder<TM>`) | strong | The builder surface makes `FormVM` adoption practical and consistent with the rest of the codebase's factory style. `FormVM.builder()` / `FormVM.Builder()` is concrete enough to track directly in the replacement ledger. |
| DerivedProperty | yes | yes | yes | strong | Strong fit for warnings, dirty flags, status text, selected result projections, and similar computed values now being raised manually. |
| property-value change helpers | yes | yes | yes | medium | Useful for trigger plumbing and adapter subscriptions. Good supporting primitive, especially if command enablement or derived state becomes more reactive. |
| dialog services | yes | yes | yes | medium | Worth using for file dialogs and confirmation bridges, but GuideArch's current dialog logic is still mostly view-layer glue rather than VM-level abstraction. |
| modal services | yes | yes | yes | medium | Available in each flavor, though the public shape differs a bit. Could help if GuideArch introduces richer modal flows; not a near-term refactor driver. |
| notification services | yes | yes | partial | medium | Python and TypeScript ship explicit notification hubs and VMs. C# currently exposes notification severity plus dialog-related pieces, but not the same standalone notification hub package shape as the other two flavors. |
| DiscriminatorVM | yes | yes | yes | medium | A credible fit for app mode, active pane, or modal precedence. Helpful, though not as urgent as form, collection, and derived-state cleanup. |
| selection capabilities | yes (`ISelectable`, `IDeselectable`, `ISelectionTogglable`) | yes (`ISelectable`, `IDeselectable`, `ISelectionTogglable`) | yes (`ISelectable`, `IDeselectable`, `ISelectionTogglable`) | strong | Strong fit for selected candidate index, active scenario child, and other current-selection state already present in GuideArch. These names are stable enough to map directly into the replacement ledger. |
| filter capabilities | yes (`Filterable[T]`) | yes (`IFilterable<T>`) | yes (`IFilterable<TItem>`) | medium | Fits search/filterable result lists and constraint slicing, but those workflows are not yet rich enough to demand a dedicated capability layer. The cross-language interface handles are concrete even though Python uses the unprefixed abstract base `Filterable`. |
| search capabilities | yes (`ISearchable`, `SearchableState`) | yes (`ISearchable`, `SearchableState<T>`) | yes (`ISearchable`) | medium | Similar to filter capabilities: available everywhere and plausible for future UX, but not a top migration target today. `SearchableState` is the clearest off-the-shelf helper when GuideArch wants more than the bare interface. |
| paging capabilities | yes (`Pageable`, `PagedComposition`, `TokenPagedComposition`) | yes (`IPageable`, `PagedComposition`, `TokenPagedComposition`) | yes (`IPageable`, `PagedComposition<TVM>`, `TokenPagedComposition<TVM, TToken>`) | weak | Complements the paging primitives, but current GuideArch usage does not justify paging abstractions yet. The capability and helper names are explicit enough to track as deferred replacement-ledger options. |

## Strongest Candidates

1. `FormVM` and form builders: these map directly onto GuideArch's bespoke dirty tracking, save/discard, validation, and persistence workflows in the Python `ScenarioVM`, TypeScript `scenario-vm.ts`, and C# `ScenarioVMFactory`.
2. `ModeledCrudCommands`: these align well with the repeated add/update/delete flows around alternatives, decisions, properties, and constraints, while preserving a shared command shape across the three language stacks.
3. `ObservableList`: this is the cleanest path toward reducing broad rerender behavior in NiceGUI, Svelte, and Avalonia by publishing granular collection mutations instead of whole-scenario refreshes.
4. `DerivedProperty`: GuideArch currently republishes a lot of computed state manually. VMx already has a cross-language primitive for those projections.
5. `RelayCommand` and `RelayCommandOf`: these are already the most stable common denominator across all three implementations, so they remain part of the abstraction baseline rather than a migration risk.
6. component builder conveniences: GuideArch is already culturally aligned with immutable builders and factory composition, which makes broader VMx adoption much less invasive than introducing a new construction style.
7. selection capabilities: they fit the existing selected-candidate and active-item patterns and would pair naturally with `CompositeVM` or `ObservableList` adoption later.

## Rejected Or Weak Candidates

- `GroupVM`: valid, but there is no clear GuideArch hotspot where grouped ownership without selection solves the current maintenance burden.
- `AggregateVM1` through `AggregateVM6`: the fixed-slot aggregate pattern is complete in VMx 3.1, but GuideArch's pressure is around editable collections, derived state, and command orchestration rather than slot bundles.
- `PagedComposition`, token paging, and paging capabilities: these are well-supported, yet GuideArch's local scenario sizes do not currently justify paging complexity.
- `filter capabilities` and `search capabilities`: these stay plausible future additions, but they are not among the best near-term refactor targets because GuideArch's current UX is not filter- or search-heavy.
- `notification services`: cross-language parity is weaker here than for the form, command, and collection surfaces, especially because the C# flavor does not mirror the Python and TypeScript notification package shape.
- `CompositeVM`, `CompositeVMOf`, `FilteredCompositeVM`, and `ScoredFilteredCompositeVM`: these are not bad fits, but they are second-wave candidates. They become compelling after the codebase first standardizes forms, CRUD commands, derived properties, and observable collections.
