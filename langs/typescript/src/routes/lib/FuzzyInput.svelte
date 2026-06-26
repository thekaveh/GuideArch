<script lang="ts">
  /**
   * FuzzyInput — compact [L · M · U] triple numeric input.
   * Emits a 'change' event with { lower, modal, upper } when any value changes.
   * Shows yellow border when ordering is violated (lower > modal or modal > upper).
   */
  import { createEventDispatcher } from 'svelte';

  export let lower: number = 0;
  export let modal: number = 0;
  export let upper: number = 0;

  const dispatch = createEventDispatcher<{
    change: { lower: number; modal: number; upper: number };
  }>();

  $: orderingWarning = lower > modal || modal > upper;

  let lStr = String(lower);
  let mStr = String(modal);
  let uStr = String(upper);

  // Re-sync the local string state when the *prop* changes from outside
  // (file Open, Sample SAS, parent re-render with new coefficients). Track
  // the previous prop value so the reactive block only fires on an external
  // prop change — not on every local-string keystroke. The earlier
  // `Number(lStr) !== lower` guard didn't work because `bind:value={lStr}`
  // updates lStr per-input-event while `lower` only updates on emit (change/
  // blur), so a user pressing "1" over a "0.5" coefficient saw the cell snap
  // back to "0.5" instantly and could never edit.
  // The `prev*` trackers are read on each reactive re-run of the matching
  // `$:` block — they're not dead writes the way ESLint's
  // `no-useless-assignment` (single-pass scope analysis) reads them.
  /* eslint-disable no-useless-assignment */
  let prevLower = lower;
  let prevModal = modal;
  let prevUpper = upper;
  $: if (lower !== prevLower) {
    lStr = String(lower);
    prevLower = lower;
  }
  $: if (modal !== prevModal) {
    mStr = String(modal);
    prevModal = modal;
  }
  $: if (upper !== prevUpper) {
    uStr = String(upper);
    prevUpper = upper;
  }
  /* eslint-enable no-useless-assignment */

  function emit() {
    const l = parseFloat(lStr);
    const m = parseFloat(mStr);
    const u = parseFloat(uStr);
    if (!isNaN(l) && !isNaN(m) && !isNaN(u)) {
      lower = l;
      modal = m;
      upper = u;
      dispatch('change', { lower: l, modal: m, upper: u });
    }
  }
</script>

<span
  class="fuzzy-input"
  class:warn={orderingWarning}
  title={orderingWarning ? 'Ordering violated: lower ≤ modal ≤ upper required' : undefined}
>
  <input type="number" class="fnum" bind:value={lStr} on:change={emit} aria-label="lower" />
  <span class="dot">·</span>
  <input type="number" class="fnum" bind:value={mStr} on:change={emit} aria-label="modal" />
  <span class="dot">·</span>
  <input type="number" class="fnum" bind:value={uStr} on:change={emit} aria-label="upper" />
</span>

<style>
  /* §5.2 Input styling — 32px height, 6px radius, bg-surface-2, border-strong */
  .fuzzy-input {
    display: inline-flex;
    align-items: center;
    gap: 2px;
    border: 1px solid var(--border-strong);
    border-radius: 6px;
    padding: 0 4px;
    height: 32px;
    background: var(--bg-surface-2);
    transition: border-color 120ms ease-out;
  }

  .fuzzy-input:focus-within {
    border-color: var(--accent);
    box-shadow: 0 0 0 2px color-mix(in srgb, var(--accent) 25%, transparent);
  }

  /* §2.4 soft warning (lower>modal): a muted amber border only — no full-cell
     fill. A max property whose legacy triples are stored best-first (decreasing)
     is legitimately ordered yet trips this rule; the old amber border + 8% fill
     on every cell made the whole column read as an error wall. */
  .fuzzy-input.warn {
    border-color: color-mix(in srgb, var(--warning) 60%, var(--border-strong));
  }

  /* §5.3 Numeric / monospace for inputs */
  .fnum {
    width: 4.5rem;
    background: transparent;
    border: none;
    color: var(--text-primary);
    font-family: var(--font-mono);
    font-size: 13px;
    font-variant-numeric: tabular-nums;
    text-align: right;
    outline: none;
    padding: 0 2px;
  }

  /* Remove spinner arrows */
  .fnum::-webkit-outer-spin-button,
  .fnum::-webkit-inner-spin-button {
    -webkit-appearance: none;
    appearance: none;
    margin: 0;
  }
  .fnum[type='number'] {
    -moz-appearance: textfield;
    appearance: textfield;
  }

  .dot {
    color: var(--text-muted);
    font-size: 12px;
    user-select: none;
  }
</style>
