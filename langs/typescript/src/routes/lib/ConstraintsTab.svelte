<script lang="ts">
  import type { ScenarioVM } from '../../viewmodels/scenario-vm.js';
  import { vmxToStore } from '../../view/adapters/vmx-to-svelte.js';
  import { ScenarioMutationError } from '../../viewmodels/scenario-vm.js';
  import type { ConstraintM } from '../../models/constraint.js';
  import SectionHeader from './SectionHeader.svelte';

  export let vm: ScenarioVM;
  export let onError: (msg: string) => void = () => {};

  let subTab: 'Threshold' | 'Dependency' | 'Conflict' = 'Threshold';

  $: scenarioStore = vmxToStore(vm, 'scenario');
  $: scenario = $scenarioStore;
  $: properties = scenario?.properties ?? [];
  $: alternatives = scenario?.alternatives ?? [];
  $: constraints = scenario?.constraints ?? [];
  $: decisions = scenario?.decisions ?? [];

  $: thresholds = constraints
    .map((c, i) => ({ c, i }))
    .filter(({ c }) => c.kind === 'threshold') as {
    c: Extract<ConstraintM, { kind: 'threshold' }>;
    i: number;
  }[];

  $: dependencies = constraints
    .map((c, i) => ({ c, i }))
    .filter(({ c }) => c.kind === 'dependency') as {
    c: Extract<ConstraintM, { kind: 'dependency' }>;
    i: number;
  }[];

  $: conflicts = constraints.map((c, i) => ({ c, i })).filter(({ c }) => c.kind === 'conflict') as {
    c: Extract<ConstraintM, { kind: 'conflict' }>;
    i: number;
  }[];

  function altLabel(altId: string): string {
    const alt = alternatives.find((a) => a.id === altId);
    if (!alt) return altId;
    const dec = decisions.find((d) => d.id === alt.decisionId);
    return dec ? `${dec.name} / ${alt.name}` : alt.name;
  }

  function deleteConstraint(index: number) {
    if (!confirm('Delete this constraint?')) return;
    try {
      vm.deleteConstraint(index);
    } catch (err) {
      if (err instanceof ScenarioMutationError) onError(err.message);
      else throw err;
    }
  }

  // Threshold add/update
  function addThreshold() {
    if (properties.length === 0) {
      onError('Add properties first.');
      return;
    }
    const c: ConstraintM = { kind: 'threshold', propertyId: properties[0].id, min: 0 };
    try {
      vm.addConstraint(c);
    } catch (err) {
      if (err instanceof ScenarioMutationError) onError(err.message);
      else throw err;
    }
  }

  function updateThresholdProperty(
    index: number,
    propId: string,
    existing: Extract<ConstraintM, { kind: 'threshold' }>,
  ) {
    try {
      vm.updateConstraint(index, { ...existing, propertyId: propId });
    } catch (err) {
      if (err instanceof ScenarioMutationError) onError(err.message);
      else throw err;
    }
  }

  function updateThresholdMin(
    index: number,
    existing: Extract<ConstraintM, { kind: 'threshold' }>,
    e: FocusEvent,
  ) {
    const raw = (e.target as HTMLInputElement).value.trim();
    const val = raw === '' ? undefined : parseFloat(raw);
    if (val !== undefined && isNaN(val)) return;
    try {
      vm.updateConstraint(index, { ...existing, min: val });
    } catch (err) {
      if (err instanceof ScenarioMutationError) onError(err.message);
      else throw err;
    }
  }

  function updateThresholdMax(
    index: number,
    existing: Extract<ConstraintM, { kind: 'threshold' }>,
    e: FocusEvent,
  ) {
    const raw = (e.target as HTMLInputElement).value.trim();
    const val = raw === '' ? undefined : parseFloat(raw);
    if (val !== undefined && isNaN(val)) return;
    try {
      vm.updateConstraint(index, { ...existing, max: val });
    } catch (err) {
      if (err instanceof ScenarioMutationError) onError(err.message);
      else throw err;
    }
  }

  function isThresholdValid(c: Extract<ConstraintM, { kind: 'threshold' }>): boolean {
    if (c.min === undefined && c.max === undefined) return false;
    if (c.min !== undefined && c.max !== undefined && c.min > c.max) return false;
    return true;
  }

  // Dependency add/update
  function addDependency() {
    if (alternatives.length < 2) {
      onError('Need at least 2 alternatives for a dependency constraint.');
      return;
    }
    const c: ConstraintM = {
      kind: 'dependency',
      sourceAlternativeId: alternatives[0].id,
      targetAlternativeId: alternatives[1].id,
    };
    try {
      vm.addConstraint(c);
    } catch (err) {
      if (err instanceof ScenarioMutationError) onError(err.message);
      else throw err;
    }
  }

  function updateDepSource(
    index: number,
    existing: Extract<ConstraintM, { kind: 'dependency' }>,
    altId: string,
  ) {
    try {
      vm.updateConstraint(index, { ...existing, sourceAlternativeId: altId });
    } catch (err) {
      if (err instanceof ScenarioMutationError) onError(err.message);
      else throw err;
    }
  }

  function updateDepTarget(
    index: number,
    existing: Extract<ConstraintM, { kind: 'dependency' }>,
    altId: string,
  ) {
    try {
      vm.updateConstraint(index, { ...existing, targetAlternativeId: altId });
    } catch (err) {
      if (err instanceof ScenarioMutationError) onError(err.message);
      else throw err;
    }
  }

  // Conflict add/update
  function addConflict() {
    if (alternatives.length < 2) {
      onError('Need at least 2 alternatives for a conflict constraint.');
      return;
    }
    const c: ConstraintM = {
      kind: 'conflict',
      alternativeAId: alternatives[0].id,
      alternativeBId: alternatives[1].id,
    };
    try {
      vm.addConstraint(c);
    } catch (err) {
      if (err instanceof ScenarioMutationError) onError(err.message);
      else throw err;
    }
  }

  function updateConflictA(
    index: number,
    existing: Extract<ConstraintM, { kind: 'conflict' }>,
    altId: string,
  ) {
    try {
      vm.updateConstraint(index, { ...existing, alternativeAId: altId });
    } catch (err) {
      if (err instanceof ScenarioMutationError) onError(err.message);
      else throw err;
    }
  }

  function updateConflictB(
    index: number,
    existing: Extract<ConstraintM, { kind: 'conflict' }>,
    altId: string,
  ) {
    try {
      vm.updateConstraint(index, { ...existing, alternativeBId: altId });
    } catch (err) {
      if (err instanceof ScenarioMutationError) onError(err.message);
      else throw err;
    }
  }
</script>

<section class="tab-content">
  {#if scenario === undefined}
    <div class="empty">
      <div class="empty-headline">No scenario loaded.</div>
      <div class="empty-body">
        Click <strong>Open Sample SAS</strong> in the toolbar to see and edit constraints.
      </div>
    </div>
  {:else}
    <SectionHeader
      title="Constraints"
      subtitle="Rules that eliminate candidates: thresholds bound a single property; dependencies require pairings; conflicts forbid them."
    />
    <nav class="sub-tabs">
      {#each ['Threshold', 'Dependency', 'Conflict'] as t (t)}
        <button
          class="sub-tab"
          class:active={subTab === t}
          on:click={() => (subTab = t as typeof subTab)}>{t}</button
        >
      {/each}
    </nav>

    {#if subTab === 'Threshold'}
      <div class="tab-toolbar">
        <button class="btn-add" on:click={addThreshold}>+ Add Threshold</button>
      </div>
      {#if thresholds.length === 0}
        <div class="empty">No threshold constraints.</div>
      {:else}
        <div class="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Property</th>
                <th>Min</th>
                <th>Max</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {#each thresholds as { c, i } (i)}
                <tr class:invalid={!isThresholdValid(c)}>
                  <td>
                    <select
                      class="kind-select"
                      value={c.propertyId}
                      on:change={(e) =>
                        updateThresholdProperty(i, (e.target as HTMLSelectElement).value, c)}
                    >
                      {#each properties as p (p.id)}
                        <option value={p.id}>{p.name}</option>
                      {/each}
                    </select>
                  </td>
                  <td>
                    <input
                      type="number"
                      class="num-input"
                      value={c.min ?? ''}
                      placeholder="—"
                      on:blur={(e) => updateThresholdMin(i, c, e)}
                    />
                  </td>
                  <td>
                    <input
                      type="number"
                      class="num-input"
                      value={c.max ?? ''}
                      placeholder="—"
                      on:blur={(e) => updateThresholdMax(i, c, e)}
                    />
                  </td>
                  <td>
                    <button class="btn-delete" on:click={() => deleteConstraint(i)}>Delete</button>
                  </td>
                </tr>
              {/each}
            </tbody>
          </table>
        </div>
      {/if}
    {:else if subTab === 'Dependency'}
      <div class="tab-toolbar">
        <button class="btn-add" on:click={addDependency}>+ Add Dependency</button>
      </div>
      {#if dependencies.length === 0}
        <div class="empty">No dependency constraints.</div>
      {:else}
        <div class="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Source Alternative</th>
                <th>Target Alternative</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {#each dependencies as { c, i } (i)}
                <tr class:self-edge={c.sourceAlternativeId === c.targetAlternativeId}>
                  <td>
                    <select
                      class="kind-select wide"
                      value={c.sourceAlternativeId}
                      on:change={(e) =>
                        updateDepSource(i, c, (e.target as HTMLSelectElement).value)}
                    >
                      {#each alternatives as a (a.id)}
                        <option value={a.id}>{altLabel(a.id)}</option>
                      {/each}
                    </select>
                  </td>
                  <td>
                    <select
                      class="kind-select wide"
                      value={c.targetAlternativeId}
                      on:change={(e) =>
                        updateDepTarget(i, c, (e.target as HTMLSelectElement).value)}
                    >
                      {#each alternatives as a (a.id)}
                        <option value={a.id}>{altLabel(a.id)}</option>
                      {/each}
                    </select>
                  </td>
                  <td>
                    <button class="btn-delete" on:click={() => deleteConstraint(i)}>Delete</button>
                  </td>
                </tr>
              {/each}
            </tbody>
          </table>
        </div>
      {/if}
    {:else}
      <!-- Conflict sub-tab -->
      <div class="tab-toolbar">
        <button class="btn-add" on:click={addConflict}>+ Add Conflict</button>
      </div>
      {#if conflicts.length === 0}
        <div class="empty">No conflict constraints.</div>
      {:else}
        <div class="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Alternative A</th>
                <th>Alternative B</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {#each conflicts as { c, i } (i)}
                <tr class:self-edge={c.alternativeAId === c.alternativeBId}>
                  <td>
                    <select
                      class="kind-select wide"
                      value={c.alternativeAId}
                      on:change={(e) =>
                        updateConflictA(i, c, (e.target as HTMLSelectElement).value)}
                    >
                      {#each alternatives as a (a.id)}
                        <option value={a.id}>{altLabel(a.id)}</option>
                      {/each}
                    </select>
                  </td>
                  <td>
                    <select
                      class="kind-select wide"
                      value={c.alternativeBId}
                      on:change={(e) =>
                        updateConflictB(i, c, (e.target as HTMLSelectElement).value)}
                    >
                      {#each alternatives as a (a.id)}
                        <option value={a.id}>{altLabel(a.id)}</option>
                      {/each}
                    </select>
                  </td>
                  <td>
                    <button class="btn-delete" on:click={() => deleteConstraint(i)}>Delete</button>
                  </td>
                </tr>
              {/each}
            </tbody>
          </table>
        </div>
      {/if}
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

  /* §5.4 Sub-tabs (same design as main tab strip) */
  .sub-tabs {
    display: flex;
    gap: 0;
    padding: 0 24px;
    height: 40px;
    background: var(--bg-surface-2);
    border-bottom: 1px solid var(--border-subtle);
    flex-shrink: 0;
    align-items: stretch;
  }

  .sub-tab {
    display: flex;
    align-items: center;
    padding: 0 12px;
    background: transparent;
    color: var(--text-secondary);
    border: none;
    border-bottom: 2px solid transparent;
    cursor: pointer;
    font-size: 13px;
    font-weight: 500;
    transition:
      color 120ms ease-out,
      background 120ms ease-out;
    white-space: nowrap;
  }

  .sub-tab:hover {
    color: var(--text-primary);
    background: var(--bg-surface-3);
  }

  .sub-tab.active {
    color: var(--text-primary);
    border-bottom-color: var(--accent);
    font-weight: 600;
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

  /* Secondary button */
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

  tr.invalid td {
    background: color-mix(in srgb, var(--danger) 10%, var(--bg-page));
  }

  tr.self-edge td {
    background: color-mix(in srgb, var(--warning) 10%, var(--bg-page));
  }

  /* §5.2 Select */
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

  .kind-select.wide {
    min-width: 16rem;
  }

  /* §5.3 Numeric input */
  .num-input {
    background: transparent;
    border: 1px solid transparent;
    color: var(--text-primary);
    font-family: var(--font-mono);
    font-size: 13px;
    font-variant-numeric: tabular-nums;
    padding: 4px 8px;
    border-radius: 6px;
    width: 7rem;
    text-align: right;
    transition:
      border-color 120ms ease-out,
      background 120ms ease-out;
  }

  .num-input:focus {
    outline: none;
    border-color: var(--accent);
    background: var(--bg-surface-2);
    box-shadow: 0 0 0 2px color-mix(in srgb, var(--accent) 25%, transparent);
  }

  /* Remove spinner arrows */
  .num-input::-webkit-outer-spin-button,
  .num-input::-webkit-inner-spin-button {
    -webkit-appearance: none;
    margin: 0;
  }
  .num-input[type='number'] {
    -moz-appearance: textfield;
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
