# VMx 3.1 Refactor Audit Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **Execution tracking note:** This file remains a source plan. Execution status for the completed audit work is tracked in `.superpowers/sdd/progress.md` and the shipped audit artifacts rather than by checking every box here.

**Goal:** Produce an evidence-backed VMx 3.1 migration/refactor audit for GuideArch across Python, TypeScript, and C#, including replacement tracking, LOC savings metrics, and test coverage impact.

**Architecture:** Keep this slice focused on dependency/source alignment, inventory, measurement, and reporting. Do not broadly refactor production ViewModel or view code yet. Use supporting Markdown artifacts as the audit database, then roll them up into a user-facing report.

**Tech Stack:** Python `uv`, PyPI `vmx==3.1.0`, VMx `main` submodule for TypeScript/C#, pnpm/Svelte/Vitest, .NET/Avalonia/xUnit, Markdown, `rg`, `git`, `wc`, `uv`, `pnpm`, and `dotnet`.

## Global Constraints

- Branch: `codex/vmx-3-1-refactor-audit`.
- Python uses PyPI `vmx==3.1.0`.
- TypeScript uses latest VMx `main` as source reference because npm is not published for this release line.
- C# uses latest VMx `main` as source reference because NuGet is not published for this release line.
- Python, TypeScript, and C# receive equal report coverage.
- ViewModel production LOC, view/adapter production LOC, and test LOC are counted separately.
- Docs, generated files, caches, build outputs, lockfile churn, and formatting-only changes are excluded from savings metrics.
- The report includes a replacement ledger with deleted production LOC, added production LOC, net production LOC, test LOC delta, behavior coverage, and risk.
- The report includes a test coverage impact matrix naming exact test files to keep, add, rewrite, or remove.
- Broad production refactors are out of scope for this audit slice.

---

## File Structure

Create:

- `docs/superpowers/specs/2026-07-02-vmx-3-1-refactor-audit-report.md`: final user-facing report.
- `docs/superpowers/specs/vmx-3-1-audit/baseline.md`: dependency/source state and verification results.
- `docs/superpowers/specs/vmx-3-1-audit/current-usage.md`: current GuideArch VMx usage inventory.
- `docs/superpowers/specs/vmx-3-1-audit/vmx-3-1-capabilities.md`: VMx 3.1 API inventory relevant to GuideArch.
- `docs/superpowers/specs/vmx-3-1-audit/replacement-ledger.md`: replacement decisions with LOC and risk accounting.
- `docs/superpowers/specs/vmx-3-1-audit/test-impact.md`: test coverage impact matrix.
- `docs/superpowers/specs/vmx-3-1-audit/loc-baseline.txt`: raw LOC commands and output.

Modify:

- `langs/python/pyproject.toml`: change VMx dependency to `vmx==3.1.0`.
- `langs/python/uv.lock`: refresh to resolve VMx 3.1.0.
- `vendor/vmx`: advance submodule to latest VMx `main` for TypeScript/C# validation.

---

### Task 1: Align VMx Sources And Record Baseline

**Files:**
- Modify: `langs/python/pyproject.toml`
- Modify: `langs/python/uv.lock`
- Modify: `vendor/vmx`
- Create: `docs/superpowers/specs/vmx-3-1-audit/baseline.md`

**Interfaces:**
- Consumes: approved design in `docs/superpowers/specs/2026-07-02-vmx-3-1-refactor-audit-design.md`.
- Produces: VMx 3.1 dependency/source state used by every later task.

- [ ] **Step 1: Confirm branch and current source state**

Run:

```bash
git status --short --branch
git rev-parse HEAD
git -C vendor/vmx rev-parse HEAD
rg -n '"vmx' langs/python/pyproject.toml
rg -n 'name = "vmx"|version = "2.6.0"' langs/python/uv.lock -C 2
```

Expected: branch is `codex/vmx-3-1-refactor-audit`; current Python dependency is still the pre-bump value before editing; current lockfile contains the old resolved VMx version.

- [ ] **Step 2: Pin Python to VMx 3.1.0**

Edit `langs/python/pyproject.toml` so the dependency entry is:

```toml
    "vmx==3.1.0",
```

Run:

```bash
rg -n '"vmx==3.1.0"' langs/python/pyproject.toml
```

Expected: one match.

- [ ] **Step 3: Refresh Python lockfile**

Run:

```bash
cd langs/python && uv lock --upgrade-package "vmx==3.1.0"
```

Verify:

```bash
rg -n 'name = "vmx"|version = "3.1.0"|specifier = "==3.1.0"' langs/python/uv.lock -C 2
```

Expected: `guidearch` metadata uses `specifier = "==3.1.0"` and the `vmx` package block resolves `version = "3.1.0"`.

- [ ] **Step 4: Advance VMx submodule to latest main**

Run:

```bash
git -C vendor/vmx fetch origin main
git -C vendor/vmx checkout origin/main
git -C vendor/vmx rev-parse --short HEAD
```

Verify:

```bash
rg -n 'Version>3.1.0|MinSpecVersion>3.1.0' vendor/vmx/langs/csharp/src/VMx/VMx.csproj
rg -n '"version": "3.1.0"' vendor/vmx/langs/typescript/package.json
rg -n '__version__ = "3.1.0"|__min_spec_version__ = "3.1.0"' vendor/vmx/langs/python/src/vmx/__about__.py
```

Expected: VMx source metadata reports 3.1.0 for C#, TypeScript, and Python.

- [ ] **Step 5: Run compatibility smoke checks**

Run:

```bash
cd langs/python && uv sync --all-extras && uv run python - <<'PY'
import vmx
from vmx import ComponentVMOf, MessageHub, RxDispatcher
print(vmx.__version__)
print(ComponentVMOf.__name__, MessageHub.__name__, RxDispatcher.__name__)
PY
```

Expected output includes:

```text
3.1.0
ComponentVMOf MessageHub RxDispatcher
```

Run:

```bash
cd vendor/vmx/langs/typescript && pnpm install && pnpm build
```

Expected: command exits 0 and creates `vendor/vmx/langs/typescript/dist`.

Run:

```bash
( cd langs/typescript && pnpm install && pnpm test )
( cd langs/csharp && dotnet test --nologo )
( cd langs/python && uv run pytest tests/ -q )
```

Expected: pass if the dependency bump is compatible. If a command fails, capture the failing command, first failing test or compiler error, and whether it blocks the audit.

- [ ] **Step 6: Write `baseline.md`**

Create `docs/superpowers/specs/vmx-3-1-audit/baseline.md` with these sections:

```markdown
# VMx 3.1 Audit Baseline

## GuideArch State

Record the branch, pre-alignment GuideArch commit, post-alignment working commit, and any unrelated untracked files intentionally ignored.

## VMx Source State

Record Python constraint before and after, Python lockfile version before and after, old and new `vendor/vmx` commits, TypeScript VMx version metadata, and C# VMx version metadata.

## Verification

Record each compatibility command from Task 1 Step 5, its pass/fail result, and the key output or failure summary.

## Compatibility Notes

Record observed compatibility issues. If none occur, write exactly: "No compatibility issues observed during baseline verification."
```

Run:

```bash
rg -n 'GuideArch State|VMx Source State|Verification|Compatibility Notes|No compatibility issues observed|fail|pass' docs/superpowers/specs/vmx-3-1-audit/baseline.md
```

Expected: all required sections and at least one verification result are present.

- [ ] **Step 7: Commit source alignment and baseline**

Run:

```bash
git add langs/python/pyproject.toml langs/python/uv.lock vendor/vmx docs/superpowers/specs/vmx-3-1-audit/baseline.md
git commit -m "chore: align VMx 3.1 audit baseline"
```

Expected: commit succeeds and does not include unrelated pre-existing untracked docs.

---

### Task 2: Measure Baseline LOC And Inventory Current Usage

**Files:**
- Create: `docs/superpowers/specs/vmx-3-1-audit/loc-baseline.txt`
- Create: `docs/superpowers/specs/vmx-3-1-audit/current-usage.md`

**Interfaces:**
- Consumes: VMx-aligned branch from Task 1.
- Produces: baseline measurement and current-usage evidence for replacement decisions.

- [ ] **Step 1: Capture LOC baseline**

Run:

```bash
{
  echo '# LOC Baseline Commands And Output'
  echo
  echo '## ViewModel production files'
  find langs/python/src/guidearch/viewmodels langs/typescript/src/viewmodels langs/csharp/src/GuideArch.ViewModels \
    -type f \( -name '*.py' -o -name '*.ts' -o -name '*.cs' \) \
    ! -path '*/bin/*' ! -path '*/obj/*' ! -path '*/__pycache__/*' \
    | sort | xargs wc -l
  echo
  echo '## View/adapter production files'
  find langs/python/src/guidearch/view langs/typescript/src/view langs/typescript/src/routes langs/csharp/src/GuideArch.View \
    -type f \( -name '*.py' -o -name '*.ts' -o -name '*.svelte' -o -name '*.cs' -o -name '*.axaml' \) \
    ! -path '*/bin/*' ! -path '*/obj/*' ! -path '*/__pycache__/*' \
    | sort | xargs wc -l
  echo
  echo '## VM/View tests'
  find langs/python/tests langs/typescript/tests langs/csharp/tests \
    -type f \( -name '*.py' -o -name '*.ts' -o -name '*.cs' \) \
    ! -path '*/bin/*' ! -path '*/obj/*' ! -path '*/__pycache__/*' \
    | sort | xargs wc -l
} > docs/superpowers/specs/vmx-3-1-audit/loc-baseline.txt
```

Expected: file has three sections and `total` lines for each section.

- [ ] **Step 2: Capture current VMx usage evidence**

Run:

```bash
rg -n "from vmx|import .*vmx|using VMx|from 'vmx'|from \"vmx\"|ComponentVM|RelayCommand|MessageHub|NullDispatcher|NullMessageHub|property_changed|PropertyChangedMessage|vmxToStore|bind_command" \
  langs/python/src/guidearch langs/python/tests \
  langs/typescript/src langs/typescript/tests \
  langs/csharp/src langs/csharp/tests \
  > /tmp/guidearch-vmx-usage.txt
```

Expected: file contains direct VMx usage sites across all three implementations.

- [ ] **Step 3: Write `current-usage.md`**

Create `docs/superpowers/specs/vmx-3-1-audit/current-usage.md` with these sections and concrete tables:

```markdown
# Current GuideArch VMx Usage Inventory

## Summary

One row each for Python, TypeScript, and C#, describing primary VMx usage, largest bespoke areas, and parity notes.

## Python

Tables for ViewModels and View/Adapter Glue. Include every file under `langs/python/src/guidearch/viewmodels`, `langs/python/src/guidearch/view/adapters/vmx_to_nicegui.py`, and `langs/python/src/guidearch/main.py` when it has VMx coupling.

## TypeScript

Tables for ViewModels and View/Adapter Glue. Include every file under `langs/typescript/src/viewmodels`, `langs/typescript/src/view/adapters/vmx-to-svelte.ts`, and Svelte route components that consume VMx stores.

## C#

Tables for ViewModels and View Glue. Include `ScenarioVMFactory.cs`, `AppVMFactory.cs`, all leaf VM factories, `ScenarioState.cs`, `MainWindow.axaml`, and `MainWindow.axaml.cs`.

## Cross-Language Hot Spots

Compare root scenario orchestration, leaf VM repetition, collections and filtered views, commands and confirmation, form/edit lifecycle, and view bindings/adapters across all three languages.
```

Every table row must include current VMx abstraction, bespoke behavior, and replacement pressure as `low`, `medium`, or `high`.

- [ ] **Step 4: Verify current usage artifact**

Run:

```bash
rg -n 'Python|TypeScript|C#|ScenarioVM|AppVM|ComponentVM|RelayCommand|MessageHub|replacement pressure|Cross-Language Hot Spots' docs/superpowers/specs/vmx-3-1-audit/current-usage.md
rg -n 'TO''DO|TB''D|FIX''ME|place''holder|angle-bracket marker' docs/superpowers/specs/vmx-3-1-audit/current-usage.md docs/superpowers/specs/vmx-3-1-audit/loc-baseline.txt
```

Expected: first command finds required concepts; second command finds no matches and exits 1.

- [ ] **Step 5: Commit LOC and usage inventory**

Run:

```bash
git add docs/superpowers/specs/vmx-3-1-audit/loc-baseline.txt docs/superpowers/specs/vmx-3-1-audit/current-usage.md
git commit -m "docs: inventory current VMx usage"
```

Expected: commit succeeds.

---

### Task 3: Inventory VMx 3.1 Capabilities

**Files:**
- Create: `docs/superpowers/specs/vmx-3-1-audit/vmx-3-1-capabilities.md`

**Interfaces:**
- Consumes: VMx 3.1 source from Task 1.
- Produces: candidate abstraction inventory for replacement ledger.

- [ ] **Step 1: Collect source evidence**

Run:

```bash
find vendor/vmx/spec -maxdepth 1 -type f -name '*.md' -print | sort > /tmp/vmx-spec-files.txt
find vendor/vmx/langs/python/src/vmx vendor/vmx/langs/typescript/src vendor/vmx/langs/csharp/src/VMx \
  -maxdepth 2 -type f \( -name '*.py' -o -name '*.ts' -o -name '*.cs' \) \
  | sort > /tmp/vmx-3-1-source-files.txt
rg -n "class |interface |export class|export interface|public sealed class|public interface|def |function |Builder|builder" \
  vendor/vmx/langs/python/src/vmx \
  vendor/vmx/langs/typescript/src \
  vendor/vmx/langs/csharp/src/VMx \
  > /tmp/vmx-3-1-symbols.txt
```

Expected: all three files contain VMx 3.1 evidence.

- [ ] **Step 2: Read VMx specs relevant to GuideArch**

Read:

```text
vendor/vmx/spec/04-commands.md
vendor/vmx/spec/06-composite-vm.md
vendor/vmx/spec/07-group-vm.md
vendor/vmx/spec/08-aggregate-vm.md
vendor/vmx/spec/14-capabilities.md
vendor/vmx/spec/15-derived-properties.md
vendor/vmx/spec/19-dialogs.md
vendor/vmx/spec/20-form-vm.md
vendor/vmx/spec/21-collections.md
vendor/vmx/spec/22-discriminator-vm.md
```

Expected: capability notes are incorporated into `vmx-3-1-capabilities.md`.

- [ ] **Step 3: Write `vmx-3-1-capabilities.md`**

Create `docs/superpowers/specs/vmx-3-1-audit/vmx-3-1-capabilities.md` with:

```markdown
# VMx 3.1 Capability Inventory For GuideArch

## Source State

Table with Python, TypeScript, and C# version/source evidence.

## Candidate Capabilities

Table with one row for each of these capabilities: component builder conveniences, readonly modeled components, CompositeVM, CompositeVMOf, GroupVM, AggregateVM1 through AggregateVM6, FilteredCompositeVM, ScoredFilteredCompositeVM, ObservableList, ServicedObservableCollection, ObservableDictionary, PagedComposition, token paging, RelayCommand, RelayCommandOf, AsyncRelayCommand, CompositeCommand, DecoratorCommand, fluent command helpers, ConfirmationDecoratorCommand, ModeledCrudCommands, FormVM, form builders, DerivedProperty, property-value change helpers, dialog services, modal services, notification services, DiscriminatorVM, selection capabilities, filter capabilities, search capabilities, and paging capabilities.

Each row must state Python availability, TypeScript availability, C# availability, GuideArch fit, and notes.

## Strongest Candidates

Rank the strongest candidates for GuideArch and explain why.

## Rejected Or Weak Candidates

List candidates that should not be adopted now and explain why.
```

- [ ] **Step 4: Verify capability inventory**

Run:

```bash
rg -n 'FormVM|ModeledCrudCommands|ObservableList|ObservableDictionary|CompositeVM|GroupVM|AggregateVM|DerivedProperty|DiscriminatorVM|ConfirmationDecoratorCommand|AsyncRelayCommand|FilteredCompositeVM' docs/superpowers/specs/vmx-3-1-audit/vmx-3-1-capabilities.md
rg -n 'TO''DO|TB''D|FIX''ME|place''holder|angle-bracket marker' docs/superpowers/specs/vmx-3-1-audit/vmx-3-1-capabilities.md
```

Expected: first command finds all required capabilities; second command finds no matches and exits 1.

- [ ] **Step 5: Commit capability inventory**

Run:

```bash
git add docs/superpowers/specs/vmx-3-1-audit/vmx-3-1-capabilities.md
git commit -m "docs: inventory VMx 3.1 capabilities"
```

Expected: commit succeeds.

---

### Task 4: Build Replacement Ledger And Test Impact Matrix

**Files:**
- Create: `docs/superpowers/specs/vmx-3-1-audit/replacement-ledger.md`
- Create: `docs/superpowers/specs/vmx-3-1-audit/test-impact.md`

**Interfaces:**
- Consumes: current usage inventory and VMx 3.1 capability inventory.
- Produces: replacement decisions and test coverage accounting.

- [ ] **Step 1: Write `replacement-ledger.md`**

Create `docs/superpowers/specs/vmx-3-1-audit/replacement-ledger.md` with:

```markdown
# VMx 3.1 Replacement Ledger

## LOC Accounting Rules

State the counting rules from Global Constraints and cite `loc-baseline.txt`.

## Summary

Table with baseline LOC, projected replacement LOC, net saved, and confidence for ViewModel production, view/adapter production, total production, and tests.

## Ledger

Table with these columns: ID, current pattern, replacement decision, languages, files, deleted production LOC, added production LOC, net production LOC, test LOC delta, behavior coverage, risk.

Include rows R1 through R14:
R1 root ScenarioVM orchestration.
R2 leaf VM factory/class repetition.
R3 candidate/result readonly wrappers.
R4 coefficient grid indexing and updates.
R5 constraint collections and per-kind views.
R6 dirty tracking and re-solve triggers.
R7 property/value change subscriptions.
R8 add/update/delete command surfaces.
R9 save/open/new command workflows.
R10 dialog and confirmation handling.
R11 candidate selection state.
R12 view adapter/store glue.
R13 AppVM theme persistence and warnings.
R14 conceptual VM tree composites/aggregates.

## Notes By Replacement

For each replacement ID, include decision, rationale, migration sketch, and test strategy.
```

Use `replace`, `partial`, `keep`, or `defer` in every replacement decision. Use numeric LOC estimates and write negative net production LOC when a candidate is expected to add code.

- [ ] **Step 2: Write `test-impact.md`**

Create `docs/superpowers/specs/vmx-3-1-audit/test-impact.md` with:

```markdown
# VMx 3.1 Test Coverage Impact Matrix

## Summary

Table with Python, TypeScript, and C# columns and rows for must keep passing, rewrite, add, and remove.

## Must Keep Passing

Table mapping behaviors to exact Python, TypeScript, and C# test files. Include VM construction and lifecycle, property change propagation, dirty tracking and re-solve triggers, add/update/delete cascades, save/open failure behavior, candidate selection, collection/filter behavior, dialog/confirmation behavior, and cross-language conformance.

## Rewrite

Table with exact test file, language, reason to rewrite, and replacement assertion.

## Add

Table with proposed exact test file, language, behavior to cover, and replacement IDs.

## Remove

Table with exact test file or `None`, language, and removal condition.

## Verification Commands

Table with commands and expected result for Python VM tests, Python conformance, TypeScript tests, TypeScript conformance, C# tests, and C# conformance.
```

- [ ] **Step 3: Verify ledger and matrix**

Run:

```bash
rg -n 'Deleted production LOC|Added production LOC|Net production LOC|Test LOC delta|Must Keep Passing|Rewrite|Add|Remove|R1|R14' docs/superpowers/specs/vmx-3-1-audit/replacement-ledger.md docs/superpowers/specs/vmx-3-1-audit/test-impact.md
rg -n 'TO''DO|TB''D|FIX''ME|place''holder|angle-bracket marker' docs/superpowers/specs/vmx-3-1-audit/replacement-ledger.md docs/superpowers/specs/vmx-3-1-audit/test-impact.md
```

Expected: first command finds required categories; second command finds no matches and exits 1.

- [ ] **Step 4: Commit ledger and test impact**

Run:

```bash
git add docs/superpowers/specs/vmx-3-1-audit/replacement-ledger.md docs/superpowers/specs/vmx-3-1-audit/test-impact.md
git commit -m "docs: map VMx 3.1 replacements and tests"
```

Expected: commit succeeds.

---

### Task 5: Write Final Audit Report

**Files:**
- Create: `docs/superpowers/specs/2026-07-02-vmx-3-1-refactor-audit-report.md`

**Interfaces:**
- Consumes: all supporting artifacts from Tasks 1 through 4.
- Produces: final report for the user.

- [ ] **Step 1: Write final report**

Create `docs/superpowers/specs/2026-07-02-vmx-3-1-refactor-audit-report.md` with:

```markdown
# VMx 3.1 Refactor Audit Report

**Date:** 2026-07-02
**Branch:** `codex/vmx-3-1-refactor-audit`
**VMx Python package:** `vmx==3.1.0`
**VMx source commit for TypeScript/C#:** record the exact `vendor/vmx` commit hash

## Executive Summary

Summarize strongest replacement opportunities, expected LOC savings, test impact, and migration risk.

## Dependency And Source State

Summarize `baseline.md`.

## Current Usage Findings

Summarize `current-usage.md`.

## VMx 3.1 Capability Findings

Summarize `vmx-3-1-capabilities.md`.

## Replacement Recommendations

Summarize `replacement-ledger.md`.

## LOC Savings Metrics

Include ViewModel production, view/adapter production, total production, and test LOC numbers with counting method and exclusions.

## Test Coverage Changes

Summarize `test-impact.md`.

## Recommended Refactor Phases

List dependency and compatibility, safe mechanical replacements, collection and composite cleanup, form and command cleanup, view adapter simplification, and parity cleanup. For each phase, list replacement IDs, files, test commands, and stop/go criteria.

## Non-Goals And Rejected Candidates

List rejected or deferred VMx abstractions and reasons.

## Appendix: Supporting Artifacts

Link all supporting files under `docs/superpowers/specs/vmx-3-1-audit/`.
```

- [ ] **Step 2: Verify final report**

Run:

```bash
rg -n 'Python|TypeScript|C#|Replacement|LOC|Test Coverage|vmx==3.1.0|VMx source commit|Recommended Refactor Phases|Non-Goals' docs/superpowers/specs/2026-07-02-vmx-3-1-refactor-audit-report.md
rg -n 'TO''DO|TB''D|FIX''ME|place''holder|angle-bracket marker' docs/superpowers/specs/2026-07-02-vmx-3-1-refactor-audit-report.md
```

Expected: first command finds all key report sections; second command finds no matches and exits 1.

- [ ] **Step 3: Cross-check report against design**

Run:

```bash
rg -n 'replacement ledger|LOC|test coverage|Python|TypeScript|C#|VMx 3.1|Non-Goals|phase' docs/superpowers/specs/2026-07-02-vmx-3-1-refactor-audit-design.md docs/superpowers/specs/2026-07-02-vmx-3-1-refactor-audit-report.md
```

Expected: both design and report contain the required themes.

- [ ] **Step 4: Commit final report**

Run:

```bash
git add docs/superpowers/specs/2026-07-02-vmx-3-1-refactor-audit-report.md
git commit -m "docs: report VMx 3.1 refactor audit"
```

Expected: commit succeeds.

---

### Task 6: Final Verification And Handoff

**Files:**
- Modify only if verification reveals inconsistency:
  - `docs/superpowers/specs/2026-07-02-vmx-3-1-refactor-audit-report.md`
  - files under `docs/superpowers/specs/vmx-3-1-audit/`

**Interfaces:**
- Consumes: all audit artifacts.
- Produces: verified branch ready for user review.

- [ ] **Step 1: Run report completeness checks**

Run:

```bash
rg -n 'TO''DO|TB''D|FIX''ME|place''holder|angle-bracket marker' docs/superpowers/specs/2026-07-02-vmx-3-1-refactor-audit-report.md docs/superpowers/specs/vmx-3-1-audit
test -f docs/superpowers/specs/vmx-3-1-audit/baseline.md
test -f docs/superpowers/specs/vmx-3-1-audit/current-usage.md
test -f docs/superpowers/specs/vmx-3-1-audit/vmx-3-1-capabilities.md
test -f docs/superpowers/specs/vmx-3-1-audit/replacement-ledger.md
test -f docs/superpowers/specs/vmx-3-1-audit/test-impact.md
test -f docs/superpowers/specs/vmx-3-1-audit/loc-baseline.txt
test -f docs/superpowers/specs/2026-07-02-vmx-3-1-refactor-audit-report.md
```

Expected: `rg` finds no matches and exits 1; all `test -f` commands exit 0.

- [ ] **Step 2: Run final test verification**

Run:

```bash
( cd langs/python && uv run pytest tests/ -q )
( cd langs/typescript && pnpm test )
( cd langs/csharp && dotnet test --nologo )
```

Expected: tests pass, or any failure is documented in the final report's dependency/source state section.

- [ ] **Step 3: Check git state**

Run:

```bash
git status --short --branch
git log --oneline -6
```

Expected: audit files are committed. Pre-existing unrelated untracked docs may remain and must not be staged.

- [ ] **Step 4: Commit verification corrections if needed**

If Steps 1 or 2 required corrections, run:

```bash
git add docs/superpowers/specs/2026-07-02-vmx-3-1-refactor-audit-report.md docs/superpowers/specs/vmx-3-1-audit
git commit -m "docs: finalize VMx 3.1 audit report"
```

Expected: commit succeeds only when corrections were made. Skip this step when there are no corrections.

- [ ] **Step 5: Prepare final response**

Final response includes:

```text
Completed VMx 3.1 refactor audit on branch `codex/vmx-3-1-refactor-audit`.

Report:
- docs/superpowers/specs/2026-07-02-vmx-3-1-refactor-audit-report.md

Key outcomes:
- VMx source/package state.
- Best replacement candidates.
- Projected production LOC saved.
- Test impact.

Verification:
- Python tests.
- TypeScript tests.
- C# tests.
```

Expected: concise summary with the final report path and actual verification status.
