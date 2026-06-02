# Editors layer (M3) — formal specification

**Status:** Authoritative. All three impls must realize this UI contract.

The M2 skeleton lets a user OPEN a scenario and SEE the resulting ranked candidates. M3 lets them EDIT scenarios in-place. Editing any field that the spec §4.3 (in `viewmodels.md`) marks "triggers solve" must re-run TOPSIS and update the candidates table live.

> **v1.0 status:** all three impls re-solve **synchronously** on each mutation. At SAS/EDS scale (≤ 25 alternatives × 7 properties) this is ≪ 10 ms per call and below human perception. The 100 ms-debounce design noted in earlier drafts is deferred to v1.1; if and when a scenario size makes synchronous resolve perceptible, the debounce lands in the view adapter layer, not the VM.

## 1. App shell

A persistent app shell hosts:

1. **Toolbar (top, 56px tall)** — left-to-right:
   - **Brand mark + wordmark** — the three-triangle motif (22×18) followed by "GuideArch" in 15px/700-weight. Same SVG geometry across impls (see `spec/design-system.md` §6.1).
   - **File group**: `New`, `Open…`, `Save`, `Save As…` (`Save` disabled if `filePath` undefined; `Save As…` requires a loaded scenario).
   - 1px vertical separator.
   - **Sample group**: `Sample SAS`, `Sample EDS` — accent-styled CTAs that load one of the two bundled scenarios. Convenience entry points for new users (see `spec/design-system.md` §8.1 / README §5.5).
   - Flexible spacer.
   - **Theme toggle** — 32×32 icon button that flips `AppVM.theme` between `"dark"` and `"light"`. Icon shows the *target* theme (sun while dark, moon while light) — same convention as macOS.
   - **Solve** — primary accent button. Explicitly re-runs `SolveCmd` (useful after import).
2. **Tab strip (top, 40px tall)** — eight tabs grouped into two phases of the workflow, each prefixed with a 14px icon:
   - **Author group**: `Decisions`, `Alternatives`, `Properties`, `Coefficients`, `Constraints` — these author the scenario.
   - **Analysis group**: `Results`, `Critical Decisions`, `Critical Constraints` — these read solved state.

   TS renders an `Author` / `Analysis` kicker label and a 1px divider between groups. C# (`TabControl`) and Python (Quasar `QTabs`) don't natively support separators between tab items, so the icon set carries the grouping work visually (the Analysis tabs use target / shield icons that read distinctly from the Author icons). The last three tabs (Results, Critical Decisions, Critical Constraints) were added in M4 with the analysis surface and ship in v1.0.
3. **Status bar (bottom, 32px tall)** — segments rendered left-to-right; chips align right:
   - Scenario name (accent color, semibold).
   - File path basename (monospace, muted), full path on hover. Hidden when `filePath` is undefined.
   - Status text (12px, secondary color) — e.g. `"Solved: 1336 candidates"`.
   - Flexible spacer.
   - Candidate count chip (info-blue) — `"{n} candidate(s)"`. Hidden when no scenario is loaded.
   - Unsaved chip (warning-yellow) — `"unsaved"`. Visible when `isDirty`.
   - Warning chip (danger-red) — `"⚠ {n} warning(s)"`, full warning list on hover. Hidden when `warnings` is empty.

## 2. Per-tab editors

All editors operate **on the live `ScenarioM`** via the M2 VM tree. Mutations propagate through the VMx hub; `ScenarioVM` sees property-change messages and re-runs `SolveCmd` synchronously (per the v1.0 status note above). The candidates table on the `Results` tab updates automatically.

### 2.1 Decisions tab

A simple editable list:

| Column | Editable | Notes |
|---|---|---|
| Name | ✓ | Inline edit |
| (toolbar) Add Decision | — | Appends a fresh `{ id: "d-<uuid>", name: "New decision" }` |
| (per row) Delete | — | Removes decision AND all its alternatives AND all coefficients/constraints referencing those alternatives. Confirm dialog. |

### 2.2 Alternatives tab

A grouped list (one accordion section per decision):

| Column | Editable | Notes |
|---|---|---|
| Decision (group header) | — | Shows decision name |
| Name | ✓ | Inline edit |
| (per group) Add Alternative | — | Appends `{ id: "a-<uuid>", decisionId: <group>, name: "New alternative" }`. Adds zero-fuzzy coefficients for every existing property. |
| (per row) Delete | — | Removes alternative AND its coefficients AND constraints referencing it. |

### 2.3 Properties tab

An editable table:

| Column | Editable | Notes |
|---|---|---|
| Name | ✓ | |
| Kind | ✓ | Dropdown `min` / `max` |
| Weight | ✓ | Numeric input, `> 0` |
| (toolbar) Add Property | — | Appends `{ id: "p-<uuid>", name: "New property", kind: "min", weight: 1 }`. Adds zero-fuzzy coefficients for every existing alternative. |
| (per row) Delete | — | Removes property AND its coefficients AND threshold constraints referencing it. |

### 2.4 Coefficients tab — the fuzzy matrix

A 2-D editable grid:

- **Rows**: alternatives, grouped by decision (sticky group headers).
- **Columns**: properties, each shown with its kind and weight badge.
- **Cell contents**: three numeric inputs `(lower, modal, upper)` — render compactly as `[L · M · U]`. Soft warning (yellow border) if `lower > modal` or `modal > upper`.
- **Edit semantics**: typing in any input updates the corresponding `CoefficientM` immediately; solve fires synchronously (per the v1.0 status note at the top of this doc).

### 2.5 Constraints tab

Three sub-tabs: `Threshold`, `Dependency`, `Conflict`.

**Threshold sub-tab** — table:

| Column | Editable | Notes |
|---|---|---|
| Property (dropdown) | ✓ | Lists all properties |
| Min | ✓ | Numeric or empty |
| Max | ✓ | Numeric or empty |
| (toolbar) Add Threshold | — | New row with first property selected; min/max empty |
| (per row) Delete | — | |

Validation per spec invariant 6.1 / 6.2: at least one of `min`/`max` must be set, and if both, `min ≤ max`. **Both rules are fatal at load** (invariants.md §6 categorises them as fatal, and the schema enforces 6.1 with an `anyOf` on `[min]`/`[max]`). Editors therefore block the Add/Update path with a `ScenarioMutationError` when these would be violated; the row never persists. Earlier drafts described "highlight red but tolerate at solve time" — that text was wrong; reject at mutation time.

**Dependency sub-tab** — table:

| Column | Editable | Notes |
|---|---|---|
| Source alternative (dropdown) | ✓ | All alternatives, labeled `<decision> / <alt>` |
| Target alternative (dropdown) | ✓ | Same |
| (toolbar) Add Dependency | — | |
| (per row) Delete | — | |

Self-edges (source == target) are **fatal** per invariant 7.1; the editor rejects the Add/Update at mutation time with a `ScenarioMutationError`, matching the loader. The row never persists.

**Conflict sub-tab** — same shape as Dependency but with columns `Alternative A`, `Alternative B`.

### 2.6 Results tab (already in M2)

No edits. The candidate table from M2. M3 only ensures the `Solve` toolbar button and the implicit re-solve work.

## 3. File operations

The M2 `OpenCmd`/`SaveCmd`/`NewCmd` are already wired in the VM. M3 connects them to OS-native dialogs:

- **Python (NiceGUI)**: `ui.upload` for Open in web mode; in native mode use Python's `tkinter.filedialog` as a synchronous fallback. Save: `tkinter.filedialog.asksaveasfilename` in native; web mode uses `ui.download(json_bytes, filename)`.
- **C# (Avalonia)**: `IStorageProvider.OpenFilePickerAsync` / `SaveFilePickerAsync`. JSON-only filter.
- **TypeScript (Svelte + Tauri)**: v1.0 ships a single browser-mode UX in both Tauri and pure-browser deployments — `<input type="file">` for Open, `URL.createObjectURL` + anchor download (with a `prompt()` for the filename in Save As) for Save. The `@tauri-apps/plugin-dialog` integration is on the v1.1 backlog and would route Tauri builds through the OS-native picker; until then both modes use the same code path. See `langs/typescript/src/routes/lib/Toolbar.svelte`.

## 4. Undo/redo

**Deferred to v1.1.** M3 does NOT implement undo. The original cross-reference was to a pre-implementation design doc (`docs/design/`) that has since been removed from the repository per the project's no-pre-impl-spec-docs policy; undo/redo lands when v1.1 work begins.

## 5. Validation feedback

When mutations violate an invariant from `spec/domain/invariants.md`:

- **Fatal** (e.g. delete a decision while its alternative is still referenced by a constraint): block the mutation, show a dialog explaining why, do not mutate.
- **Warning** (e.g. typed lower > modal in a coefficient): apply the mutation, show a non-blocking warning chip in the status bar.

## 6. Tests

Each impl ships tests (TS and Python use `langs/<impl>/tests/unit/` and `tests/integration/`; C# uses `langs/csharp/tests/GuideArch.Models.Tests/` and `tests/GuideArch.ViewModels.Tests/` per .NET solution conventions):

- **Round-trip test**: load `sas.json`, mutate a property weight via the VM, observe `candidates[0]` change, assert solve actually re-ran.
- **Delete cascade test**: load `sas.json`, delete a decision; assert that the decision's alternatives, their coefficients, and any constraints referencing the deleted alternatives are also gone, AND the scenario validates against the JSON schema after the cascade.
- **Save round-trip test**: load `sas.json`, save to a temp path, re-load, assert structurally identical (modulo formatting).

CI conformance is unchanged from M2 (M1 numerical conformance is the gate).

## 7. Layout & visual style

- Dark theme by default (matches existing Quasar/Avalonia/SvelteKit baselines).
- Monospace font for IDs and numeric cells; system sans-serif for names and labels.
- Tab strip uses standard control-library tab widgets per platform.
- Tables show ~30 rows then scroll.
- The Coefficients grid uses **flex columns**: the alternative-name column is fixed at ~12rem (read first), and each property column flexes to share the remaining canvas width above a per-column floor (~9rem). Horizontal scroll only appears when `propCount × min-width` exceeds the canvas — typical SAS/EDS layouts (7 properties) fit a 1280px window without scrolling, while wider scenarios still degrade gracefully into a scrolling matrix. Sticky decision-group row headers, sticky property-column headers.

## 8. Out of scope for M3

- Charts (M4): ranked-candidates bar, fuzzy-value triangle.
- Critical decisions / constraints panels (M4).
- Multi-scenario tabs (v1.1).
- Themes other than the default (v1.x).
- Localization (v2.x).
