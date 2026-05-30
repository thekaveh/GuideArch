<script lang="ts">
  import type { ScenarioVM } from '../../viewmodels/scenario-vm.js';
  import { vmxToStore } from '../../view/adapters/vmx-to-svelte.js';

  export let vm: ScenarioVM;

  $: criticalConstraintsStore = vmxToStore(vm, 'criticalConstraints');
  $: scenarioStore = vmxToStore(vm, 'scenario');

  // Sort descending by eliminated (most-binding first)
  $: sorted = [...$criticalConstraintsStore].sort((a, b) => b.eliminated - a.eliminated);

  function eliminatedPct(eliminated: number, total: number): string {
    if (total === 0) return '—';
    return ((100 * eliminated) / total).toFixed(1) + '%';
  }
</script>

<section class="tab-content">
  {#if $scenarioStore === undefined}
    <div class="empty">Open a scenario to see critical constraints.</div>
  {:else if sorted.length === 0}
    <div class="empty">No constraints found.</div>
  {:else}
    <div class="table-wrap">
      <table>
        <thead>
          <tr>
            <th>Index</th>
            <th>Kind</th>
            <th>Eliminated</th>
            <th>Total</th>
            <th>Eliminated %</th>
            <th>Redundant</th>
          </tr>
        </thead>
        <tbody>
          {#each sorted as cc (cc.constraintIndex)}
            <tr class:redundant={cc.redundant}>
              <td class="idx">{cc.constraintIndex}</td>
              <td class="kind">{cc.kind}</td>
              <td class="num">{cc.eliminated}</td>
              <td class="num">{cc.total}</td>
              <td class="pct">{eliminatedPct(cc.eliminated, cc.total)}</td>
              <td class="bool">{cc.redundant ? 'Yes' : 'No'}</td>
            </tr>
          {/each}
        </tbody>
      </table>
    </div>
  {/if}
</section>

<style>
  .tab-content {
    display: flex;
    flex-direction: column;
    height: 100%;
    overflow: hidden;
  }

  /* §8 Empty state */
  .empty {
    flex: 1;
    display: flex;
    align-items: center;
    justify-content: center;
    color: var(--text-secondary);
    font-size: 14px;
  }

  .table-wrap {
    flex: 1;
    overflow: auto;
    padding: 16px 24px;
  }

  /* §5.3 Tables */
  table {
    width: 100%;
    border-collapse: collapse;
    font-size: 13px;
  }

  thead {
    position: sticky;
    top: 0;
    z-index: 1;
  }

  thead tr {
    background: var(--bg-surface);
  }

  th {
    height: 32px;
    padding: 0 8px;
    text-align: left;
    color: var(--text-secondary);
    font-size: 12px;
    font-weight: 500;
    border-bottom: 1px solid var(--border-subtle);
    white-space: nowrap;
    background: var(--bg-surface);
  }

  tbody tr:hover td {
    background: var(--bg-surface-2);
  }

  tbody tr.redundant {
    opacity: 0.45;
  }

  td {
    height: 36px;
    padding: 0 8px;
    border-bottom: 1px solid var(--border-subtle);
    background: var(--bg-page);
    vertical-align: middle;
  }

  /* §5.3 Numeric columns */
  .idx {
    color: var(--accent-hover);
    font-weight: 600;
    text-align: right;
    width: 4rem;
    font-variant-numeric: tabular-nums;
  }

  .kind {
    color: var(--text-primary);
  }

  .num {
    font-family: var(--font-mono);
    color: var(--success);
    text-align: right;
    width: 6rem;
    font-variant-numeric: tabular-nums;
  }

  .pct {
    font-family: var(--font-mono);
    color: var(--warning);
    width: 7rem;
    font-variant-numeric: tabular-nums;
  }

  .bool {
    color: var(--text-secondary);
    width: 5rem;
  }
</style>
