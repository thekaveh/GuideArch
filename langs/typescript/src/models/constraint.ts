export type ConstraintM =
  | {
      readonly kind: 'threshold';
      readonly propertyId: string;
      readonly min?: number;
      readonly max?: number;
    }
  | {
      readonly kind: 'dependency';
      readonly sourceAlternativeId: string;
      readonly targetAlternativeId: string;
    }
  | { readonly kind: 'conflict'; readonly alternativeAId: string; readonly alternativeBId: string };
