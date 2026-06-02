/**
 * Unit tests for the ConfirmDialog promise-based store contract.
 *
 * Tests the JS-side logic in `confirm-dialog.ts` — the
 * {@link confirmDialog} function, the {@link confirmRequest} store,
 * and the Promise resolution semantics on accept / cancel.
 *
 * The Svelte component (`ConfirmDialog.svelte`) renders from the store
 * state but is visual-only; rendering-pipeline tests live in the design
 * system documentation (`spec/design-system.md` §5.10) and are
 * covered via integration/manual review per the repo's testing policy
 * for layout components.
 */
import { describe, it, expect, beforeEach } from 'vitest';
import { get } from 'svelte/store';

import {
  confirmDialog,
  confirmRequest,
  type ConfirmOptions,
} from '../../src/routes/lib/confirm-dialog.js';

describe('confirmDialog store + Promise contract', () => {
  beforeEach(() => {
    // Each test starts with no pending request.
    confirmRequest.set(null);
  });

  it('publishes a request to the store when confirmDialog is called', () => {
    const opts: ConfirmOptions = {
      title: 'Delete decision?',
      body: 'This cascades into alternatives and coefficients.',
    };
    void confirmDialog(opts);
    const req = get(confirmRequest);
    expect(req).not.toBeNull();
    expect(req!.title).toBe(opts.title);
    expect(req!.body).toBe(opts.body);
  });

  it('returns a Promise that resolves true when the request resolves with true', async () => {
    const promise = confirmDialog({ title: 't', body: 'b' });
    const req = get(confirmRequest);
    expect(req).not.toBeNull();

    req!.resolve(true);
    confirmRequest.set(null);

    await expect(promise).resolves.toBe(true);
  });

  it('returns a Promise that resolves false when the request resolves with false', async () => {
    const promise = confirmDialog({ title: 't', body: 'b' });
    const req = get(confirmRequest);
    req!.resolve(false);
    confirmRequest.set(null);

    await expect(promise).resolves.toBe(false);
  });

  it('preserves the destructive flag on the published request', () => {
    void confirmDialog({
      title: 'Delete alternative?',
      body: 'Permanent.',
      destructive: true,
    });
    const req = get(confirmRequest);
    expect(req!.destructive).toBe(true);
  });

  it('omitting destructive results in undefined / falsy on the request', () => {
    void confirmDialog({ title: 't', body: 'b' });
    const req = get(confirmRequest);
    expect(req!.destructive).toBeFalsy();
  });

  it('preserves confirmLabel and cancelLabel when provided', () => {
    void confirmDialog({
      title: 't',
      body: 'b',
      confirmLabel: 'Discard',
      cancelLabel: 'Keep editing',
    });
    const req = get(confirmRequest);
    expect(req!.confirmLabel).toBe('Discard');
    expect(req!.cancelLabel).toBe('Keep editing');
  });

  it('omitted confirmLabel / cancelLabel come through as undefined (caller chooses defaults)', () => {
    void confirmDialog({ title: 't', body: 'b' });
    const req = get(confirmRequest);
    expect(req!.confirmLabel).toBeUndefined();
    expect(req!.cancelLabel).toBeUndefined();
  });

  it('resolve() is callable exactly once per request without throwing on re-call', () => {
    // The component clears the store after resolving; a second call to
    // resolve on the same Promise is a no-op in standard Promise semantics.
    // This pins that contract so future refactors don't introduce a
    // double-resolve crash.
    const promise = confirmDialog({ title: 't', body: 'b' });
    const req = get(confirmRequest);
    req!.resolve(true);
    expect(() => req!.resolve(false)).not.toThrow();
    return expect(promise).resolves.toBe(true);
  });

  it('two sequential calls each get their own resolver (request is replaced atomically)', async () => {
    const p1 = confirmDialog({ title: 'first', body: 'b1' });
    const req1 = get(confirmRequest);
    // Resolving the first request clears the store via the component's
    // decide() handler; simulate that here.
    req1!.resolve(true);
    confirmRequest.set(null);

    const p2 = confirmDialog({ title: 'second', body: 'b2' });
    const req2 = get(confirmRequest);
    expect(req2!.title).toBe('second');
    req2!.resolve(false);
    confirmRequest.set(null);

    await expect(p1).resolves.toBe(true);
    await expect(p2).resolves.toBe(false);
  });
});
