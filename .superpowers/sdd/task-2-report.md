# Task 2 Report - VMx 3.1 baseline LOC and current-usage inventory

## Scope

- Read the task brief at `.superpowers/sdd/task-2-brief.md`.
- Measured baseline LOC only.
- Inventoried current GuideArch VMx usage only.
- Did not modify dependency files, production code, or unrelated untracked docs from 2026-06-22.
- Did not attempt to fix known Python or TypeScript VMx 3.1 compatibility failures.

## Repository context

- Branch at start: `codex/vmx-3-1-refactor-audit`
- Start commit: `3b077d0`
- Unrelated untracked files intentionally left alone:
  - `docs/superpowers/plans/2026-06-22-ui-ux-elevation-editors-tables.md`
  - `docs/superpowers/plans/2026-06-22-ui-ux-elevation-foundation.md`
  - `docs/superpowers/plans/2026-06-22-ui-ux-elevation-loose-ends-docs.md`
  - `docs/superpowers/plans/2026-06-22-ui-ux-elevation-overlays-results.md`
  - `docs/superpowers/plans/2026-06-22-ui-ux-elevation-shell-nav.md`
  - `docs/superpowers/specs/2026-06-22-ui-ux-elevation-design.md`

## Step 1 - LOC baseline

Executed the brief's command verbatim and wrote:

- `docs/superpowers/specs/vmx-3-1-audit/loc-baseline.txt`

Key totals captured:

- ViewModel production files: `5436 total`
- View/adapter production files: `9109 total`
- VM/View tests: `12892 total`

Largest files observed from the baseline:

- `langs/python/src/guidearch/viewmodels/scenario_vm.py` - `1139`
- `langs/csharp/src/GuideArch.ViewModels/ScenarioVMFactory.cs` - `983`
- `langs/typescript/src/viewmodels/scenario-vm.ts` - `829`
- `langs/csharp/src/GuideArch.View/MainWindow.axaml.cs` - `1341`
- `langs/csharp/src/GuideArch.View/MainWindow.axaml` - `1153`
- `langs/typescript/src/routes/lib/ConstraintsTab.svelte` - `642`

## Step 2 - VMx usage evidence capture

Executed the brief's `rg` command verbatim and wrote results to:

- `/tmp/guidearch-vmx-usage.txt`

Follow-up inspection focused on:

- Python VM roots and NiceGUI glue:
  - `langs/python/src/guidearch/viewmodels/scenario_vm.py`
  - `langs/python/src/guidearch/viewmodels/app_vm.py`
  - `langs/python/src/guidearch/view/adapters/vmx_to_nicegui.py`
  - `langs/python/src/guidearch/main.py`
- TypeScript VM roots and Svelte adapter / route consumers:
  - `langs/typescript/src/viewmodels/scenario-vm.ts`
  - `langs/typescript/src/viewmodels/app-vm.ts`
  - `langs/typescript/src/view/adapters/vmx-to-svelte.ts`
  - `langs/typescript/src/routes/+page.svelte`
  - Route components under `langs/typescript/src/routes/lib` that call `vmxToStore`
- C# VM factories and Avalonia view glue:
  - `langs/csharp/src/GuideArch.ViewModels/ScenarioVMFactory.cs`
  - `langs/csharp/src/GuideArch.ViewModels/AppVMFactory.cs`
  - All listed leaf VM factories
  - `langs/csharp/src/GuideArch.ViewModels/ScenarioState.cs`
  - `langs/csharp/src/GuideArch.View/MainWindow.axaml`
  - `langs/csharp/src/GuideArch.View/MainWindow.axaml.cs`

## Step 3 - Current usage inventory

Created:

- `docs/superpowers/specs/vmx-3-1-audit/current-usage.md`

Document structure implemented per brief:

- Summary table with one row each for Python, TypeScript, and C#
- Python:
  - ViewModels table
  - View/Adapter Glue table
- TypeScript:
  - ViewModels table
  - View/Adapter Glue table
- C#:
  - ViewModels table
  - View Glue table
- Cross-Language Hot Spots table

Inventory highlights:

- Python has the largest bespoke VMx surface because `ScenarioVM` and `AppVM` manually recreate behavior that TypeScript and C# keep inside VMx component VMs.
- TypeScript has the broadest view-layer fan-out because `vmxToStore` is consumed by the root page plus multiple route components.
- C# concentrates risk in the root factories and in Avalonia view glue that binds against open-generic `ComponentVM<T>` instances.

## Step 4 - Artifact verification

Ran the brief's verification commands:

```bash
rg -n 'Python|TypeScript|C#|ScenarioVM|AppVM|ComponentVM|RelayCommand|MessageHub|replacement pressure|Cross-Language Hot Spots' docs/superpowers/specs/vmx-3-1-audit/current-usage.md
rg -n 'TO''DO|TB''D|FIX''ME|place''holder|angle-bracket marker' docs/superpowers/specs/vmx-3-1-audit/current-usage.md docs/superpowers/specs/vmx-3-1-audit/loc-baseline.txt
```

Results:

- First command: passed and found the required concepts.
- Second command: exited `1`, which is the expected clean result.

## Files changed

- `docs/superpowers/specs/vmx-3-1-audit/loc-baseline.txt`
- `docs/superpowers/specs/vmx-3-1-audit/current-usage.md`
- `.superpowers/sdd/task-2-report.md`

## Commit

- `7c4b0b0` - `docs: inventory current VMx usage`

## Concerns

- The inventory intentionally estimates replacement pressure qualitatively; it is evidence-backed but still judgmental, not mechanically derived.
- The raw `rg` pattern does not catch every indirect dependency shape on its own, so I supplemented it with targeted file inspection before classifying files.
- Known baseline compatibility failures remain unchanged by design:
  - Python still has VMx 3.1 collection/import failures around `RelayCommandOfT`.
  - TypeScript still has the previously recorded `PropertyChangedMessage` naming mismatch in baseline tests.

## Task 2 review fix

- Updated `docs/superpowers/specs/vmx-3-1-audit/loc-baseline.txt` so the test section is now `VM/View test files` with a reproducible explicit file list piped to `wc -l`.
- Kept the ViewModel production and View/adapter production sections unchanged.
- Re-scoped the counted test surface from `12892 total` to `10241 total` by excluding non-VM/View suites from savings metrics.
- Added an `Excluded from VM/View test LOC` note covering:
  - C# `GuideArch.Models.Tests`
  - Python model/TOPSIS/output/conformance-only tests
  - TypeScript model/TOPSIS/conformance-only tests
