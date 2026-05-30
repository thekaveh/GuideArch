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
  .fuzzy-input {
    display: inline-flex;
    align-items: center;
    gap: 2px;
    border: 1px solid #3e3e50;
    border-radius: 4px;
    padding: 1px 4px;
    background: #1a1a28;
  }

  .fuzzy-input.warn {
    border-color: #ca8a04;
    background: #1e1800;
  }

  .fnum {
    width: 4.5rem;
    background: transparent;
    border: none;
    color: #e8e8ec;
    font-family: monospace;
    font-size: 0.8rem;
    text-align: right;
    outline: none;
    padding: 2px 2px;
  }

  /* Remove spinner arrows */
  .fnum::-webkit-outer-spin-button,
  .fnum::-webkit-inner-spin-button {
    -webkit-appearance: none;
    margin: 0;
  }
  .fnum[type='number'] {
    -moz-appearance: textfield;
  }

  .dot {
    color: #555570;
    font-size: 0.75rem;
    user-select: none;
  }
</style>
