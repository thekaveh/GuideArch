<script lang="ts">
  import type { ScenarioVM } from '../../viewmodels/scenario-vm.js';
  import { vmxToStore } from '../../view/adapters/vmx-to-svelte.js';
  import RankedCandidatesChart from './RankedCandidatesChart.svelte';
  import FuzzyTriangleChart from './FuzzyTriangleChart.svelte';
  import { buildCandidateBarData, buildTriangleSeriesData } from '../../view/chart-data.js';

  export let vm: ScenarioVM;

  $: candidatesStore = vmxToStore(vm, 'candidates');
  $: scenarioStore = vmxToStore(vm, 'scenario');
  $: selectedIndexStore = vmxToStore(vm, 'selectedCandidateIndex');

  $: top50 = $candidatesStore.slice(0, 50);
  $: alternatives = $scenarioStore?.alternatives ?? [];
  $: properties = $scenarioStore?.properties ?? [];
  $: coefficients = $scenarioStore?.coefficients ?? [];

  // Chart A data — top 30 by rank
  $: barData = buildCandidateBarData($candidatesStore, alternatives, 30);

  // Chart B data — triangles for selected candidate
  $: selectedCandidate =
    $selectedIndexStore !== null ? ($candidatesStore[$selectedIndexStore] ?? null) : null;
  $: triangleSeries =
    selectedCandidate !== null
      ? buildTriangleSeriesData(selectedCandidate, properties, coefficients)
      : [];

  function altName(id: string): string {
    return alternatives.find((a) => a.id === id)?.name ?? id;
  }

  function handleBarSelect(rank: number) {
    vm.setSelectedCandidateIndex(rank);
  }
</script>

<section class="tab-content">
  {#if $scenarioStore === undefined}
    <div class="empty">
      <div class="empty-headline">No scenario loaded.</div>
      <div class="empty-body">
        Click <strong>Open Sample SAS</strong> in the toolbar to try the example scenario and see ranked
        candidates here.
      </div>
    </div>
  {:else if top50.length === 0}
    <div class="empty">
      <div class="empty-headline">No feasible candidates found.</div>
      <div class="empty-body">
        All alternative combinations were eliminated by constraints, or the scenario has no
        alternatives. Add alternatives and relax constraints to see results.
      </div>
    </div>
  {:else}
    <div class="split-pane">
      <!-- Left: ranked candidates table (~60%) -->
      <div class="left-pane">
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
                <tr
                  class:selected={c.rank === $selectedIndexStore}
                  tabindex={0}
                  on:click={() => vm.setSelectedCandidateIndex(c.rank)}
                  on:keydown={(e) => {
                    if (e.key === 'Enter' || e.key === ' ') {
                      e.preventDefault();
                      vm.setSelectedCandidateIndex(c.rank);
                    }
                  }}
                >
                  <td class="rank">{c.rank}</td>
                  <td class="score">{c.score.toFixed(6)}</td>
                  <td class="alts">
                    {c.alternativeIds.map(altName).join(', ')}
                  </td>
                </tr>
              {/each}
            </tbody>
          </table>
        </div>
      </div>

      <!-- Right: charts (~40%) -->
      <div class="right-pane">
        <div class="chart-section">
          <div class="chart-title">Top 30 by Score</div>
          <RankedCandidatesChart
            data={barData}
            selectedIndex={$selectedIndexStore}
            onSelect={handleBarSelect}
          />
        </div>
        <div class="chart-section">
          <div class="chart-title">
            Fuzzy Profile{selectedCandidate !== null ? ` — Rank ${selectedCandidate.rank}` : ''}
          </div>
          <FuzzyTriangleChart series={triangleSeries} />
        </div>
      </div>
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

  .split-pane {
    flex: 1;
    display: flex;
    overflow: hidden;
    min-height: 0;
  }

  .left-pane {
    flex: 0 0 60%;
    display: flex;
    flex-direction: column;
    overflow: hidden;
    border-right: 1px solid var(--border-subtle);
  }

  .right-pane {
    flex: 0 0 40%;
    display: flex;
    flex-direction: column;
    overflow-y: auto;
    padding: 12px;
    gap: 12px;
    min-height: 0;
    background: var(--bg-surface);
  }

  /* §5.5 Card for chart sections */
  .chart-section {
    background: var(--bg-surface);
    border: 1px solid var(--border-strong);
    border-radius: 8px;
    padding: 16px;
    display: flex;
    flex-direction: column;
    gap: 8px;
  }

  .chart-title {
    font-size: 12px;
    font-weight: 600;
    color: var(--text-secondary);
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

  tbody tr {
    cursor: pointer;
  }

  tbody tr:hover td {
    background: var(--bg-surface-2);
  }

  /* §5.3 Selected row */
  tbody tr.selected td {
    background: var(--accent-muted);
    border-left: 2px solid var(--accent);
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

  .score {
    font-family: var(--font-mono);
    color: var(--success);
    width: 8rem;
    font-variant-numeric: tabular-nums;
  }

  .alts {
    color: var(--text-secondary);
    max-width: 30vw;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }
</style>
