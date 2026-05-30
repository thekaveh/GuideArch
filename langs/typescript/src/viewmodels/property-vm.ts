/**
 * PropertyVM — wraps a PropertyM.
 *
 * Observable: id (read-only), name (read-write), kind (read-write), weight (read-write).
 * Changing kind or weight triggers a re-solve via the onModelChanged callback.
 */
import { ComponentVMOf, MessageHub, NullDispatcher } from 'vmx';
import type { PropertyM } from '../models/property.js';

export type PropertyVM = ComponentVMOf<PropertyM>;

export function makePropertyVm(
  property: PropertyM,
  hub: MessageHub,
  onSolveTrigger: () => void,
): PropertyVM {
  let prevKind = property.kind;
  let prevWeight = property.weight;

  const vm = ComponentVMOf.builder<PropertyM>()
    .name('property')
    .model(property)
    .services(hub, NullDispatcher.INSTANCE)
    .modeledHinter((m) => `${m.name} (${m.kind}, w=${m.weight})`)
    .onModelChanged((newModel) => {
      if (newModel.kind !== prevKind || newModel.weight !== prevWeight) {
        prevKind = newModel.kind;
        prevWeight = newModel.weight;
        onSolveTrigger();
      }
    })
    .build();
  vm.construct();
  return vm;
}
