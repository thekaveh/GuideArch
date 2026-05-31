# Analysis & charts (M4) — formal specification

**Status:** Authoritative. All three impls must realize this UI contract.

M3 gave editors. M4 gives the architect the *insight* views: ranked-candidates score chart, fuzzy-value triangle visualizer, critical-decisions panel, critical-constraints panel. All four are read-only.

## 1. New top-level tabs

Extend the M3 tab strip with two more tabs after `Results`:

- **Critical Decisions** — table view of `criticalDecisions` (see `spec/viewmodels.md` §3.1)
- **Critical Constraints** — table view of `criticalConstraints` (see `spec/viewmodels.md` §3.1)

And **upgrade the `Results` tab** to a split pane:

- Left (~60% width): the ranked-candidates table from M2/M3 (rank, score, alternatives).
- Right (~40% width): two stacked charts (see §2 and §3).

## 2. Chart A — ranked candidates score chart

A horizontal bar chart of the top 30 candidates:

- **X-axis:** score (0 to max observed)
- **Y-axis:** candidate rank (0 at top)
- **Bar color:** monochrome accent, gradient from full opacity at rank 0 to half opacity at rank 30
- **Hover:** tooltip showing rank, score, list of alternative names
- **Click:** selects the candidate (drives Chart B + highlights the candidates table row)

Per impl:

- **TS (Svelte):** use **LayerChart** (`pnpm add layerchart`) or roll a small SVG component. LayerChart is preferred — Svelte-native, tree-shakeable.
- **C# (Avalonia):** use **ScottPlot.Avalonia** (NuGet `ScottPlot.Avalonia` 5.x).
- **Python (NiceGUI):** use **`ui.echart`** (Apache ECharts, built into NiceGUI).

## 3. Chart B — fuzzy-value triangle visualizer

For the currently-selected candidate (defaults to rank 0 when scenario first solves), render a stacked triangle plot: one triangle per property, X-axis is value, Y-axis is membership (`0` at vertices, `1` at modal). Color-coded by property; legend on the right.

This visualizes the `triangularValue` decomposition — what each property contributed to the candidate's score.

## 4. Critical Decisions tab

A read-only table:

| Column | Source |
|---|---|
| Rank | `CriticalDecisionM.rank` |
| Decision | name from `scenario.decisions` lookup by `decisionId` |
| Score | `score` to 6 sig figs |
| Triangular value | `(lower, modal, upper)` |
| Normalized | `(positive, average, negative)` |

Sort ascending by `rank` (lower score = more critical).

## 5. Critical Constraints tab

A read-only table:

| Column | Source |
|---|---|
| Index | `CriticalConstraintM.constraintIndex` |
| Kind | `kind` (threshold / dependency / conflict) |
| Eliminated | `eliminated` |
| Total | `total` |
| Eliminated % | `100 * eliminated / total` |
| Redundant | `redundant` (Yes/No) |

Sort descending by `eliminated` (most-binding first). Redundant rows shown with a faded background.

## 6. Selection state — `ScenarioState.selectedCandidateIndex`

Add an observable field `selectedCandidateIndex: int | None` (default `None`). The candidates table, Chart A, and Chart B all read it. Editing it from any of the three updates the other two.

## 7. Tests

- `tests/unit/chart-data.{py,cs,ts}` — verify the data prep step: candidates → chart-input arrays preserves rank order, score values, and alternative-name lookups.
- `tests/unit/selection-state.*` — observe `selectedCandidateIndex` propagating between subscribers.

UI rendering itself remains non-automatable.

## 8. Out of scope for M4

- Editing critical decisions / constraints (they are *outputs*, not inputs)
- Multi-candidate comparison (deferred to v1.2)
- Export of chart images
- Animations / transitions
