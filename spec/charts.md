# Analysis & charts (M4) — formal specification

**Status:** Authoritative. All three impls must realize this UI contract.

M3 gave editors. M4 gives the architect the *insight* views: ranked-candidates score chart, fuzzy-value triangle visualizer, critical-decisions panel, critical-constraints panel. All four are read-only.

## 1. New top-level tabs

Extend the M3 tab strip with two more tabs after `Results`:

- **Critical Decisions** — table view of `criticalDecisions` (see `spec/viewmodels.md` §3.1)
- **Critical Constraints** — table view of `criticalConstraints` (see `spec/viewmodels.md` §3.1)

And **upgrade the `Results` tab** to a split pane:

- Left (~60% width): the ranked-candidates table from M2/M3 (rank, score, alternatives).
- Right (~40% width): a **sub-tabbed chart pane** with three modes (default = `Ranking`):
  - `Ranking` → Chart A (see §2).
  - `Profile` → Chart B (see §3).
  - `Compare` → Chart C (see §4).

The sub-tab strip lives at the top of the right pane; only one chart is mounted at a time so each gets full vertical height. Selection state (§7) survives sub-tab switches.

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

## 4. Chart C — top-10 candidate comparison polylines

A line chart overlaying up to ten candidates so the architect can read the differences across properties at a glance. Ports the spirit of the legacy GuideArch `CandidatesAnalysis.xaml` polyline view to the v1.0 design language.

- **X-axis:** property index (one slot per scenario property), labelled with the property name.
- **Y-axis:** sum of modal coefficients across the candidate's selected alternatives for that property — the same value Chart B uses for the triangle's modal peak, so the two views stay numerically consistent.
- **Series:** one polyline per top-N candidate. `N = min(topN, candidates.length, COMPARISON_PALETTE.length)` with `DEFAULT_COMPARISON_TOP_N = 10`.
- **Color:** stable assignment from a shared 10-color Tableau palette indexed by rank. The same rank gets the same color across re-solves until the candidate set changes:

| Index | Hex | Name |
|---|---|---|
| 0 | `#4e79a7` | blue |
| 1 | `#f28e2b` | orange |
| 2 | `#e15759` | red |
| 3 | `#76b7b2` | teal |
| 4 | `#59a14f` | green |
| 5 | `#edc948` | yellow |
| 6 | `#b07aa1` | purple |
| 7 | `#ff9da7` | pink |
| 8 | `#9c755f` | brown |
| 9 | `#bab0ac` | grey |

- **Selection state:** when `selectedCandidateIndex` is set (§7), the matching polyline is drawn last (on top), at `stroke-width: 2.5` and full opacity; non-selected lines fade to ~25% opacity. When no candidate is selected, all polylines render at full opacity.
- **Click:** click on any polyline or its legend chip sets `selectedCandidateIndex` to that rank — the candidates table, Chart A, and Chart B update in sync.
- **Legend:** scrollable chip strip below the chart. Each chip shows the rank and score (e.g. `#0 (0.0312)`); the chip for the selected candidate is highlighted.

Per impl: TS rolls a small SVG component (no chart library); C# uses ScottPlot.Avalonia `ScatterLine`; Python uses `ui.echart` with one `type: "line"` series per candidate. Data preparation lives in each impl's `chart-data` module (`buildComparisonSeries` / `ChartData.PrepComparisonSeries` / `comparison_option`).

## 5. Critical Decisions tab

A read-only table:

| Column | Source |
|---|---|
| Rank | `CriticalDecisionM.rank` |
| Decision | name from `scenario.decisions` lookup by `decisionId` |
| Score | `score` to 6 sig figs |
| Triangular value | `(lower, modal, upper)` |
| Normalized | `(positive, average, negative)` |

Sort ascending by `rank` (lower score = more critical).

## 6. Critical Constraints tab

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

## 7. Selection state — `ScenarioState.selectedCandidateIndex`

Add an observable field `selectedCandidateIndex: int | None` (default `None`). The candidates table, Chart A, Chart B, and Chart C all read it. Editing it from any of the four updates the others.

## 8. Tests

- `tests/unit/chart-data.{py,cs,ts}` — verify the data prep step: candidates → chart-input arrays preserves rank order, score values, alternative-name lookups, and (for Chart C) modal sums and stable palette indexing.
- `tests/unit/selection-state.*` — observe `selectedCandidateIndex` propagating between subscribers across the candidates table, Chart A, Chart B, and Chart C.

UI rendering itself remains non-automatable.

## 9. Out of scope for M4

- Editing critical decisions / constraints (they are *outputs*, not inputs)
- Export of chart images
- Animations / transitions
