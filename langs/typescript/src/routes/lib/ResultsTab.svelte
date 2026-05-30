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
    <div class="empty">Open a scenario to see results.</div>
  {:else if top50.length === 0}
    <div class="empty">No feasible candidates found.</div>
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
            Fuzzy Profile{selectedCandidate !== null ? ` — Rank ${selectedCandidate.rank + 1}` : ''}
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

  .empty {
    flex: 1;
    display: flex;
    align-items: center;
    justify-content: center;
    color: #555566;
    font-size: 1rem;
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
    border-right: 1px solid #2e2e38;
  }

  .right-pane {
    flex: 0 0 40%;
    display: flex;
    flex-direction: column;
    overflow-y: auto;
    padding: 0.5rem 0.75rem;
    gap: 0.75rem;
    min-height: 0;
  }

  .chart-section {
    display: flex;
    flex-direction: column;
    gap: 0.25rem;
  }

  .chart-title {
    font-size: 0.75rem;
    font-weight: 600;
    color: #888899;
    padding: 0 0.25rem;
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

  tbody tr {
    cursor: pointer;
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

  tbody tr.selected {
    background: #2a1f4e;
    outline: 1px solid #7c3aed;
    outline-offset: -1px;
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
    max-width: 30vw;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }
</style>
