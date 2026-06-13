/**
 * Promise-based confirm dialog — drop-in replacement for the browser's
 * native `confirm()` that matches GuideArch's design language and supports
 * a destructive variant for delete prompts.
 *
 * Usage:
 *
 * ```ts
 * if (await confirmDialog({
 *   title: 'Delete decision?',
 *   body: 'This removes its alternatives and coefficients.',
 *   destructive: true,
 * })) {
 *   // user clicked Confirm
 * }
 * ```
 *
 * The {@link ConfirmDialog.svelte} component must be mounted once at the
 * page level — it watches {@link confirmRequest} and renders whenever a
 * request is pending, resolving the promise on user input.
 */
import { get, writable } from 'svelte/store';

export interface ConfirmOptions {
  title: string;
  body: string;
  confirmLabel?: string;
  cancelLabel?: string;
  /** Destructive prompts (delete) get a danger-colored primary button. */
  destructive?: boolean;
}

interface ConfirmRequest extends ConfirmOptions {
  resolve: (ok: boolean) => void;
}

export const confirmRequest = writable<ConfirmRequest | null>(null);

export function confirmDialog(opts: ConfirmOptions): Promise<boolean> {
  return new Promise<boolean>((resolve) => {
    // If two click handlers fire close together (e.g. a user double-clicks a
    // delete button), the second call would replace the first request's
    // ConfirmRequest entry in the store and the first promise would never
    // resolve. Settle any in-flight request as a cancel before installing the
    // new one so its `await` releases.
    const pending = get(confirmRequest);
    if (pending) pending.resolve(false);
    confirmRequest.set({ ...opts, resolve });
  });
}
