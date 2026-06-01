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
import { writable } from 'svelte/store';

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
    confirmRequest.set({ ...opts, resolve });
  });
}
