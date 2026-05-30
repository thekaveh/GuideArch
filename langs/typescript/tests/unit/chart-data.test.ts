/**
 * Tests: pure data-prep functions in view/chart-data.ts.
 */
import { describe, it, expect } from 'vitest';
import { buildCandidateBarData, buildTriangleSeriesData } from '../../src/view/chart-data.js';
import { TriangularFuzzyM } from '../../src/models/triangular-fuzzy.js';
import type { CandidateM } from '../../src/models/candidate.js';
import type { AlternativeM } from '../../src/models/alternative.js';
import type { PropertyM } from '../../src/models/property.js';
import type { CoefficientM } from '../../src/models/coefficient.js';

// ---------------------------------------------------------------------------
// Fixtures
// ---------------------------------------------------------------------------

function makeCandidate(rank: number, score: number, altIds: string[]): CandidateM {
  return {
    rank,
    score,
    alternativeIds: altIds,
    triangularValue: new TriangularFuzzyM(0, score, score * 2),
    normalizedValue: { positive: score, average: score, negative: score },
  };
}

const alts: AlternativeM[] = [
  { id: 'a1', decisionId: 'd1', name: 'Alt A' },
  { id: 'a2', decisionId: 'd1', name: 'Alt B' },
  { id: 'a3', decisionId: 'd2', name: 'Alt C' },
];

const props: PropertyM[] = [
  { id: 'p1', name: 'Cost', kind: 'min', weight: 1 },
  { id: 'p2', name: 'Speed', kind: 'max', weight: 2 },
];

const coefficients: CoefficientM[] = [
  { alternativeId: 'a1', propertyId: 'p1', value: new TriangularFuzzyM(1, 2, 3) },
  { alternativeId: 'a1', propertyId: 'p2', value: new TriangularFuzzyM(4, 5, 6) },
  { alternativeId: 'a3', propertyId: 'p1', value: new TriangularFuzzyM(2, 3, 4) },
  { alternativeId: 'a3', propertyId: 'p2', value: new TriangularFuzzyM(1, 2, 3) },
];

// ---------------------------------------------------------------------------
// buildCandidateBarData
// ---------------------------------------------------------------------------

describe('buildCandidateBarData', () => {
  const candidates: CandidateM[] = [
    makeCandidate(0, 0.9, ['a1', 'a3']),
    makeCandidate(1, 0.7, ['a2', 'a3']),
    makeCandidate(2, 0.5, ['a1']),
  ];

  it('preserves rank order', () => {
    const data = buildCandidateBarData(candidates, alts);
    expect(data.map((d) => d.rank)).toEqual([0, 1, 2]);
  });

  it('preserves score values', () => {
    const data = buildCandidateBarData(candidates, alts);
    expect(data[0].score).toBe(0.9);
    expect(data[1].score).toBe(0.7);
    expect(data[2].score).toBe(0.5);
  });

  it('resolves alternative names', () => {
    const data = buildCandidateBarData(candidates, alts);
    expect(data[0].altNames).toEqual(['Alt A', 'Alt C']);
    expect(data[1].altNames[0]).toBe('Alt B');
  });

  it('falls back to id when alternative not found', () => {
    const data = buildCandidateBarData(candidates, []);
    expect(data[0].altNames).toEqual(['a1', 'a3']);
  });

  it('respects the limit parameter', () => {
    const manyCandidates = Array.from({ length: 50 }, (_, i) =>
      makeCandidate(i, 1 - i * 0.02, ['a1']),
    );
    const data = buildCandidateBarData(manyCandidates, alts, 30);
    expect(data).toHaveLength(30);
  });

  it('returns empty array for empty candidates', () => {
    const data = buildCandidateBarData([], alts);
    expect(data).toHaveLength(0);
  });
});

// ---------------------------------------------------------------------------
// buildTriangleSeriesData
// ---------------------------------------------------------------------------

describe('buildTriangleSeriesData', () => {
  const candidate = makeCandidate(0, 0.9, ['a1', 'a3']);

  it('returns one series per property', () => {
    const series = buildTriangleSeriesData(candidate, props, coefficients);
    expect(series).toHaveLength(2);
  });

  it('series have correct propertyId and propertyName', () => {
    const series = buildTriangleSeriesData(candidate, props, coefficients);
    expect(series[0].propertyId).toBe('p1');
    expect(series[0].propertyName).toBe('Cost');
    expect(series[1].propertyId).toBe('p2');
    expect(series[1].propertyName).toBe('Speed');
  });

  it('each series has exactly 3 points', () => {
    const series = buildTriangleSeriesData(candidate, props, coefficients);
    for (const s of series) {
      expect(s.points).toHaveLength(3);
    }
  });

  it('triangle points have y=0 at vertices and y=1 at modal', () => {
    const series = buildTriangleSeriesData(candidate, props, coefficients);
    for (const s of series) {
      expect(s.points[0].y).toBe(0); // lower vertex
      expect(s.points[1].y).toBe(1); // modal apex
      expect(s.points[2].y).toBe(0); // upper vertex
    }
  });

  it('sums coefficients from both alternatives for p1', () => {
    // a1.p1 = (1,2,3); a3.p1 = (2,3,4) => sum = (3,5,7)
    const series = buildTriangleSeriesData(candidate, props, coefficients);
    const p1 = series.find((s) => s.propertyId === 'p1')!;
    expect(p1.points[0].x).toBe(3); // lower: 1+2
    expect(p1.points[1].x).toBe(5); // modal: 2+3
    expect(p1.points[2].x).toBe(7); // upper: 3+4
  });

  it('sums coefficients from both alternatives for p2', () => {
    // a1.p2 = (4,5,6); a3.p2 = (1,2,3) => sum = (5,7,9)
    const series = buildTriangleSeriesData(candidate, props, coefficients);
    const p2 = series.find((s) => s.propertyId === 'p2')!;
    expect(p2.points[0].x).toBe(5);
    expect(p2.points[1].x).toBe(7);
    expect(p2.points[2].x).toBe(9);
  });

  it('returns zeros for a property with no matching coefficients', () => {
    const candidateNoCoeff = makeCandidate(0, 0.5, ['unknown-alt']);
    const series = buildTriangleSeriesData(candidateNoCoeff, props, coefficients);
    for (const s of series) {
      expect(s.points[0].x).toBe(0);
      expect(s.points[1].x).toBe(0);
      expect(s.points[2].x).toBe(0);
    }
  });

  it('returns empty array when no properties', () => {
    const series = buildTriangleSeriesData(candidate, [], coefficients);
    expect(series).toHaveLength(0);
  });
});
