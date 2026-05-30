export type Aggregation = 'sum' | 'max';

export interface Weights {
  readonly positive: number;
  readonly average: number;
  readonly negative: number;
}

export interface ConfigM {
  readonly aggregation: Aggregation;
  readonly weights: Weights;
}
