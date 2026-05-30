/**
 * Deterministic JSON serialization for candidates, critical decisions,
 * and critical constraints.
 *
 * Field order for candidates: alternativeIds, triangularValue, normalizedValue, score, rank.
 * Float representation: JS default Number.prototype.toString — matches Python repr(float).
 * Do NOT use .toFixed, .toPrecision, or any locale-aware formatting.
 */
import type { CandidateM } from '../models/candidate.js';
import type { CriticalDecisionM } from '../models/critical-decision.js';
import type { CriticalConstraintM } from '../models/critical-constraint.js';

// eslint-disable-next-line @typescript-eslint/no-explicit-any
function orderedStringify(obj: any): string {
  return JSON.stringify(obj, null, 2) + '\n';
}

export function serializeCandidates(candidates: CandidateM[]): string {
  const arr = candidates.map((c) => ({
    alternativeIds: [...c.alternativeIds],
    triangularValue: {
      lower: c.triangularValue.lower,
      modal: c.triangularValue.modal,
      upper: c.triangularValue.upper,
    },
    normalizedValue: {
      positive: c.normalizedValue.positive,
      average: c.normalizedValue.average,
      negative: c.normalizedValue.negative,
    },
    score: c.score,
    rank: c.rank,
  }));
  return orderedStringify(arr);
}

export function serializeCriticalDecisions(decisions: CriticalDecisionM[]): string {
  const arr = decisions.map((d) => ({
    decisionId: d.decisionId,
    triangularValue: {
      lower: d.triangularValue.lower,
      modal: d.triangularValue.modal,
      upper: d.triangularValue.upper,
    },
    normalizedValue: {
      positive: d.normalizedValue.positive,
      average: d.normalizedValue.average,
      negative: d.normalizedValue.negative,
    },
    score: d.score,
    rank: d.rank,
  }));
  return orderedStringify(arr);
}

export function serializeCriticalConstraints(constraints: CriticalConstraintM[]): string {
  const arr = constraints.map((c) => ({
    constraintIndex: c.constraintIndex,
    kind: c.kind,
    eliminated: c.eliminated,
    total: c.total,
    redundant: c.redundant,
  }));
  return orderedStringify(arr);
}
