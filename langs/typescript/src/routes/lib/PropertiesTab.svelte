<script lang="ts">
  import type { ScenarioVM } from '../../viewmodels/scenario-vm.js';
  import { vmxToStore } from '../../view/adapters/vmx-to-svelte.js';
  import { ScenarioMutationError } from '../../viewmodels/scenario-vm.js';

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
  <div class="tab-toolbar">
    <button class="btn-add" on:click={handleAdd}>+ Add Property</button>
  </div>
  {#if properties.length === 0}
    <div class="empty">No properties yet. Click "Add Property" to create one.</div>
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
</section>

<style>
  .tab-content {
    display: flex;
    flex-direction: column;
    height: 100%;
    overflow: hidden;
  }

  .tab-toolbar {
    padding: 0.6rem 1.25rem;
    border-bottom: 1px solid #2e2e38;
    flex-shrink: 0;
  }

  .btn-add {
    padding: 0.3rem 0.85rem;
    background: #2d4a2d;
    color: #86efac;
    border: 1px solid #3a6a3a;
    border-radius: 4px;
    cursor: pointer;
    font-size: 0.82rem;
    transition: background 0.12s;
  }

  .btn-add:hover {
    background: #3a5e3a;
  }

  .empty {
    flex: 1;
    display: flex;
    align-items: center;
    justify-content: center;
    color: #555566;
    font-size: 0.9rem;
  }

  .table-wrap {
    flex: 1;
    overflow: auto;
    padding: 0.75rem 1.25rem;
  }

  table {
    width: 100%;
    border-collapse: collapse;
    font-size: 0.85rem;
  }

  th {
    padding: 0.45rem 0.65rem;
    text-align: left;
    color: #888899;
    font-weight: 600;
    border-bottom: 1px solid #2e2e38;
    background: #1a1a20;
  }

  td {
    padding: 0.35rem 0.65rem;
    border-bottom: 1px solid #22222c;
  }

  tbody tr:hover {
    background: #1e1e28;
  }

  .mono {
    font-family: monospace;
    font-size: 0.78rem;
    color: #888899;
  }

  .name-input {
    background: transparent;
    border: 1px solid transparent;
    color: #e8e8ec;
    font-size: 0.85rem;
    padding: 0.2rem 0.4rem;
    border-radius: 3px;
    width: 100%;
    min-width: 10rem;
    transition: border-color 0.12s;
  }

  .name-input:focus {
    outline: none;
    border-color: #4f46e5;
    background: #1a1a28;
  }

  .kind-select {
    background: #1a1a28;
    border: 1px solid #3e3e50;
    color: #e8e8ec;
    font-size: 0.82rem;
    padding: 0.2rem 0.4rem;
    border-radius: 3px;
    cursor: pointer;
  }

  .weight-input {
    background: transparent;
    border: 1px solid transparent;
    color: #6ee7b7;
    font-family: monospace;
    font-size: 0.82rem;
    padding: 0.2rem 0.4rem;
    border-radius: 3px;
    width: 6rem;
    text-align: right;
    transition: border-color 0.12s;
  }

  .weight-input:focus {
    outline: none;
    border-color: #4f46e5;
    background: #1a1a28;
  }

  /* Remove spinner arrows */
  .weight-input::-webkit-outer-spin-button,
  .weight-input::-webkit-inner-spin-button {
    -webkit-appearance: none;
    margin: 0;
  }
  .weight-input[type='number'] {
    -moz-appearance: textfield;
  }

  .btn-delete {
    padding: 0.2rem 0.6rem;
    background: #3d1a1a;
    color: #f87171;
    border: 1px solid #6b2020;
    border-radius: 3px;
    cursor: pointer;
    font-size: 0.78rem;
    transition: background 0.12s;
  }

  .btn-delete:hover {
    background: #5a2020;
  }
</style>
