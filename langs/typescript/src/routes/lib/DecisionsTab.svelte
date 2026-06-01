<script lang="ts">
  import type { ScenarioVM } from '../../viewmodels/scenario-vm.js';
  import { vmxToStore } from '../../view/adapters/vmx-to-svelte.js';
  import { ScenarioMutationError } from '../../viewmodels/scenario-vm.js';
  import EmptyState from './EmptyState.svelte';
  import { SAMPLES } from '../../samples/index.js';

  export let vm: ScenarioVM;
  export let onError: (msg: string) => void = () => {};

  $: scenarioStore = vmxToStore(vm, 'scenario');
  $: decisions = $scenarioStore?.decisions ?? [];

  // Hero-state CTA: load a sample. No dirty-discard prompt because the
  // hero only renders when no scenario is loaded — there's nothing to lose.
  function openSample(index: number) {
    const sample = SAMPLES[index];
    vm._browserOpen(sample.raw, sample.id + '.json');
  }

  function handleAdd() {
    try {
      vm.addDecision();
    } catch (err) {
      if (err instanceof ScenarioMutationError) onError(err.message);
      else throw err;
    }
  }

  function handleDelete(id: string, name: string) {
    if (!confirm(`Delete decision "${name}" and all its alternatives? This cannot be undone.`))
      return;
    try {
      vm.deleteDecision(id);
    } catch (err) {
      if (err instanceof ScenarioMutationError) onError(err.message);
      else throw err;
    }
  }

  function handleNameBlur(id: string, e: FocusEvent) {
    const val = (e.target as HTMLInputElement).value.trim();
    if (!val) return;
    try {
      vm.updateDecisionName(id, val);
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
    <EmptyState
      variant="hero"
      kicker="Welcome to GuideArch"
      headline="Pick a software architecture, with fuzzy TOPSIS."
      body="Model decisions, alternatives, weighted quality properties, and constraints — then see ranked candidates and which decisions move the result most. Start with a bundled sample to see it in action."
      primary={{ label: 'Open Sample SAS', onClick: () => openSample(0) }}
      secondary={{ label: 'Open Sample EDS', onClick: () => openSample(1) }}
    />
  {:else}
    <div class="tab-toolbar">
      <button class="btn-add" on:click={handleAdd}>+ Add Decision</button>
    </div>
    {#if decisions.length === 0}
      <EmptyState
        headline="No decisions yet"
        body="A scenario is a set of architectural decisions you need to make. Add your first one to begin laying out the decision space."
        primary={{ label: '+ Add Decision', onClick: handleAdd }}
      />
    {:else}
      <div class="table-wrap">
        <table>
          <thead>
            <tr>
              <th>ID</th>
              <th>Name</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {#each decisions as d (d.id)}
              <tr>
                <td class="mono">{d.id}</td>
                <td>
                  <input
                    class="name-input"
                    value={d.name}
                    on:blur={(e) => handleNameBlur(d.id, e)}
                    on:keydown={handleNameKeydown}
                  />
                </td>
                <td>
                  <button class="btn-delete" on:click={() => handleDelete(d.id, d.name)}>
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

  .tab-toolbar {
    display: flex;
    align-items: center;
    height: 40px;
    padding: 0 24px;
    border-bottom: 1px solid var(--border-subtle);
    flex-shrink: 0;
    gap: 8px;
  }

  /* Secondary button — §5.1 */
  .btn-add {
    height: 28px;
    padding: 0 16px;
    background: transparent;
    color: var(--text-primary);
    border: 1px solid var(--border-strong);
    border-radius: 6px;
    cursor: pointer;
    font-size: 13px;
    font-weight: 500;
    transition: background 120ms ease-out;
  }

  .btn-add:hover {
    background: var(--bg-surface-2);
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

  /* §5.3 Monospace IDs */
  .mono {
    font-family: var(--font-mono);
    font-size: 12px;
    color: var(--text-secondary);
    font-variant-numeric: tabular-nums;
  }

  /* Inline name input — transparent until focused */
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

  /* Destructive button — §5.1 */
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
