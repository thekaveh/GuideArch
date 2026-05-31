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
    <div class="empty">
      <div class="empty-headline">No scenario loaded.</div>
      <div class="empty-body">
        Click <strong>Open Sample SAS</strong> in the toolbar to see which architectural decisions have
        the greatest impact on the solution.
      </div>
    </div>
  {:else if sorted.length === 0}
    <div class="empty">
      <div class="empty-headline">No critical decisions computed.</div>
      <div class="empty-body">
        Add decisions and alternatives, then solve to see criticality rankings.
      </div>
    </div>
  {:else}
    <div class="table-wrap">
      <table>
        <thead>
          <tr>
            <th>Rank</th>
            <th>Decision</th>
            <th>Score</th>
            <th>Triangular value</th>
            <th>Normalized</th>
          </tr>
        </thead>
        <tbody>
          {#each sorted as cd (cd.rank)}
            <tr>
              <td class="rank">{cd.rank}</td>
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

  /* §8 Empty state */
  .empty {
    flex: 1;
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    gap: 8px;
    text-align: center;
    padding: 32px;
  }

  .empty-headline {
    color: var(--text-secondary);
    font-size: 14px;
    font-weight: 500;
  }

  .empty-body {
    color: var(--text-muted);
    font-size: 13px;
    max-width: 28rem;
    line-height: 1.6;
  }

  .empty-body strong {
    color: var(--text-secondary);
    font-weight: 600;
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

  td {
    height: 36px;
    padding: 0 8px;
    border-bottom: 1px solid var(--border-subtle);
    background: var(--bg-page);
    vertical-align: middle;
  }

  /* §5.3 Numeric columns */
  .rank {
    color: var(--accent-hover);
    font-weight: 600;
    text-align: right;
    width: 3.5rem;
    font-variant-numeric: tabular-nums;
  }

  .decision {
    color: var(--text-primary);
  }

  .score {
    font-family: var(--font-mono);
    color: var(--success);
    width: 9rem;
    font-variant-numeric: tabular-nums;
  }

  .tri {
    font-family: var(--font-mono);
    color: var(--text-secondary);
    font-size: 12px;
    font-variant-numeric: tabular-nums;
  }

  .norm {
    font-family: var(--font-mono);
    color: var(--text-secondary);
    font-size: 12px;
    font-variant-numeric: tabular-nums;
  }
</style>
