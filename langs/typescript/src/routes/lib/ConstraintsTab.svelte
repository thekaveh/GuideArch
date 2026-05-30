<script lang="ts">
  import type { ScenarioVM } from '../../viewmodels/scenario-vm.js';
  import { vmxToStore } from '../../view/adapters/vmx-to-svelte.js';
  import { ScenarioMutationError } from '../../viewmodels/scenario-vm.js';
  import type { ConstraintM } from '../../models/constraint.js';

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
                    on:change={(e) => updateDepSource(i, c, (e.target as HTMLSelectElement).value)}
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
                    on:change={(e) => updateDepTarget(i, c, (e.target as HTMLSelectElement).value)}
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
                    on:change={(e) => updateConflictA(i, c, (e.target as HTMLSelectElement).value)}
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
                    on:change={(e) => updateConflictB(i, c, (e.target as HTMLSelectElement).value)}
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
</section>

<style>
  .tab-content {
    display: flex;
    flex-direction: column;
    height: 100%;
    overflow: hidden;
  }

  .sub-tabs {
    display: flex;
    gap: 0;
    padding: 0 1.25rem;
    background: #13131a;
    border-bottom: 1px solid #2e2e38;
    flex-shrink: 0;
  }

  .sub-tab {
    padding: 0.4rem 0.9rem;
    background: transparent;
    color: #888899;
    border: none;
    border-bottom: 2px solid transparent;
    cursor: pointer;
    font-size: 0.8rem;
    transition: color 0.12s;
  }

  .sub-tab:hover {
    color: #c4b5fd;
  }

  .sub-tab.active {
    color: #e8e8ec;
    border-bottom-color: #7c3aed;
    font-weight: 600;
  }

  .tab-toolbar {
    padding: 0.5rem 1.25rem;
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

  tr.invalid td {
    background: #2d1010;
  }

  tr.self-edge td {
    background: #2d2010;
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

  .kind-select.wide {
    min-width: 16rem;
  }

  .num-input {
    background: transparent;
    border: 1px solid transparent;
    color: #6ee7b7;
    font-family: monospace;
    font-size: 0.82rem;
    padding: 0.2rem 0.4rem;
    border-radius: 3px;
    width: 7rem;
    text-align: right;
    transition: border-color 0.12s;
  }

  .num-input:focus {
    outline: none;
    border-color: #4f46e5;
    background: #1a1a28;
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
