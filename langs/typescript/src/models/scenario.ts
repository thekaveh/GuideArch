import type { DecisionM } from './decision.js';
import type { AlternativeM } from './alternative.js';
import type { PropertyM } from './property.js';
import type { CoefficientM } from './coefficient.js';
import type { ConstraintM } from './constraint.js';
import type { ConfigM } from './config.js';

export interface ScenarioM {
  readonly schemaVersion: string;
  readonly name: string;
  readonly description: string;
  readonly decisions: readonly DecisionM[];
  readonly alternatives: readonly AlternativeM[];
  readonly properties: readonly PropertyM[];
  readonly coefficients: readonly CoefficientM[];
  readonly constraints: readonly ConstraintM[];
  readonly config: ConfigM;
  readonly warnings: readonly string[];
}
