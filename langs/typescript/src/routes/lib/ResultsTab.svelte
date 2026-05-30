<script lang="ts">
  import type { ScenarioVM } from '../../viewmodels/scenario-vm.js';
  import { vmxToStore } from '../../view/adapters/vmx-to-svelte.js';

  export let vm: ScenarioVM;

  $: candidatesStore = vmxToStore(vm, 'candidates');
  $: scenarioStore = vmxToStore(vm, 'scenario');

  $: top50 = $candidatesStore.slice(0, 50);
  $: alternatives = $scenarioStore?.alternatives ?? [];

  function altName(id: string): string {
    return alternatives.find((a) => a.id === id)?.name ?? id;
  }
</script>

<section class="tab-content">
  {#if $scenarioStore === undefined}
    <div class="empty">Open a scenario to see results.</div>
  {:else if top50.length === 0}
    <div class="empty">No feasible candidates found.</div>
  {:else}
    <div class="table-wrap">
      <table>
        <thead>
          <tr>
            <th>Rank</th>
            <th>Score</th>
            <th>Alternatives</th>
          </tr>
        </thead>
        <tbody>
          {#each top50 as c (c.rank)}
            <tr>
              <td class="rank">{c.rank + 1}</td>
              <td class="score">{c.score.toFixed(6)}</td>
              <td class="alts">
                {c.alternativeIds.map(altName).join(', ')}
              </td>
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

  td {
    padding: 0.4rem 0.75rem;
    border-bottom: 1px solid #22222c;
    vertical-align: middle;
  }

  .rank {
    color: #a78bfa;
    font-weight: 600;
    text-align: right;
    width: 3.5rem;
  }

  .score {
    font-family: monospace;
    color: #6ee7b7;
    width: 8rem;
  }

  .alts {
    color: #94a3b8;
    max-width: 60vw;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }
</style>
