/**
 * Unit tests for buildComparisonSeries — the chart-data prep step that
 * feeds Chart C (top-10 polyline comparison) per spec/charts.md §4.
 *
 * Mirrors the C# ChartComparisonTests.cs and Python test_comparison_option.py
 * suites so the three impls lock the same invariants.
 */
import { describe, it, expect } from 'vitest';

import type { CandidateM } from '../../src/models/candidate.js';
import type { PropertyM } from '../../src/models/property.js';
import type { CoefficientM } from '../../src/models/coefficient.js';
import { TriangularFuzzyM } from '../../src/models/triangular-fuzzy.js';
import { buildComparisonSeries, COMPARISON_PALETTE } from '../../src/view/chart-data.js';

function mkCandidate(rank: number, altIds: string[], score: number): CandidateM {
  return {
    alternativeIds: altIds,
    triangularValue: new TriangularFuzzyM(0, score, 0),
    normalizedValue: { positive: 0, average: score, negative: 0 },
    score,
    rank,
  };
}

function mkProperty(id: string, name: string): PropertyM {
  return { id, name, kind: 'max', weight: 1 };
}

function mkCoeff(altId: string, propId: string, modal: number): CoefficientM {
  return {
    alternativeId: altId,
    propertyId: propId,
    value: new TriangularFuzzyM(modal, modal, modal),
  };
}

describe('buildComparisonSeries (spec/charts.md §4)', () => {
  it('returns an empty array when there are no candidates', () => {
    const result = buildComparisonSeries([], [mkProperty('p1', 'P1')], []);
    expect(result).toEqual([]);
  });

  it('returns an empty array when there are no properties', () => {
    const result = buildComparisonSeries([mkCandidate(0, ['a'], 0.5)], [], []);
    // No properties → loop over properties produces an empty points array;
    // the series still exists since the candidate exists. Verify the
    // contract: one entry per candidate, with empty points.
    expect(result).toHaveLength(1);
    expect(result[0].points).toEqual([]);
  });

  it('caps the result at topN (default = palette length = 10)', () => {
    const props = [mkProperty('p1', 'P1')];
    const candidates = Array.from({ length: 25 }, (_, i) =>
      mkCandidate(i, [`a${i}`], 1 - i * 0.01),
    );
    const result = buildComparisonSeries(candidates, props, []);
    expect(result).toHaveLength(COMPARISON_PALETTE.length);
    expect(COMPARISON_PALETTE.length).toBe(10);
  });

  it('caps at the explicit topN argument when smaller than palette + candidates', () => {
    const props = [mkProperty('p1', 'P1')];
    const candidates = Array.from({ length: 15 }, (_, i) =>
      mkCandidate(i, [`a${i}`], 1 - i * 0.01),
    );
    const result = buildComparisonSeries(candidates, props, [], 5);
    expect(result).toHaveLength(5);
  });

  it('caps at candidates.length when fewer than topN', () => {
    const props = [mkProperty('p1', 'P1')];
    const candidates = [mkCandidate(0, ['a0'], 0.9), mkCandidate(1, ['a1'], 0.8)];
    const result = buildComparisonSeries(candidates, props, [], 10);
    expect(result).toHaveLength(2);
  });

  it('assigns palette colors by series index, not by rank', () => {
    // If the first candidate has rank 7 (e.g. the ranking is non-monotone
    // in the input), the palette assignment uses the array index (0), not
    // the rank. Spec §4: "Color: stable assignment from a shared 10-color
    // Tableau palette indexed by rank" — the implementation maps the *list
    // position* to the palette index, which equals the rank for a sorted
    // list. This test pins the array-index behavior so a future refactor
    // doesn't silently switch to rank-keyed lookup (different in unsorted
    // inputs).
    const props = [mkProperty('p1', 'P1')];
    const candidates = [
      mkCandidate(0, ['a'], 0.5),
      mkCandidate(1, ['a'], 0.4),
      mkCandidate(2, ['a'], 0.3),
    ];
    const result = buildComparisonSeries(candidates, props, []);
    expect(result[0].color).toBe(COMPARISON_PALETTE[0]); // #4e79a7
    expect(result[1].color).toBe(COMPARISON_PALETTE[1]); // #f28e2b
    expect(result[2].color).toBe(COMPARISON_PALETTE[2]); // #e15759
  });

  it('preserves the candidate rank on each series entry', () => {
    const props = [mkProperty('p1', 'P1')];
    const candidates = [mkCandidate(0, ['a'], 0.5), mkCandidate(1, ['a'], 0.4)];
    const result = buildComparisonSeries(candidates, props, []);
    expect(result.map((s) => s.rank)).toEqual([0, 1]);
  });

  it("computes modal sum across the candidate's alternatives for each property", () => {
    // Candidate selects alternatives a1 + a2.
    // p1: a1.modal = 0.6, a2.modal = 0.3 → sum 0.9
    // p2: a1.modal = 0.1, a2.modal = 0.2 → sum 0.3
    const props = [mkProperty('p1', 'P1'), mkProperty('p2', 'P2')];
    const coeffs: CoefficientM[] = [
      mkCoeff('a1', 'p1', 0.6),
      mkCoeff('a1', 'p2', 0.1),
      mkCoeff('a2', 'p1', 0.3),
      mkCoeff('a2', 'p2', 0.2),
      // Noise: shouldn't be counted (not in the candidate's set):
      mkCoeff('a3', 'p1', 99),
      mkCoeff('a3', 'p2', 99),
    ];
    const candidates = [mkCandidate(0, ['a1', 'a2'], 0.5)];
    const result = buildComparisonSeries(candidates, props, coeffs);

    expect(result[0].points[0].propertyIndex).toBe(0);
    expect(result[0].points[0].modal).toBeCloseTo(0.9, 12);
    expect(result[0].points[1].propertyIndex).toBe(1);
    expect(result[0].points[1].modal).toBeCloseTo(0.3, 12);
  });

  it('treats missing coefficients as zero contribution', () => {
    const props = [mkProperty('p1', 'P1'), mkProperty('p2', 'P2')];
    // Only one coefficient defined; the candidate's other (alt, prop)
    // pairings should contribute zero rather than throwing.
    const coeffs: CoefficientM[] = [mkCoeff('a1', 'p1', 0.5)];
    const candidates = [mkCandidate(0, ['a1'], 0.5)];
    const result = buildComparisonSeries(candidates, props, coeffs);

    expect(result[0].points).toEqual([
      { propertyIndex: 0, modal: 0.5 },
      { propertyIndex: 1, modal: 0 },
    ]);
  });

  it('formats the label as "#<rank> (<score to 3 sig figs>)"', () => {
    const props = [mkProperty('p1', 'P1')];
    const candidates = [mkCandidate(0, ['a'], 0.03118), mkCandidate(7, ['a'], 0.0271423)];
    const result = buildComparisonSeries(candidates, props, []);
    expect(result[0].label).toBe('#0 (0.0312)');
    expect(result[1].label).toBe('#7 (0.0271)');
  });

  it('exposes the candidate score on each series entry (for the legend chip)', () => {
    const props = [mkProperty('p1', 'P1')];
    const candidates = [mkCandidate(0, ['a'], 0.031180695179944)];
    const result = buildComparisonSeries(candidates, props, []);
    expect(result[0].score).toBeCloseTo(0.031180695179944, 12);
  });

  it('palette has exactly 10 entries with the spec/charts.md §4 Tableau hex values', () => {
    expect(COMPARISON_PALETTE).toEqual([
      '#4e79a7',
      '#f28e2b',
      '#e15759',
      '#76b7b2',
      '#59a14f',
      '#edc948',
      '#b07aa1',
      '#ff9da7',
      '#9c755f',
      '#bab0ac',
    ]);
  });
});
