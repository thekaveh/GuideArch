/**
 * ConstraintVM — wraps a ConstraintM (any flavor: threshold, dependency, conflict).
 *
 * Editing any field triggers a re-solve via the onModelChanged callback.
 */
import { ComponentVMOf, MessageHub, NullDispatcher } from 'vmx';
import type { ConstraintM } from '../models/constraint.js';

export type ConstraintVM = ComponentVMOf<ConstraintM>;

export function makeConstraintVm(
  constraint: ConstraintM,
  hub: MessageHub,
  onSolveTrigger: () => void,
): ConstraintVM {
  const vm = ComponentVMOf.builder<ConstraintM>()
    .name('constraint')
    .model(constraint)
    .services(hub, NullDispatcher.INSTANCE)
    .modeledHinter((m) => m.kind)
    .onModelChanged(() => {
      onSolveTrigger();
    })
    .build();
  vm.construct();
  return vm;
}
