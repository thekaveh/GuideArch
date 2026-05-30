/**
 * DecisionVM — wraps a DecisionM.
 *
 * Observable: id (read-only), name (read-write).
 * Mutating name updates the underlying model but does NOT trigger a solve.
 */
import { ComponentVMOf, MessageHub, NullDispatcher } from 'vmx';
import type { DecisionM } from '../models/decision.js';

export type DecisionVM = ComponentVMOf<DecisionM>;

export function makeDecisionVm(decision: DecisionM, hub: MessageHub): DecisionVM {
  const vm = ComponentVMOf.builder<DecisionM>()
    .name('decision')
    .model(decision)
    .services(hub, NullDispatcher.INSTANCE)
    .modeledHinter((m) => m.name)
    .build();
  vm.construct();
  return vm;
}
