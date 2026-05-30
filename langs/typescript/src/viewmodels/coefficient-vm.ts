/**
 * CoefficientVM — wraps a CoefficientM cell (alternativeId × propertyId).
 *
 * Exposes lower, modal, upper as read-write values. Editing any cell triggers
 * a re-solve via the onModelChanged callback.
 */
import { ComponentVMOf, MessageHub, NullDispatcher } from 'vmx';
import type { CoefficientM } from '../models/coefficient.js';

export type CoefficientVM = ComponentVMOf<CoefficientM>;

export function makeCoefficientVm(
  coefficient: CoefficientM,
  hub: MessageHub,
  onSolveTrigger: () => void,
): CoefficientVM {
  const vm = ComponentVMOf.builder<CoefficientM>()
    .name('coefficient')
    .model(coefficient)
    .services(hub, NullDispatcher.INSTANCE)
    .modeledHinter(
      (m) =>
        `(${m.alternativeId}, ${m.propertyId}): [${m.value.lower}, ${m.value.modal}, ${m.value.upper}]`,
    )
    .onModelChanged(() => {
      onSolveTrigger();
    })
    .build();
  vm.construct();
  return vm;
}
