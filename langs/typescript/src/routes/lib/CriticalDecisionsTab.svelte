<script lang="ts">
  import type { ScenarioVM } from '../../viewmodels/scenario-vm.js';
  import { vmxToStore } from '../../view/adapters/vmx-to-svelte.js';

  export let vm: ScenarioVM;

  $: criticalDecisionsStore = vmxToStore(vm, 'criticalDecisions');
  $: scenarioStore = vmxToStore(vm, 'scenario');

  $: decisions = $scenarioStore?.decisions ?? [];

  function decisionName(id: string): string {
    return decisions.find((d) => d.id === id)?.name ?? id;
  }

  // Sort ascending by rank (lower score = more critical = lower rank)
  $: sorted = [...$criticalDecisionsStore].sort((a, b) => a.rank - b.rank);

  function fmtScore(s: number): string {
    return s.toPrecision(6);
  }

  function fmtTri(lower: number, modal: number, upper: number): string {
    return `(${fmtScore(lower)}, ${fmtScore(modal)}, ${fmtScore(upper)})`;
  }

  function fmtNorm(positive: number, average: number, negative: number): string {
    return `(${fmtScore(positive)}, ${fmtScore(average)}, ${fmtScore(negative)})`;
  }
</script>

<section class="tab-content">
  {#if $scenarioStore === undefined}
    <div class="empty">Open a scenario to see critical decisions.</div>
  {:else if sorted.length === 0}
    <div class="empty">No critical decisions computed.</div>
  {:else}
    <div class="table-wrap">
      <table>
        <thead>
          <tr>
            <th>Rank</th>
            <th>Decision</th>
            <th>Score</th>
            <th>Triangular Value</th>
            <th>Normalized</th>
          </tr>
        </thead>
        <tbody>
          {#each sorted as cd (cd.rank)}
            <tr>
              <td class="rank">{cd.rank + 1}</td>
              <td class="decision">{decisionName(cd.decisionId)}</td>
              <td class="score">{fmtScore(cd.score)}</td>
              <td class="tri"
                >{fmtTri(
                  cd.triangularValue.lower,
                  cd.triangularValue.modal,
                  cd.triangularValue.upper,
                )}</td
              >
              <td class="norm"
                >{fmtNorm(
                  cd.normalizedValue.positive,
                  cd.normalizedValue.average,
                  cd.normalizedValue.negative,
                )}</td
              >
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

  .decision {
    color: #e8e8ec;
  }

  .score {
    font-family: monospace;
    color: #6ee7b7;
    width: 9rem;
  }

  .tri {
    font-family: monospace;
    color: #94a3b8;
    font-size: 0.78rem;
  }

  .norm {
    font-family: monospace;
    color: #94a3b8;
    font-size: 0.78rem;
  }
</style>
