/**
 * ViewModel layer — public re-exports.
 */
export type { ScenarioState, ScenarioVM } from './scenario-vm.js';
export { makeScenarioVm, ScenarioMutationError } from './scenario-vm.js';

export type { DecisionVM } from './decision-vm.js';
export { makeDecisionVm } from './decision-vm.js';

export type { AlternativeVM } from './alternative-vm.js';
export { makeAlternativeVm } from './alternative-vm.js';

export type { PropertyVM } from './property-vm.js';
export { makePropertyVm } from './property-vm.js';

export type { CoefficientVM } from './coefficient-vm.js';
export { makeCoefficientVm } from './coefficient-vm.js';

export type { ConstraintVM } from './constraint-vm.js';
export { makeConstraintVm } from './constraint-vm.js';

export type { CandidateVM } from './candidate-vm.js';
export { makeCandidateVm } from './candidate-vm.js';

export type { CriticalDecisionVM } from './critical-decision-vm.js';
export { makeCriticalDecisionVm } from './critical-decision-vm.js';

export type { CriticalConstraintVM } from './critical-constraint-vm.js';
export { makeCriticalConstraintVm } from './critical-constraint-vm.js';
