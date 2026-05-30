import {
  ComponentVMOf,
  NullMessageHub,
  NullDispatcher,
} from 'vmx';

/**
 * Smoke-test VMx wiring by instantiating a trivial ComponentVMOf and
 * returning a descriptive string for the UI.
 *
 * VMx uses a builder pattern — ComponentVM has no public constructor.
 * NullMessageHub and NullDispatcher are the null-object singletons
 * provided by VMx for tests and standalone demos.
 */
export function helloVMx(): string {
  const vm = ComponentVMOf.builder<{ message: string }>()
    .name('hello')
    .model({ message: 'hello from VMx' })
    .services(NullMessageHub.INSTANCE, NullDispatcher.INSTANCE)
    .modeledHinter((m) => m.message)
    .build();

  vm.construct();

  return `VMx loaded — model.message = "${vm.model.message}", status = ${vm.status}`;
}
