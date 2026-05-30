import type { TriangularFuzzyM } from './triangular-fuzzy.js';
import type { NormalizedFuzzyM } from './normalized-fuzzy.js';

export interface CandidateM {
  readonly alternativeIds: readonly string[];
  readonly triangularValue: TriangularFuzzyM;
  readonly normalizedValue: NormalizedFuzzyM;
  readonly score: number;
  readonly rank: number;
}
