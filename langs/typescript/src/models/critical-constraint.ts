export interface CriticalConstraintM {
  readonly constraintIndex: number;
  readonly kind: string;
  readonly eliminated: number;
  readonly total: number;
  readonly redundant: boolean;
}
