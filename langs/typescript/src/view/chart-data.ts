/**
 * Pure data-preparation functions for the M4 charts.
 *
 * These functions have no Svelte or DOM dependencies so they can be unit-tested
 * with plain vitest (no browser environment required).
 */
import type { CandidateM } from '../models/candidate.js';
import type { AlternativeM } from '../models/alternative.js';
import type { PropertyM } from '../models/property.js';
import type { CoefficientM } from '../models/coefficient.js';

// ---------------------------------------------------------------------------
// Chart A — ranked candidates bar-chart data
// ---------------------------------------------------------------------------

export interface CandidateBarDatum {
  /** 0-based rank */
  rank: number;
  score: number;
  /** Human-readable alternative names for the combo */
  altNames: string[];
}

/**
 * Build the input array for Chart A (top N candidates by rank, ascending).
 *
 * @param candidates  - Full candidate array (already sorted by rank asc).
 * @param alternatives - The scenario's alternatives (for name lookup).
 * @param limit       - Maximum number of entries; defaults to 30.
 */
export function buildCandidateBarData(
  candidates: readonly CandidateM[],
  alternatives: readonly AlternativeM[],
  limit = 30,
): CandidateBarDatum[] {
  const altMap = new Map(alternatives.map((a) => [a.id, a.name]));
  return candidates.slice(0, limit).map((c) => ({
    rank: c.rank,
    score: c.score,
    altNames: c.alternativeIds.map((id) => altMap.get(id) ?? id),
  }));
}

// ---------------------------------------------------------------------------
// Chart B — fuzzy triangle data for a single candidate
// ---------------------------------------------------------------------------

export interface TrianglePoint {
  x: number;
  y: number;
}

export interface PropertyTriangleSeries {
  propertyId: string;
  propertyName: string;
  /** Three points: (lower,0), (modal,1), (upper,0) */
  points: [TrianglePoint, TrianglePoint, TrianglePoint];
}

/**
 * Build the per-property triangle series for Chart B.
 *
 * Each property contributes one triangular series whose x-values are the
 * lower/modal/upper of the coefficient for the selected candidate's
 * alternative combination.  Because a candidate spans one alternative per
 * decision, we look up the coefficient for each (alternativeId, propertyId)
 * pair and sum the triangular contributions.
 *
 * @param candidate    - The selected candidate.
 * @param properties   - All scenario properties (for names).
 * @param coefficients - All scenario coefficients.
 */
export function buildTriangleSeriesData(
  candidate: CandidateM,
  properties: readonly PropertyM[],
  coefficients: readonly CoefficientM[],
): PropertyTriangleSeries[] {
  const altIds = new Set(candidate.alternativeIds);

  return properties.map((prop) => {
    // Sum the fuzzy coefficients for this property across the candidate's alternatives
    let lower = 0;
    let modal = 0;
    let upper = 0;
    let count = 0;
    for (const coeff of coefficients) {
      if (coeff.propertyId === prop.id && altIds.has(coeff.alternativeId)) {
        lower += coeff.value.lower;
        modal += coeff.value.modal;
        upper += coeff.value.upper;
        count++;
      }
    }
    if (count === 0) {
      lower = 0;
      modal = 0;
      upper = 0;
    }

    return {
      propertyId: prop.id,
      propertyName: prop.name,
      points: [
        { x: lower, y: 0 },
        { x: modal, y: 1 },
        { x: upper, y: 0 },
      ] as [TrianglePoint, TrianglePoint, TrianglePoint],
    };
  });
}

// ---------------------------------------------------------------------------
// Chart C — top-N candidate comparison polylines (legacy "color-coded
// comparison chart"). One polyline per top-N candidate; x = property index,
// y = sum of modal coefficients across the candidate's alternatives for
// that property. Same y-value the existing fuzzy-triangle chart uses for
// its modal peak — keeps the two charts numerically consistent.
// ---------------------------------------------------------------------------

/**
 * Stable 10-color qualitative palette (Tableau 10, hex). Index by candidate
 * rank so colors don't reshuffle as the user clicks around.
 */
export const COMPARISON_PALETTE: readonly string[] = [
  '#4e79a7', // blue
  '#f28e2b', // orange
  '#e15759', // red
  '#76b7b2', // teal
  '#59a14f', // green
  '#edc948', // yellow
  '#b07aa1', // purple
  '#ff9da7', // pink
  '#9c755f', // brown
  '#bab0ac', // grey
];

export interface ComparisonPoint {
  /** Zero-based property index along X. */
  propertyIndex: number;
  /** Sum of modal coefficients (same as the fuzzy triangle's modal). */
  modal: number;
}

export interface ComparisonSeries {
  /** Candidate rank, 0-based. Stable across re-solves until the candidate set changes. */
  rank: number;
  /** Display label, e.g. "#0 (0.0312)". */
  label: string;
  /** Color from COMPARISON_PALETTE indexed by rank. */
  color: string;
  /** Total TOPSIS score (for the legend). */
  score: number;
  /** One point per scenario property. */
  points: ComparisonPoint[];
}

/**
 * Build the polyline series for the top-N comparison chart.
 *
 * @param candidates   - Already-ranked candidate list.
 * @param properties   - Scenario properties (X-axis ordering).
 * @param coefficients - Scenario coefficients (lookup source).
 * @param topN         - Limit; defaults to {@link COMPARISON_PALETTE}.length (10).
 */
export function buildComparisonSeries(
  candidates: readonly CandidateM[],
  properties: readonly PropertyM[],
  coefficients: readonly CoefficientM[],
  topN: number = COMPARISON_PALETTE.length,
): ComparisonSeries[] {
  const n = Math.min(topN, candidates.length, COMPARISON_PALETTE.length);
  const series: ComparisonSeries[] = [];

  for (let i = 0; i < n; i++) {
    const c = candidates[i];
    const altIds = new Set(c.alternativeIds);
    const points: ComparisonPoint[] = properties.map((prop, pIdx) => {
      let modal = 0;
      for (const coeff of coefficients) {
        if (coeff.propertyId === prop.id && altIds.has(coeff.alternativeId)) {
          modal += coeff.value.modal;
        }
      }
      return { propertyIndex: pIdx, modal };
    });

    series.push({
      rank: c.rank,
      label: `#${c.rank} (${c.score.toPrecision(3)})`,
      color: COMPARISON_PALETTE[i],
      score: c.score,
      points,
    });
  }

  return series;
}
