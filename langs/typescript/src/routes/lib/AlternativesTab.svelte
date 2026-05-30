<script lang="ts">
  import type { ScenarioVM } from '../../viewmodels/scenario-vm.js';
  import { vmxToStore } from '../../view/adapters/vmx-to-svelte.js';
  import { ScenarioMutationError } from '../../viewmodels/scenario-vm.js';

  export let vm: ScenarioVM;
  export let onError: (msg: string) => void = () => {};

  $: scenarioStore = vmxToStore(vm, 'scenario');
  $: decisions = $scenarioStore?.decisions ?? [];
  $: alternatives = $scenarioStore?.alternatives ?? [];

  function altsForDecision(decId: string) {
    return alternatives.filter((a) => a.decisionId === decId);
  }

  function handleAdd(decisionId: string) {
    try {
      vm.addAlternative(decisionId);
    } catch (err) {
      if (err instanceof ScenarioMutationError) onError(err.message);
      else throw err;
    }
  }

  function handleDelete(id: string, name: string) {
    if (!confirm(`Delete alternative "${name}"? This removes its coefficients and constraints.`))
      return;
    try {
      vm.deleteAlternative(id);
    } catch (err) {
      if (err instanceof ScenarioMutationError) onError(err.message);
      else throw err;
    }
  }

  function handleNameBlur(id: string, e: FocusEvent) {
    const val = (e.target as HTMLInputElement).value.trim();
    if (!val) return;
    try {
      vm.updateAlternativeName(id, val);
    } catch (err) {
      if (err instanceof ScenarioMutationError) onError(err.message);
      else throw err;
    }
  }

  function handleNameKeydown(e: KeyboardEvent) {
    if (e.key === 'Enter') (e.target as HTMLInputElement).blur();
  }
</script>

<section class="tab-content">
  {#if decisions.length === 0}
    <div class="empty">No decisions yet. Add decisions first.</div>
  {:else}
    <div class="table-wrap">
      {#each decisions as dec (dec.id)}
        <div class="decision-group">
          <div class="group-header">
            <span class="group-name">{dec.name}</span>
            <span class="group-id mono">{dec.id}</span>
            <button class="btn-add" on:click={() => handleAdd(dec.id)}>+ Add Alternative</button>
          </div>
          {#if altsForDecision(dec.id).length === 0}
            <div class="empty-group">No alternatives for this decision.</div>
          {:else}
            <table>
              <thead>
                <tr>
                  <th>ID</th>
                  <th>Name</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                {#each altsForDecision(dec.id) as alt (alt.id)}
                  <tr>
                    <td class="mono">{alt.id}</td>
                    <td>
                      <input
                        class="name-input"
                        value={alt.name}
                        on:blur={(e) => handleNameBlur(alt.id, e)}
                        on:keydown={handleNameKeydown}
                      />
                    </td>
                    <td>
                      <button class="btn-delete" on:click={() => handleDelete(alt.id, alt.name)}>
                        Delete
                      </button>
                    </td>
                  </tr>
                {/each}
              </tbody>
            </table>
          {/if}
        </div>
      {/each}
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
  }

  .table-wrap {
    flex: 1;
    overflow: auto;
    padding: 0.75rem 1.25rem;
    display: flex;
    flex-direction: column;
    gap: 1.25rem;
  }

  .decision-group {
    border: 1px solid #2e2e38;
    border-radius: 6px;
    overflow: hidden;
  }

  .group-header {
    display: flex;
    align-items: center;
    gap: 0.75rem;
    padding: 0.55rem 0.85rem;
    background: #1a1a28;
    border-bottom: 1px solid #2e2e38;
  }

  .group-name {
    font-weight: 600;
    color: #c4b5fd;
    font-size: 0.88rem;
  }

  .group-id {
    color: #555566;
    font-size: 0.75rem;
  }

  .btn-add {
    margin-left: auto;
    padding: 0.25rem 0.7rem;
    background: #2d4a2d;
    color: #86efac;
    border: 1px solid #3a6a3a;
    border-radius: 4px;
    cursor: pointer;
    font-size: 0.78rem;
    transition: background 0.12s;
  }

  .btn-add:hover {
    background: #3a5e3a;
  }

  .empty-group {
    padding: 0.6rem 0.85rem;
    color: #555566;
    font-size: 0.82rem;
  }

  table {
    width: 100%;
    border-collapse: collapse;
    font-size: 0.85rem;
  }

  th {
    padding: 0.4rem 0.65rem;
    text-align: left;
    color: #888899;
    font-weight: 600;
    border-bottom: 1px solid #2e2e38;
    background: #15151e;
  }

  td {
    padding: 0.32rem 0.65rem;
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
    min-width: 12rem;
    transition: border-color 0.12s;
  }

  .name-input:focus {
    outline: none;
    border-color: #4f46e5;
    background: #1a1a28;
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
