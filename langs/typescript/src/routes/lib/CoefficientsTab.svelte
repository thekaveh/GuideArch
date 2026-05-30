<script lang="ts">
  import type { ScenarioVM } from '../../viewmodels/scenario-vm.js';
  import { vmxToStore } from '../../view/adapters/vmx-to-svelte.js';
  import { ScenarioMutationError } from '../../viewmodels/scenario-vm.js';
  import FuzzyInput from './FuzzyInput.svelte';

  export let vm: ScenarioVM;
  export let onError: (msg: string) => void = () => {};

  $: scenarioStore = vmxToStore(vm, 'scenario');
  $: scenario = $scenarioStore;
  $: decisions = scenario?.decisions ?? [];
  $: alternatives = scenario?.alternatives ?? [];
  $: properties = scenario?.properties ?? [];
  $: coefficients = scenario?.coefficients ?? [];

  function altsForDecision(decId: string) {
    return alternatives.filter((a) => a.decisionId === decId);
  }

  function getCoeff(altId: string, propId: string) {
    return coefficients.find((c) => c.alternativeId === altId && c.propertyId === propId);
  }

  function handleFuzzyChange(
    altId: string,
    propId: string,
    e: CustomEvent<{ lower: number; modal: number; upper: number }>,
  ) {
    const { lower, modal, upper } = e.detail;
    try {
      vm.updateCoefficient(altId, propId, lower, modal, upper);
    } catch (err) {
      if (err instanceof ScenarioMutationError) onError(err.message);
      else throw err;
    }
  }
</script>

<section class="tab-content">
  {#if decisions.length === 0 || properties.length === 0}
    <div class="empty">
      Add decisions, alternatives, and properties first to build the coefficient matrix.
    </div>
  {:else}
    <div class="grid-wrap">
      <table class="coeff-table">
        <thead>
          <tr>
            <th class="sticky-col group-col">Decision / Alternative</th>
            {#each properties as p (p.id)}
              <th class="prop-header">
                <div class="prop-name">{p.name}</div>
                <div class="prop-meta">
                  <span class="kind-badge" class:max={p.kind === 'max'}>{p.kind}</span>
                  <span class="weight-badge">w={p.weight}</span>
                </div>
              </th>
            {/each}
          </tr>
        </thead>
        <tbody>
          {#each decisions as dec (dec.id)}
            <tr class="group-row">
              <td class="sticky-col group-label" colspan={properties.length + 1}>
                {dec.name}
              </td>
            </tr>
            {#each altsForDecision(dec.id) as alt (alt.id)}
              <tr>
                <td class="sticky-col alt-label">
                  <span class="alt-name">{alt.name}</span>
                  <span class="alt-id mono">{alt.id}</span>
                </td>
                {#each properties as p (p.id)}
                  {@const coeff = getCoeff(alt.id, p.id)}
                  <td class="coeff-cell">
                    {#if coeff}
                      <FuzzyInput
                        lower={coeff.value.lower}
                        modal={coeff.value.modal}
                        upper={coeff.value.upper}
                        on:change={(e) => handleFuzzyChange(alt.id, p.id, e)}
                      />
                    {:else}
                      <span class="missing">—</span>
                    {/if}
                  </td>
                {/each}
              </tr>
            {/each}
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
    font-size: 0.9rem;
    text-align: center;
    padding: 2rem;
  }

  .grid-wrap {
    flex: 1;
    overflow: auto;
    padding: 0;
  }

  .coeff-table {
    border-collapse: collapse;
    font-size: 0.82rem;
    min-width: 100%;
  }

  thead tr {
    background: #1a1a20;
    position: sticky;
    top: 0;
    z-index: 2;
  }

  .prop-header {
    padding: 0.4rem 0.5rem;
    border-bottom: 1px solid #2e2e38;
    border-right: 1px solid #2e2e38;
    min-width: 14rem;
    text-align: center;
  }

  .prop-name {
    color: #e8e8ec;
    font-weight: 600;
    font-size: 0.82rem;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
    max-width: 13rem;
  }

  .prop-meta {
    display: flex;
    gap: 0.3rem;
    justify-content: center;
    margin-top: 2px;
  }

  .kind-badge {
    padding: 0 0.35rem;
    background: #1e2a3a;
    color: #93c5fd;
    border-radius: 3px;
    font-size: 0.7rem;
    font-weight: 600;
    border: 1px solid #1e3a5a;
  }

  .kind-badge.max {
    background: #2a1e3a;
    color: #c4b5fd;
    border-color: #3a1e5a;
  }

  .weight-badge {
    padding: 0 0.35rem;
    background: #1e3a2a;
    color: #6ee7b7;
    border-radius: 3px;
    font-size: 0.7rem;
    font-family: monospace;
    border: 1px solid #1e5a3a;
  }

  .sticky-col {
    position: sticky;
    left: 0;
    z-index: 1;
    background: #16161e;
  }

  .group-col {
    min-width: 14rem;
    padding: 0.4rem 0.65rem;
    color: #888899;
    font-weight: 600;
    border-bottom: 1px solid #2e2e38;
    border-right: 1px solid #2e2e38;
  }

  .group-row td {
    background: #1a1a28;
    z-index: 2;
  }

  .group-label {
    padding: 0.4rem 0.65rem;
    color: #c4b5fd;
    font-weight: 700;
    font-size: 0.84rem;
    border-bottom: 1px solid #2e2e38;
    border-right: 1px solid #2e2e38;
  }

  .alt-label {
    padding: 0.32rem 0.65rem;
    border-bottom: 1px solid #22222c;
    border-right: 1px solid #2e2e38;
    background: #15151e;
    min-width: 14rem;
  }

  .alt-name {
    display: block;
    color: #e8e8ec;
    font-size: 0.82rem;
  }

  .alt-id {
    display: block;
    color: #555566;
    font-size: 0.72rem;
  }

  .coeff-cell {
    padding: 0.3rem 0.4rem;
    border-bottom: 1px solid #22222c;
    border-right: 1px solid #22222c;
    text-align: center;
  }

  .missing {
    color: #555566;
    font-size: 0.8rem;
  }

  .mono {
    font-family: monospace;
  }
</style>
