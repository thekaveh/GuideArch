/**
 * CriticalConstraintVM — read-only wrapper for a CriticalConstraintM.
 *
 * Created by ScenarioVM after each solve run. No mutations; no commands.
 */
import { ComponentVMOf, NullMessageHub, NullDispatcher } from 'vmx';
import type { CriticalConstraintM } from '../models/critical-constraint.js';

export type CriticalConstraintVM = ComponentVMOf<CriticalConstraintM>;

export function makeCriticalConstraintVm(constraint: CriticalConstraintM): CriticalConstraintVM {
  const vm = ComponentVMOf.builder<CriticalConstraintM>()
    .name('criticalConstraint')
    .model(constraint)
    .services(NullMessageHub.INSTANCE, NullDispatcher.INSTANCE)
    .modeledHinter((m) => `idx=${m.constraintIndex} eliminated=${m.eliminated}`)
    .build();
  vm.construct();
  return vm;
}
