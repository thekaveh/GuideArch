<script lang="ts">
  import type { ScenarioVM } from '../../viewmodels/scenario-vm.js';
  import { vmxToStore } from '../../view/adapters/vmx-to-svelte.js';
  import { ScenarioMutationError } from '../../viewmodels/scenario-vm.js';
  import EmptyState from './EmptyState.svelte';
  import SectionHeader from './SectionHeader.svelte';

  export let vm: ScenarioVM;
  export let onError: (msg: string) => void = () => {};

  $: scenarioStore = vmxToStore(vm, 'scenario');
  $: properties = $scenarioStore?.properties ?? [];

  function handleAdd() {
    try {
      vm.addProperty();
    } catch (err) {
      if (err instanceof ScenarioMutationError) onError(err.message);
      else throw err;
    }
  }

  function handleDelete(id: string, name: string) {
    if (
      !confirm(
        `Delete property "${name}"? This removes its coefficients and threshold constraints.`,
      )
    )
      return;
    try {
      vm.deleteProperty(id);
    } catch (err) {
      if (err instanceof ScenarioMutationError) onError(err.message);
      else throw err;
    }
  }

  function handleNameBlur(id: string, e: FocusEvent) {
    const val = (e.target as HTMLInputElement).value.trim();
    if (!val) return;
    try {
      vm.updatePropertyName(id, val);
    } catch (err) {
      if (err instanceof ScenarioMutationError) onError(err.message);
      else throw err;
    }
  }

  function handleNameKeydown(e: KeyboardEvent) {
    if (e.key === 'Enter') (e.target as HTMLInputElement).blur();
  }

  function handleKindChange(id: string, e: Event) {
    const val = (e.target as HTMLSelectElement).value as 'min' | 'max';
    try {
      vm.updatePropertyKind(id, val);
    } catch (err) {
      if (err instanceof ScenarioMutationError) onError(err.message);
      else throw err;
    }
  }

  function handleWeightBlur(id: string, e: FocusEvent) {
    const raw = (e.target as HTMLInputElement).value;
    const val = parseFloat(raw);
    if (isNaN(val) || val <= 0) {
      onError('Property weight must be a positive number.');
      return;
    }
    try {
      vm.updatePropertyWeight(id, val);
    } catch (err) {
      if (err instanceof ScenarioMutationError) onError(err.message);
      else throw err;
    }
  }

  function handleWeightKeydown(e: KeyboardEvent) {
    if (e.key === 'Enter') (e.target as HTMLInputElement).blur();
  }
</script>

<section class="tab-content">
  {#if $scenarioStore === undefined}
    <div class="empty">
      <div class="empty-headline">No scenario loaded.</div>
      <div class="empty-body">
        Click <strong>Open Sample SAS</strong> in the toolbar to try the example, or click
        <strong>New</strong> to create a blank scenario.
      </div>
    </div>
  {:else}
    <SectionHeader
      title="Properties"
      subtitle="Quality criteria — each with a kind (max / min) and a weight; together they decide how candidates rank."
      action={{ label: '+ Add Property', onClick: handleAdd }}
    />
    {#if properties.length === 0}
      <EmptyState
        headline="No properties yet"
        body="Quality properties — performance, cost, reliability — are how candidates get ranked. Add at least one, then weight it."
        primary={{ label: '+ Add Property', onClick: handleAdd }}
      />
    {:else}
      <div class="table-wrap">
        <table>
          <thead>
            <tr>
              <th>ID</th>
              <th>Name</th>
              <th>Kind</th>
              <th>Weight</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {#each properties as p (p.id)}
              <tr>
                <td class="mono">{p.id}</td>
                <td>
                  <input
                    class="name-input"
                    value={p.name}
                    on:blur={(e) => handleNameBlur(p.id, e)}
                    on:keydown={handleNameKeydown}
                  />
                </td>
                <td>
                  <select
                    class="kind-select"
                    value={p.kind}
                    on:change={(e) => handleKindChange(p.id, e)}
                  >
                    <option value="min">min</option>
                    <option value="max">max</option>
                  </select>
                </td>
                <td>
                  <input
                    type="number"
                    class="weight-input"
                    value={p.weight}
                    step="0.1"
                    min="0.001"
                    on:blur={(e) => handleWeightBlur(p.id, e)}
                    on:keydown={handleWeightKeydown}
                  />
                </td>
                <td>
                  <button class="btn-delete" on:click={() => handleDelete(p.id, p.name)}>
                    Delete
                  </button>
                </td>
              </tr>
            {/each}
          </tbody>
        </table>
      </div>
    {/if}
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

  th {
    height: 32px;
    padding: 0 8px;
    text-align: left;
    color: var(--text-secondary);
    font-size: 12px;
    font-weight: 500;
    border-bottom: 1px solid var(--border-subtle);
    background: var(--bg-surface);
  }

  td {
    height: 36px;
    padding: 0 8px;
    border-bottom: 1px solid var(--border-subtle);
    background: var(--bg-page);
    vertical-align: middle;
  }

  tbody tr:hover td {
    background: var(--bg-surface-2);
  }

  .mono {
    font-family: var(--font-mono);
    font-size: 12px;
    color: var(--text-secondary);
  }

  .name-input {
    background: transparent;
    border: 1px solid transparent;
    color: var(--text-primary);
    font-size: 13px;
    padding: 4px 8px;
    border-radius: 6px;
    width: 100%;
    min-width: 10rem;
    transition:
      border-color 120ms ease-out,
      background 120ms ease-out;
  }

  .name-input:focus {
    outline: none;
    border-color: var(--accent);
    background: var(--bg-surface-2);
    box-shadow: 0 0 0 2px color-mix(in srgb, var(--accent) 25%, transparent);
  }

  /* §5.2 Select input */
  .kind-select {
    height: 32px;
    padding: 0 8px;
    background: var(--bg-surface-2);
    border: 1px solid var(--border-strong);
    color: var(--text-primary);
    font-size: 13px;
    border-radius: 6px;
    cursor: pointer;
  }

  .kind-select:focus {
    outline: none;
    border-color: var(--accent);
    box-shadow: 0 0 0 2px color-mix(in srgb, var(--accent) 25%, transparent);
  }

  /* §5.3 Numeric columns */
  .weight-input {
    background: transparent;
    border: 1px solid transparent;
    color: var(--text-primary);
    font-family: var(--font-mono);
    font-size: 13px;
    font-variant-numeric: tabular-nums;
    padding: 4px 8px;
    border-radius: 6px;
    width: 6rem;
    text-align: right;
    transition:
      border-color 120ms ease-out,
      background 120ms ease-out;
  }

  .weight-input:focus {
    outline: none;
    border-color: var(--accent);
    background: var(--bg-surface-2);
    box-shadow: 0 0 0 2px color-mix(in srgb, var(--accent) 25%, transparent);
  }

  /* Remove spinner arrows */
  .weight-input::-webkit-outer-spin-button,
  .weight-input::-webkit-inner-spin-button {
    -webkit-appearance: none;
    appearance: none;
    margin: 0;
  }
  .weight-input[type='number'] {
    -moz-appearance: textfield;
    appearance: textfield;
  }

  /* Destructive button */
  .btn-delete {
    height: 26px;
    padding: 0 10px;
    background: transparent;
    color: var(--danger);
    border: 1px solid var(--danger);
    border-radius: 6px;
    cursor: pointer;
    font-size: 12px;
    font-weight: 500;
    transition: background 120ms ease-out;
  }

  .btn-delete:hover {
    background: color-mix(in srgb, var(--danger) 15%, transparent);
  }
</style>
