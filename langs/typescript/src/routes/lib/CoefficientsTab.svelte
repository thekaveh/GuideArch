<script lang="ts">
  import type { ScenarioVM } from '../../viewmodels/scenario-vm.js';
  import { vmxToStore } from '../../view/adapters/vmx-to-svelte.js';
  import { ScenarioMutationError } from '../../viewmodels/scenario-vm.js';
  import FuzzyInput from './FuzzyInput.svelte';
  import EmptyState from './EmptyState.svelte';
  import SectionHeader from './SectionHeader.svelte';

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
  {#if $scenarioStore === undefined}
    <div class="empty">
      <div class="empty-headline">No scenario loaded.</div>
      <div class="empty-body">
        Click <strong>Open Sample SAS</strong> in the toolbar to see the fuzzy coefficient matrix.
      </div>
    </div>
  {:else if decisions.length === 0 || properties.length === 0}
    <EmptyState
      headline="Coefficient matrix is not ready"
      body="The matrix needs at least one decision and one property. Add them on their tabs and the cells will populate here automatically."
    />
  {:else}
    <SectionHeader
      title="Coefficients"
      subtitle="Fuzzy values per (alternative, property): lower · modal · upper. Edit to shift how each alternative scores."
    />
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

  .grid-wrap {
    flex: 1;
    overflow: auto;
    padding: 0;
  }

  /* §5.3 Coefficient matrix table */
  .coeff-table {
    border-collapse: collapse;
    font-size: 13px;
    width: 100%;
  }

  thead tr {
    background: var(--bg-surface);
    position: sticky;
    top: 0;
    z-index: 2;
  }

  .prop-header {
    padding: 6px 8px;
    border-bottom: 1px solid var(--border-subtle);
    border-right: 1px solid var(--border-subtle);
    min-width: 9rem;
    text-align: center;
    background: var(--bg-surface);
  }

  .prop-name {
    color: var(--text-primary);
    font-weight: 600;
    font-size: 12px;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
    max-width: 13rem;
  }

  .prop-meta {
    display: flex;
    gap: 4px;
    justify-content: center;
    margin-top: 2px;
  }

  /* §5.6 Chip-like badges */
  .kind-badge {
    display: inline-flex;
    align-items: center;
    height: 16px;
    padding: 0 6px;
    background: color-mix(in srgb, var(--info) 12%, transparent);
    color: var(--info);
    border-radius: 4px;
    font-size: 11px;
    font-weight: 500;
  }

  .kind-badge.max {
    background: color-mix(in srgb, var(--accent) 12%, transparent);
    color: var(--accent-hover);
  }

  .weight-badge {
    display: inline-flex;
    align-items: center;
    height: 16px;
    padding: 0 6px;
    background: color-mix(in srgb, var(--success) 12%, transparent);
    color: var(--success);
    border-radius: 4px;
    font-size: 11px;
    font-family: var(--font-mono);
  }

  .sticky-col {
    position: sticky;
    left: 0;
    z-index: 1;
    background: var(--bg-page);
  }

  .group-col {
    min-width: 12rem;
    padding: 0 8px;
    height: 32px;
    color: var(--text-secondary);
    font-size: 12px;
    font-weight: 500;
    border-bottom: 1px solid var(--border-subtle);
    border-right: 1px solid var(--border-subtle);
    background: var(--bg-surface);
  }

  .group-row td {
    background: var(--bg-surface-2);
    z-index: 2;
  }

  .group-label {
    padding: 0 8px;
    height: 32px;
    vertical-align: middle;
    color: var(--accent-hover);
    font-weight: 600;
    font-size: 13px;
    border-bottom: 1px solid var(--border-subtle);
    border-right: 1px solid var(--border-subtle);
  }

  .alt-label {
    padding: 0 8px;
    height: 36px;
    vertical-align: middle;
    border-bottom: 1px solid var(--border-subtle);
    border-right: 1px solid var(--border-subtle);
    background: var(--bg-page);
    min-width: 12rem;
    width: 12rem;
  }

  .alt-name {
    display: block;
    color: var(--text-primary);
    font-size: 13px;
  }

  .coeff-cell {
    padding: 4px 6px;
    height: 36px;
    vertical-align: middle;
    border-bottom: 1px solid var(--border-subtle);
    border-right: 1px solid var(--border-subtle);
    text-align: center;
    background: var(--bg-page);
  }

  .missing {
    color: var(--text-muted);
    font-size: 13px;
  }
</style>
