import type { TriangularFuzzyM } from './triangular-fuzzy.js';

export interface CoefficientM {
  readonly alternativeId: string;
  readonly propertyId: string;
  readonly value: TriangularFuzzyM;
}
