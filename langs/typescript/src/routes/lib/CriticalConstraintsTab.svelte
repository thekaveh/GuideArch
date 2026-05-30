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

  .empty {
    flex: 1;
    display: flex;
    align-items: center;
    justify-content: center;
    color: #555566;
    font-size: 1rem;
  }

  .table-wrap {
    flex: 1;
    overflow: auto;
    padding: 1rem 1.25rem;
  }

  table {
    width: 100%;
    border-collapse: collapse;
    font-size: 0.85rem;
  }

  thead tr {
    background: #1a1a20;
  }

  th {
    padding: 0.5rem 0.75rem;
    text-align: left;
    color: #888899;
    font-weight: 600;
    border-bottom: 1px solid #2e2e38;
    white-space: nowrap;
  }

  tbody tr:nth-child(odd) {
    background: #111118;
  }

  tbody tr:nth-child(even) {
    background: #0f0f16;
  }

  tbody tr:hover {
    background: #1e1e28;
  }

  tbody tr.redundant {
    opacity: 0.45;
  }

  td {
    padding: 0.4rem 0.75rem;
    border-bottom: 1px solid #22222c;
    vertical-align: middle;
  }

  .idx {
    color: #a78bfa;
    font-weight: 600;
    text-align: right;
    width: 4rem;
  }

  .kind {
    color: #e8e8ec;
  }

  .num {
    font-family: monospace;
    color: #6ee7b7;
    text-align: right;
    width: 6rem;
  }

  .pct {
    font-family: monospace;
    color: #f59e0b;
    width: 7rem;
  }

  .bool {
    color: #94a3b8;
    width: 5rem;
  }
</style>
