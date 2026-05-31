# Editors layer (M3) — formal specification

**Status:** Authoritative. All three impls must realize this UI contract.

The M2 skeleton lets a user OPEN a scenario and SEE the resulting ranked candidates. M3 lets them EDIT scenarios in-place. Editing any field that the spec §3.3 (in `viewmodels.md`) marks "triggers solve" must re-run TOPSIS and update the candidates table live.

> **v1.0 status:** all three impls re-solve **synchronously** on each mutation. At SAS/EDS scale (≤ 25 alternatives × 7 properties) this is ≪ 10 ms per call and below human perception. The 100 ms-debounce design noted in earlier drafts is deferred to v1.1; if and when a scenario size makes synchronous resolve perceptible, the debounce lands in the view adapter layer, not the VM.

## 1. App shell

A persistent app shell hosts:

1. **Toolbar (top)** — buttons in this order:
   - `New` — clears scenario; `ScenarioVM.NewCmd`.
   - `Open…` — OS-native file dialog; `OpenCmd`.
   - `Save` — disabled if `filePath` undefined; `SaveCmd`.
   - `Save As…` — OS-native save dialog; `SaveAsCmd`.
   - A flexible spacer.
   - `Solve` — explicitly re-runs `SolveCmd` (useful after import).
2. **Tab strip (left or top)** — five tabs: `Decisions`, `Alternatives`, `Properties`, `Coefficients`, `Constraints`. A sixth tab `Results` shows the M2 candidates table.
3. **Status bar (bottom)** — scenario name · `n candidates` · last error/warning (if any).

## 2. Per-tab editors

All editors operate **on the live `ScenarioM`** via the M2 VM tree. Mutations propagate through the VMx hub; `ScenarioVM` sees property-change messages and triggers `SolveCmd` on a 100 ms debounce. The candidates table on the `Results` tab updates automatically.

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
- **Edit semantics**: typing in any input updates the corresponding `CoefficientM` immediately; solve fires debounced.

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

Validation per spec invariant 6: at least one of `min`/`max` must be set; if both, `min ≤ max`. Invalid rows highlight red but the impl tolerates them by skipping the constraint at solve time (warn in status bar).

**Dependency sub-tab** — table:

| Column | Editable | Notes |
|---|---|---|
| Source alternative (dropdown) | ✓ | All alternatives, labeled `<decision> / <alt>` |
| Target alternative (dropdown) | ✓ | Same |
| (toolbar) Add Dependency | — | |
| (per row) Delete | — | |

Self-edges (source == target) are flagged but tolerated.

**Conflict sub-tab** — same shape as Dependency but with columns `Alternative A`, `Alternative B`.

### 2.6 Results tab (already in M2)

No edits. The candidate table from M2. M3 only ensures the `Solve` toolbar button and the implicit re-solve work.

## 3. File operations

The M2 `OpenCmd`/`SaveCmd`/`NewCmd` are already wired in the VM. M3 connects them to OS-native dialogs:

- **Python (NiceGUI)**: `ui.upload` for Open in web mode; in native mode use Python's `tkinter.filedialog` as a synchronous fallback. Save: `tkinter.filedialog.asksaveasfilename` in native; web mode uses `ui.download(json_bytes, filename)`.
- **C# (Avalonia)**: `IStorageProvider.OpenFilePickerAsync` / `SaveFilePickerAsync`. JSON-only filter.
- **TypeScript (Svelte + Tauri)**: `@tauri-apps/plugin-dialog` open/save in desktop; in browser-only mode use `<input type="file">` for open and `URL.createObjectURL` + anchor download for save.

## 4. Undo/redo

**Deferred to v1.1 per design spec §14.** M3 does NOT implement undo.

## 5. Validation feedback

When mutations violate an invariant from `spec/domain/invariants.md`:

- **Fatal** (e.g. delete a decision while its alternative is still referenced by a constraint): block the mutation, show a dialog explaining why, do not mutate.
- **Warning** (e.g. typed lower > modal in a coefficient): apply the mutation, show a non-blocking warning chip in the status bar.

## 6. Tests

Each impl ships tests under its `tests/unit/` (or `tests/integration/`):

- **Round-trip test**: load `sas.json`, mutate a property weight via the VM, observe `candidates[0]` change, assert solve actually re-ran.
- **Delete cascade test**: load `sas.json`, delete a decision; assert that the decision's alternatives, their coefficients, and any constraints referencing the deleted alternatives are also gone, AND the scenario validates against the JSON schema after the cascade.
- **Save round-trip test**: load `sas.json`, save to a temp path, re-load, assert structurally identical (modulo formatting).

CI conformance is unchanged from M2 (M1 numerical conformance is the gate).

## 7. Layout & visual style

- Dark theme by default (matches existing Quasar/Avalonia/SvelteKit baselines).
- Monospace font for IDs and numeric cells; system sans-serif for names and labels.
- Tab strip uses standard control-library tab widgets per platform.
- Tables show ~30 rows then scroll.
- The Coefficients grid is wider than the viewport for any non-trivial scenario; horizontal scrolling is expected. Sticky decision-group row headers, sticky property-column headers.

## 8. Out of scope for M3

- Charts (M4): ranked-candidates bar, fuzzy-value triangle.
- Critical decisions / constraints panels (M4).
- Multi-scenario tabs (v1.1).
- Themes other than the default (v1.x).
- Localization (v2.x).
