# VMx 3.1 Refactor Audit Design

**Date:** 2026-07-02
**Branch:** `codex/vmx-3-1-refactor-audit`
**Status:** Approved for implementation planning

## Purpose

GuideArch currently uses VMx heavily in its ViewModel layer across Python,
TypeScript, and C#. VMx has advanced to the 3.1 line, adding more specialized
ViewModel, command, collection, form, dialog, and state abstractions than the
older VMx surface GuideArch was written against.

This work will produce a comprehensive, evidence-backed migration and refactor
report. The report will identify which current GuideArch ViewModel and view
adapter code can be replaced by better-fitting VMx 3.1 abstractions, quantify
the expected code reduction, and define the test coverage changes needed to
keep all three implementations behaviorally aligned.

## Scope

The audit covers all three implementations equally:

- Python / NiceGUI, using the PyPI `vmx` package.
- TypeScript / Svelte + Tauri, using VMx from the `vendor/vmx` submodule.
- C# / Avalonia, using VMx from the `vendor/vmx` submodule.

The first implementation step is to update the branch's VMx references so the
audit studies the correct surface:

- Python: bump the package constraint and lockfile to `vmx==3.1.0` from PyPI.
- TypeScript: use latest VMx `main` as the source reference because npm is not
  published for this release line.
- C#: use latest VMx `main` as the source reference because NuGet is not
  published for this release line.

The audit may update the `vendor/vmx` submodule to the latest VMx `main` commit
if the implementation plan chooses to validate TypeScript and C# directly
against the new source. The report must record the exact VMx commit used.

## Deliverables

The implementation will produce a committed Markdown report under
`docs/superpowers/specs/` or another docs location chosen during planning. The
report must include:

1. Executive summary of the strongest VMx 3.1 replacement opportunities.
2. Dependency and source state for Python, TypeScript, and C#.
3. Current GuideArch VMx usage inventory.
4. VMx 3.1 capability inventory limited to components relevant to GuideArch.
5. One-by-one replacement matrix.
6. Replacement ledger with LOC accounting.
7. Test coverage impact matrix.
8. Recommended refactor phases.
9. Non-goals and rejected candidates.

The report is an audit and plan, not the full refactor implementation. It can
include small compatibility probes or compile/test checks needed to prove a
candidate is viable, but broad production refactors should happen in later
implementation slices.

## Investigation Method

### 1. Establish Baseline

Record the starting state:

- Current GuideArch branch and commit.
- Current Python VMx constraint and resolved lockfile version.
- Current TypeScript VMx dependency and lockfile source.
- Current C# VMx source mode and version metadata.
- Current `vendor/vmx` submodule commit.
- Latest VMx PyPI version and metadata.
- Latest VMx `main` commit and per-language version metadata.

### 2. Inventory Current Usage

Build a cross-language inventory of VMx use in GuideArch:

- Leaf VM factories/classes:
  `DecisionVM`, `AlternativeVM`, `PropertyVM`, `CoefficientVM`,
  constraints, candidates, critical decisions, and critical constraints.
- Root state and command VMs:
  `ScenarioVM`, `AppVM`, `ScenarioState`, and `AppState`.
- Mutation orchestration:
  add, update, delete, cascade, dirty tracking, re-solve, selection state,
  warning/status updates, and save/open flows.
- Collection and indexing patterns:
  candidates, coefficients, constraints, per-kind filtered views, and chart
  selection.
- Adapter/view glue:
  VMx-to-Svelte stores, VMx-to-NiceGUI binding helpers, Avalonia binding
  assumptions, command-to-button adapters, dialogs, and confirmation flows.
- Existing tests:
  unit, integration, VM tree, adapter, cascade, selection, save/open, and
  conformance tests that cover VM behavior.

### 3. Inventory VMx 3.1 Candidates

Study the VMx 3.1 API and specs in all three languages, focusing on components
that could replace current GuideArch code:

- `ComponentVM`, `ComponentVMOf`, readonly modeled components, and builder
  conveniences.
- `CompositeVM`, `CompositeVMOf`, `GroupVM`, and `AggregateVM1..6`.
- `FilteredCompositeVM` and `ScoredFilteredCompositeVM`.
- `ObservableList`, `ServicedObservableCollection`, `ObservableDictionary`,
  `PagedComposition`, and token paging helpers.
- `RelayCommand`, `RelayCommandOf`, `AsyncRelayCommand`, `CompositeCommand`,
  `DecoratorCommand`, `ConfirmationDecoratorCommand`, fluent command helpers,
  and `ModeledCrudCommands`.
- `FormVM`, form builders, snapshot/revert behavior, and form persistence
  interfaces.
- `DerivedProperty` and property-value-changed / when-property-changed helpers.
- Dialog, modal, notification, and confirmation ViewModels/services.
- `DiscriminatorVM` and state helper abstractions.
- Capabilities such as selection, search, filtering, paging, expansion,
  lifecycle, CRUD, and dialog capabilities.
- Tree/hierarchical utilities where they can express GuideArch's conceptual
  VM tree without overfitting.

### 4. Map Replacements One By One

For each current GuideArch pattern, compare the old approach against the VMx
3.1 candidates and choose one of these outcomes:

- **Replace:** VMx 3.1 has a better-fitting abstraction with acceptable risk.
- **Partially replace:** VMx 3.1 can remove part of the bespoke code, but
  GuideArch-specific behavior remains.
- **Keep:** the current code is simpler, clearer, or more domain-specific than
  the available VMx abstraction.
- **Defer:** viable but too broad for the first refactor wave.

Each decision must include rationale, affected files, behavior preserved,
cross-language availability, risk, and required tests.

## Replacement Ledger And LOC Metrics

The final report must include a ledger with one row per migration candidate:

| Field | Meaning |
|---|---|
| Current pattern | Existing GuideArch code or old VMx usage being considered |
| Replacement | VMx 3.1 abstraction or "keep/defer" decision |
| Language(s) | Python, TypeScript, C#, or all |
| Files | Affected production and test files |
| Deleted production LOC | Lines expected to disappear from ViewModel/view code |
| Added production LOC | New glue or wrapper code required |
| Net production LOC | Deleted minus added, excluding docs and generated files |
| Test LOC delta | Test lines deleted, added, or rewritten |
| Behavior coverage | Existing and new tests proving behavior |
| Risk | Low, medium, or high, with reason |

LOC accounting rules:

- Count ViewModel production code separately from view/adapter/UI glue.
- Count tests separately from production code.
- Exclude docs, generated files, caches, build outputs, lockfile churn, and
  formatting-only changes from savings metrics.
- Use stable counting commands such as `cloc`, `tokei`, or a documented `wc -l`
  fallback over explicit file lists.
- Record both baseline and projected post-refactor numbers so the final report
  can answer how much the newer VMx surface improves GuideArch compared with
  the older usage.

The report should summarize:

- ViewModel production LOC before and projected after.
- View/adapter production LOC before and projected after.
- Total production LOC saved.
- Test LOC delta.
- Confidence notes for each estimate.

## Test Coverage Impact

The audit must produce a test coverage impact matrix with these categories:

- **Must keep passing:** existing tests that validate user-visible behavior and
  cross-language parity.
- **Rewrite:** tests that assert old implementation details instead of stable
  behavior.
- **Add:** tests needed for new VMx-backed behavior.
- **Remove:** tests made obsolete because VMx now owns the behavior, provided
  equivalent behavior is covered through integration tests.

Coverage must include:

- VM construction and lifecycle behavior.
- Property change and property-value change propagation.
- Dirty tracking and re-solve triggers.
- Add/update/delete cascades.
- Save/open failure behavior and warnings.
- Candidate selection behavior.
- Command enablement, confirmation, and async command behavior where adopted.
- Collection mutation notifications and filtered views where adopted.
- Form snapshot/revert semantics where adopted.
- Cross-language conformance and VM tree parity.

The report should name exact test files to keep, add, rewrite, or delete.

## Recommended Refactor Phases

The report should group candidates into phases:

1. **Dependency and compatibility phase:** bump Python to `vmx==3.1.0`, align
   VMx main reference for TypeScript/C#, and run baseline verification.
2. **Safe mechanical replacements:** helper imports, builder conveniences,
   property-change helpers, command wrappers, and null service improvements.
3. **Collection and composite cleanup:** replace bespoke list/index/view logic
   with VMx collections, groups, composites, filters, and aggregates where
   they reduce code without hiding GuideArch-specific behavior.
4. **Form and command cleanup:** evaluate `FormVM`, `ModeledCrudCommands`,
   command decorators, and confirmation/dialog services for editor and file
   workflows.
5. **View adapter simplification:** replace hand-written VM-to-view glue where
   VMx 3.1 now exposes a cleaner primitive.
6. **Parity and cleanup:** remove obsolete bespoke code and stabilize tests
   across all three implementations.

Each phase must include verification commands and expected test coverage.

## Non-Goals

This design does not authorize a broad production refactor yet. It also does
not change GuideArch's domain model, TOPSIS algorithms, JSON schema, or release
versioning policy. Any VMx feature that would force algorithm/domain changes is
out of scope for this audit.

## Success Criteria

The work is successful when the branch contains a report that:

- Covers Python, TypeScript, and C# equally.
- Records the VMx 3.1 PyPI package state and latest VMx main commit used.
- Maps current GuideArch VM/ViewModel usage to VMx 3.1 candidates one by one.
- Identifies concrete replacement, partial replacement, keep, and defer
  decisions.
- Quantifies expected production LOC savings in ViewModel and view/adapter code.
- Separately accounts for test LOC changes.
- Names the test coverage changes required for each replacement.
- Defines a phased implementation path that can be converted into a detailed
  development plan.
