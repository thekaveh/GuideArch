/**
 * Unit tests for the vmxToSvelte adapter.
 *
 * Tests vmxToStore and commandToButton against stub VMs to verify:
 * - Initial value is emitted synchronously on subscribe.
 * - Store updates when VM model changes.
 * - commandToButton disabled store reflects canExecute.
 * - onClick calls execute.
 */
import { describe, it, expect, vi } from 'vitest';
import { get } from 'svelte/store';
import { ComponentVMOf, MessageHub, NullDispatcher, RelayCommand } from 'vmx';
import { vmxToStore, vmxStoreAll, commandToButton } from '../../src/view/adapters/vmx-to-svelte.js';

// ---------------------------------------------------------------------------
// Helper: build a simple test VM
// ---------------------------------------------------------------------------
interface TestModel {
  count: number;
  label: string;
}

function makeTestVm(initial: TestModel): ComponentVMOf<TestModel> {
  const hub = new MessageHub();
  const vm = ComponentVMOf.builder<TestModel>()
    .name('test')
    .model(initial)
    .services(hub, NullDispatcher.INSTANCE)
    .modeledHinter((m) => m.label)
    .build();
  vm.construct();
  return vm;
}

// ---------------------------------------------------------------------------
// vmxToStore tests
// ---------------------------------------------------------------------------

describe('vmxToStore — initial value', () => {
  it('emits the current model value synchronously on subscribe', () => {
    const vm = makeTestVm({ count: 42, label: 'hello' });
    const store = vmxToStore(vm, 'count');
    expect(get(store)).toBe(42);
  });

  it('emits the label property', () => {
    const vm = makeTestVm({ count: 0, label: 'world' });
    const store = vmxToStore(vm, 'label');
    expect(get(store)).toBe('world');
  });
});

describe('vmxToStore — reactivity', () => {
  it('updates when VM model changes', () => {
    const vm = makeTestVm({ count: 0, label: 'a' });
    const store = vmxToStore(vm, 'count');

    const values: number[] = [];
    const unsub = store.subscribe((v) => values.push(v));

    vm.model = { count: 10, label: 'a' };
    vm.model = { count: 20, label: 'a' };

    unsub();

    expect(values).toEqual([0, 10, 20]);
  });

  it('does not emit after unsubscribe', () => {
    const vm = makeTestVm({ count: 0, label: 'a' });
    const store = vmxToStore(vm, 'count');

    const values: number[] = [];
    const unsub = store.subscribe((v) => values.push(v));
    unsub();

    vm.model = { count: 99, label: 'a' };
    expect(values).toEqual([0]);
  });
});

// ---------------------------------------------------------------------------
// vmxStoreAll tests
// ---------------------------------------------------------------------------

describe('vmxStoreAll', () => {
  it('emits full model on subscribe', () => {
    const vm = makeTestVm({ count: 5, label: 'foo' });
    const store = vmxStoreAll(vm);
    const m = get(store);
    expect(m.count).toBe(5);
    expect(m.label).toBe('foo');
  });

  it('emits updated model on change', () => {
    const vm = makeTestVm({ count: 1, label: 'x' });
    const store = vmxStoreAll(vm);

    let latest = get(store);
    const unsub = store.subscribe((m) => {
      latest = m;
    });

    vm.model = { count: 99, label: 'y' };
    unsub();

    expect(latest.count).toBe(99);
    expect(latest.label).toBe('y');
  });
});

// ---------------------------------------------------------------------------
// commandToButton tests
// ---------------------------------------------------------------------------

describe('commandToButton — disabled store', () => {
  it('disabled=false when command has no predicate (always enabled)', () => {
    const cmd = RelayCommand.builder()
      .task(() => {})
      .build();
    const { disabled } = commandToButton(cmd);
    expect(get(disabled)).toBe(false);
  });

  it('disabled=true when canExecute returns false', () => {
    const cmd = RelayCommand.builder()
      .predicate(() => false)
      .task(() => {})
      .build();
    const { disabled } = commandToButton(cmd);
    expect(get(disabled)).toBe(true);
  });
});

describe('commandToButton — onClick', () => {
  it('calls execute on click', () => {
    const fn = vi.fn();
    const cmd = RelayCommand.builder().task(fn).build();
    const { onClick } = commandToButton(cmd);
    onClick();
    expect(fn).toHaveBeenCalledTimes(1);
  });

  it('does not call task when disabled', () => {
    const fn = vi.fn();
    const cmd = RelayCommand.builder()
      .predicate(() => false)
      .task(fn)
      .build();
    const { onClick } = commandToButton(cmd);
    onClick(); // execute is gated by canExecute inside RelayCommand
    expect(fn).not.toHaveBeenCalled();
  });
});
