<script lang="ts">
  import type { ScenarioVM } from '../../viewmodels/scenario-vm.js';
  import { vmxToStore } from '../../view/adapters/vmx-to-svelte.js';
  import { ScenarioMutationError } from '../../viewmodels/scenario-vm.js';
  import EmptyState from './EmptyState.svelte';
  import SectionHeader from './SectionHeader.svelte';

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
  {#if $scenarioStore === undefined}
    <div class="empty">
      <div class="empty-headline">No scenario loaded.</div>
      <div class="empty-body">
        Click <strong>Open Sample SAS</strong> in the toolbar to try the example, or click
        <strong>New</strong> to start a blank scenario.
      </div>
    </div>
  {:else if decisions.length === 0}
    <EmptyState
      headline="No decisions yet"
      body="Alternatives belong to decisions. Switch to the Decisions tab and add at least one before coming back here."
    />
  {:else}
    <SectionHeader
      title="Alternatives"
      subtitle="Concrete options under each decision; pick one per decision to form a candidate."
    />
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
    display: flex;
    flex-direction: column;
    gap: 16px;
  }

  /* §5.5 Card */
  .decision-group {
    background: var(--bg-surface);
    border: 1px solid var(--border-strong);
    border-radius: 8px;
    overflow: hidden;
  }

  .group-header {
    display: flex;
    align-items: center;
    gap: 8px;
    padding: 0 16px;
    height: 40px;
    background: var(--bg-surface-2);
    border-bottom: 1px solid var(--border-subtle);
  }

  .group-name {
    font-weight: 600;
    font-size: 13px;
    color: var(--accent-hover);
  }

  .group-id {
    color: var(--text-muted);
    font-size: 12px;
    font-family: var(--font-mono);
  }

  /* Secondary button */
  .btn-add {
    margin-left: auto;
    height: 26px;
    padding: 0 12px;
    background: transparent;
    color: var(--text-primary);
    border: 1px solid var(--border-strong);
    border-radius: 6px;
    cursor: pointer;
    font-size: 12px;
    font-weight: 500;
    transition: background 120ms ease-out;
  }

  .btn-add:hover {
    background: var(--bg-surface-3);
  }

  .empty-group {
    padding: 8px 16px;
    color: var(--text-muted);
    font-size: 13px;
  }

  /* §5.3 Tables */
  table {
    width: 100%;
    border-collapse: collapse;
    font-size: 13px;
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
    min-width: 12rem;
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
