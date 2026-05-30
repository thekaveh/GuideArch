/**
 * vmxToSvelte adapter — bridges VMx observable properties to Svelte stores.
 *
 * ## Semantics
 *
 * ### vmxToStore<T>(vm, propName)
 * Returns a Svelte `Readable<T>` that reflects the current value of
 * `vm.model[propName]`. It subscribes to the VMx `MessageHub` stream
 * (`vm` must be wired to a live `MessageHub`, not `NullMessageHub`) and
 * re-emits whenever a `PropertyChangedMessage` arrives for this VM with
 * the property name "model" (VMx emits "model" whenever the whole model
 * changes, which is the pattern used by `ComponentVMOf`).
 *
 * The store emits the initial value synchronously on subscription and then
 * emits again on every relevant hub message.
 *
 * ### commandToButton(cmd)
 * Returns `{ onClick, disabled }` where:
 * - `onClick` calls `cmd.execute()` when invoked.
 * - `disabled` is a `Readable<boolean>` that reflects `!cmd.canExecute()`.
 *   It re-evaluates whenever `cmd.canExecuteChanged` fires.
 *
 * Debouncing (spec §3.3 guidance) belongs in the UI layer, not here.
 *
 * ### commandOfToButton<T>(cmd, parameter)
 * Same as `commandToButton` but for `RelayCommandOf<T>`. The `parameter`
 * argument is captured at call time and used as the fixed argument to both
 * `canExecute` and `execute`.
 */
import { readable, writable, type Readable } from 'svelte/store';
import { filter } from 'rxjs/operators';
import type { ComponentVMOf } from 'vmx';
import { type RelayCommand, type RelayCommandOf } from 'vmx';

// ---------------------------------------------------------------------------
// vmxToStore
// ---------------------------------------------------------------------------

/**
 * Wrap a VMx modeled property as a Svelte `Readable<T>`.
 *
 * @param vm       - A `ComponentVMOf<M>` wired to a live `MessageHub`.
 * @param propName - The key of `M` to expose; the store emits `vm.model[propName]`.
 */
export function vmxToStore<M, K extends keyof M>(
  vm: ComponentVMOf<M>,
  propName: K,
): Readable<M[K]> {
  return readable<M[K]>(vm.model[propName], (set) => {
    // VMx emits a PropertyChangedMessage with propertyName === "model" whenever
    // ComponentVMOf.model setter fires (_setModel). Filter for our vm + "model".
    // We use the hub's messages observable, accessible via the vm's hub property.
    // ComponentVMBase exposes propertyChanged (a stream of property name strings)
    // directly on the VM — use that as a lighter alternative to hub filtering.
    const sub = vm.propertyChanged.pipe(filter((name) => name === 'model')).subscribe(() => {
      set(vm.model[propName]);
    });

    return () => sub.unsubscribe();
  });
}

// ---------------------------------------------------------------------------
// vmxStoreAll — convenience: watch ALL model changes and return the full model
// ---------------------------------------------------------------------------

/**
 * Returns a `Readable<M>` that emits the entire model whenever it changes.
 */
export function vmxStoreAll<M>(vm: ComponentVMOf<M>): Readable<M> {
  return readable<M>(vm.model, (set) => {
    const sub = vm.propertyChanged
      .pipe(filter((name) => name === 'model'))
      .subscribe(() => set(vm.model));
    return () => sub.unsubscribe();
  });
}

// ---------------------------------------------------------------------------
// commandToButton
// ---------------------------------------------------------------------------

/**
 * Bind a `RelayCommand` to a Svelte button.
 *
 * Usage:
 * ```svelte
 * <script>
 *   const { onClick, disabled } = commandToButton(vm.solveCmd);
 * </script>
 * <button on:click={onClick} disabled={$disabled}>Solve</button>
 * ```
 */
export function commandToButton(cmd: RelayCommand): {
  onClick: () => void;
  disabled: Readable<boolean>;
} {
  const disabledStore = writable(!cmd.canExecute());

  const sub = cmd.canExecuteChanged.subscribe(() => {
    disabledStore.set(!cmd.canExecute());
  });

  // Attach a teardown so the subscription is released when the store is GC'd.
  // Svelte stores do not guarantee teardown for writable stores unless they
  // are created with the `readable` factory; we rely on the component's
  // lifecycle for cleanup in practice (M2 scope).
  const disabled: Readable<boolean> = {
    subscribe: (run, invalidate?) => {
      const unsub = disabledStore.subscribe(run, invalidate);
      return () => {
        unsub();
        sub.unsubscribe();
      };
    },
  };

  return {
    onClick: () => cmd.execute(),
    disabled,
  };
}

// ---------------------------------------------------------------------------
// commandOfToButton
// ---------------------------------------------------------------------------

/**
 * Bind a `RelayCommandOf<T>` to a Svelte button, with a fixed parameter.
 *
 * Usage:
 * ```svelte
 * <script>
 *   const { onClick, disabled } = commandOfToButton(vm.openCmd, selectedPath);
 * </script>
 * <button on:click={onClick} disabled={$disabled}>Open</button>
 * ```
 */
export function commandOfToButton<T>(
  cmd: RelayCommandOf<T>,
  parameter: T,
): {
  onClick: () => void;
  disabled: Readable<boolean>;
} {
  const disabledStore = writable(!cmd.canExecute(parameter));

  const sub = cmd.canExecuteChanged.subscribe(() => {
    disabledStore.set(!cmd.canExecute(parameter));
  });

  const disabled: Readable<boolean> = {
    subscribe: (run, invalidate?) => {
      const unsub = disabledStore.subscribe(run, invalidate);
      return () => {
        unsub();
        sub.unsubscribe();
      };
    },
  };

  return {
    onClick: () => cmd.execute(parameter),
    disabled,
  };
}
