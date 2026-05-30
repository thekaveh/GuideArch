/**
 * Minimal scenario: 1 decision, 2 alternatives, 1 property (max).
 * Hand-computed expected values.
 */
import { describe, it, expect } from 'vitest';
import { solve } from '../../src/models/topsis/solve.js';
import type { ScenarioM } from '../../src/models/scenario.js';
import { TriangularFuzzyM } from '../../src/models/triangular-fuzzy.js';

function makeMinimalScenario(): ScenarioM {
  // Decision d1, alternatives a1 (lower=1,modal=2,upper=3) and a2 (lower=4,modal=5,upper=6)
  // Property p1: kind=max, weight=1
  // M(p1) = max(a1.upper, a2.upper) across 1 decision group = max(3,6) = 6
  // contribution(a1,p1) = (1,2,3) * (-1) * 1 / 6 = (-1/6, -2/6, -3/6)
  // contribution(a2,p1) = (4,5,6) * (-1) * 1 / 6 = (-4/6, -5/6, -6/6)
  // totalValue(a1) = (-1/6, -2/6, -3/6) = (-0.1667, -0.3333, -0.5)
  // totalValue(a2) = (-4/6, -5/6, -6/6) = (-0.6667, -0.8333, -1.0)
  // Z-space (§3.6):
  //   toZ(t) = { average: t.modal, positive: |t.modal-t.lower|, negative: |t.upper-t.modal| }
  //   z(a1): { positive: |-0.3333-(-0.1667)| = 0.1667, average: -0.3333, negative: |-0.5-(-0.3333)| = 0.1667 }
  //   z(a2): { positive: |-0.8333-(-0.6667)| = 0.1667, average: -0.8333, negative: |-1.0-(-0.8333)| = 0.1667 }
  // PIS: { average: min(-0.3333,-0.8333)=-0.8333, positive: max(0.1667,0.1667)=0.1667, negative: min(0.1667,0.1667)=0.1667 }
  // NIS: { average: max(...)=-0.3333, positive: min(...)=0.1667, negative: max(...)=0.1667 }
  // Normalization: denom_pos = 0 => n_pos = 0; denom_neg = 0 => n_neg = 0
  // denom_avg = -0.3333 - (-0.8333) = 0.5
  // n_avg(a1) = (-0.3333 - (-0.8333)) / 0.5 = 0.5/0.5 = 1.0
  // n_avg(a2) = (-0.8333 - (-0.8333)) / 0.5 = 0.0
  // With weights (1/3, 1/3, 1/3) and aggregation='max':
  //   score(a1) = max(1/3 * 0, 1/3 * 1, 1/3 * 0) = 1/3
  //   score(a2) = max(1/3 * 0, 1/3 * 0, 1/3 * 0) = 0
  // So a2 should rank 0 (lower score = better), a1 rank 1.
  return {
    schemaVersion: '1.0.0',
    name: 'minimal',
    description: '',
    decisions: [{ id: 'd1', name: 'Decision 1' }],
    alternatives: [
      { id: 'a1', decisionId: 'd1', name: 'Alt 1' },
      { id: 'a2', decisionId: 'd1', name: 'Alt 2' },
    ],
    properties: [{ id: 'p1', name: 'Prop 1', kind: 'max', weight: 1 }],
    coefficients: [
      { alternativeId: 'a1', propertyId: 'p1', value: new TriangularFuzzyM(1, 2, 3) },
      { alternativeId: 'a2', propertyId: 'p1', value: new TriangularFuzzyM(4, 5, 6) },
    ],
    constraints: [],
    config: {
      aggregation: 'max',
      weights: { positive: 1 / 3, average: 1 / 3, negative: 1 / 3 },
    },
    warnings: [],
  };
}

describe('solve — minimal scenario', () => {
  const scenario = makeMinimalScenario();
  const candidates = solve(scenario);

  it('returns 2 candidates', () => {
    expect(candidates.length).toBe(2);
  });

  it('rank 0 candidate has lower score', () => {
    expect(candidates[0].score).toBeLessThan(candidates[1].score);
  });

  it('rank 0 is a2 (better on max property)', () => {
    expect(candidates[0].alternativeIds[0]).toBe('a2');
    expect(candidates[0].rank).toBe(0);
  });

  it('rank 1 is a1', () => {
    expect(candidates[1].alternativeIds[0]).toBe('a1');
    expect(candidates[1].rank).toBe(1);
  });

  it('rank 0 score is 0', () => {
    expect(candidates[0].score).toBeCloseTo(0, 10);
  });

  it('rank 1 score is 1/3', () => {
    expect(candidates[1].score).toBeCloseTo(1 / 3, 10);
  });
});
