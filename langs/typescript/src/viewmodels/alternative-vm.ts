/**
 * AlternativeVM — wraps an AlternativeM.
 *
 * Observable: id (read-only), decisionId (read-write), name (read-write).
 * Changing decisionId triggers a re-solve via the onModelChanged callback.
 */
import { ComponentVMOf, MessageHub, NullDispatcher } from 'vmx';
import type { AlternativeM } from '../models/alternative.js';

export type AlternativeVM = ComponentVMOf<AlternativeM>;

export function makeAlternativeVm(
  alternative: AlternativeM,
  hub: MessageHub,
  onDecisionIdChanged: () => void,
): AlternativeVM {
  const vm = ComponentVMOf.builder<AlternativeM>()
    .name('alternative')
    .model(alternative)
    .services(hub, NullDispatcher.INSTANCE)
    .modeledHinter((m) => m.name)
    .onModelChanged((newModel) => {
      // Only trigger solve when decisionId changes (moves the alt between decisions)
      if (newModel.decisionId !== alternative.decisionId) {
        onDecisionIdChanged();
      }
    })
    .build();
  vm.construct();
  return vm;
}
