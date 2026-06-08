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
  // (file Open, Sample SAS, parent re-render with new coefficients). Without
  // this, the local strings stay at their first-mount value because
  // CoefficientsTab keys the `{#each}` by alt+prop id, so component instances
  // are reused across re-solves and sample swaps. The `Number(lStr) === lower`
  // guard makes the resync a no-op while the user is mid-edit (after emit()
  // sets the prop to the value the user just typed) — only external mutations
  // produce a divergence and trigger the reset.
  $: if (Number(lStr) !== lower) lStr = String(lower);
  $: if (Number(mStr) !== modal) mStr = String(modal);
  $: if (Number(uStr) !== upper) uStr = String(upper);

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

  .fuzzy-input.warn {
    border-color: var(--warning);
    background: color-mix(in srgb, var(--warning) 8%, var(--bg-surface-2));
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
