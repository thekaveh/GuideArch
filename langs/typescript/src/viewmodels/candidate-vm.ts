/**
 * CandidateVM — read-only wrapper for a CandidateM.
 *
 * Created by ScenarioVM after each solve run. No mutations; no commands.
 */
import { ComponentVMOf, NullMessageHub, NullDispatcher } from 'vmx';
import type { CandidateM } from '../models/candidate.js';

export type CandidateVM = ComponentVMOf<CandidateM>;

export function makeCandidateVm(candidate: CandidateM): CandidateVM {
  const vm = ComponentVMOf.builder<CandidateM>()
    .name('candidate')
    .model(candidate)
    .services(NullMessageHub.INSTANCE, NullDispatcher.INSTANCE)
    .modeledHinter((m) => `rank=${m.rank} score=${m.score.toFixed(6)}`)
    .build();
  vm.construct();
  return vm;
}
