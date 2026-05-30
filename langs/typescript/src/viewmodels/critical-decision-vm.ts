/**
 * CriticalDecisionVM — read-only wrapper for a CriticalDecisionM.
 *
 * Created by ScenarioVM after each solve run. No mutations; no commands.
 */
import { ComponentVMOf, NullMessageHub, NullDispatcher } from 'vmx';
import type { CriticalDecisionM } from '../models/critical-decision.js';

export type CriticalDecisionVM = ComponentVMOf<CriticalDecisionM>;

export function makeCriticalDecisionVm(decision: CriticalDecisionM): CriticalDecisionVM {
  const vm = ComponentVMOf.builder<CriticalDecisionM>()
    .name('criticalDecision')
    .model(decision)
    .services(NullMessageHub.INSTANCE, NullDispatcher.INSTANCE)
    .modeledHinter((m) => `decisionId=${m.decisionId} rank=${m.rank}`)
    .build();
  vm.construct();
  return vm;
}
