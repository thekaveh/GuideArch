export type PropertyKind = 'min' | 'max';

export interface PropertyM {
  readonly id: string;
  readonly name: string;
  readonly kind: PropertyKind;
  readonly weight: number;
}
