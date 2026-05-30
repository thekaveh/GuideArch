<script lang="ts">
  import { makeScenarioVm } from '$lib/../viewmodels/scenario-vm.js';
  import { vmxStoreAll } from '$lib/../view/adapters/vmx-to-svelte.js';

  const vm = makeScenarioVm();
  const stateStore = vmxStoreAll(vm);

  let fileInput: { files: FileList | null; value: string } | undefined;

  function handleFileChange(event: { currentTarget: { files: FileList | null; value: string } }) {
    const file = event.currentTarget.files?.[0];
    if (!file) return;
    // In Tauri the File object has a non-standard .path property with the real
    // filesystem path. In a pure browser SPA it may be empty — we fall back to
    // the file name (cosmetic only; loadScenario will fail without a real path).
    const fsPath: string = (file as typeof file & { path?: string }).path ?? file.name;
    vm.openCmd.execute(fsPath);
    // Reset so the same file can be re-opened
    event.currentTarget.value = '';
  }

  $: scenario = $stateStore.scenario;
  $: candidates = $stateStore.candidates;
  $: status = $stateStore.status;
  $: hasScenario = scenario !== undefined;
  $: top50 = candidates.slice(0, 50);
</script>

<main>
  <!-- ── Top bar ─────────────────────────────────────────────────────── -->
  <header>
    <span class="app-name">GuideArch</span>
    <label class="open-btn">
      Open scenario…
      <input
        type="file"
        accept=".json"
        bind:this={fileInput}
        on:change={handleFileChange}
        style="display:none"
      />
    </label>
  </header>

  <!-- ── Status row ─────────────────────────────────────────────────── -->
  <div class="status-row">
    {#if hasScenario}
      <span class="scenario-name">{scenario?.name ?? ''}</span>
      <span class="sep">·</span>
    {/if}
    <span class="status-text">{status}</span>
  </div>

  <!-- ── Main content ───────────────────────────────────────────────── -->
  {#if !hasScenario}
    <div class="empty-state">Open a scenario JSON to begin.</div>
  {:else if top50.length === 0}
    <div class="empty-state">No feasible candidates found.</div>
  {:else}
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
            <tr>
              <td class="rank">{c.rank + 1}</td>
              <td class="score">{c.score.toFixed(6)}</td>
              <td class="alts">{c.alternativeIds.join(', ')}</td>
            </tr>
          {/each}
        </tbody>
      </table>
    </div>
  {/if}
</main>

<style>
  :global(body) {
    margin: 0;
    font-family: system-ui, sans-serif;
    background: #0f0f11;
    color: #e8e8ec;
  }

  main {
    display: flex;
    flex-direction: column;
    height: 100vh;
  }

  /* ── Top bar ─────────────────────────────────────────────────────── */
  header {
    display: flex;
    align-items: center;
    gap: 1rem;
    padding: 0.75rem 1.25rem;
    background: #1a1a20;
    border-bottom: 1px solid #2e2e38;
    flex-shrink: 0;
  }

  .app-name {
    font-weight: 700;
    font-size: 1.1rem;
    color: #a78bfa;
    letter-spacing: 0.03em;
  }

  .open-btn {
    padding: 0.35rem 0.9rem;
    background: #4f46e5;
    color: #fff;
    border-radius: 4px;
    cursor: pointer;
    font-size: 0.875rem;
    user-select: none;
    transition: background 0.15s;
  }

  .open-btn:hover {
    background: #6366f1;
  }

  /* ── Status row ───────────────────────────────────────────────────── */
  .status-row {
    display: flex;
    align-items: center;
    gap: 0.5rem;
    padding: 0.4rem 1.25rem;
    background: #13131a;
    border-bottom: 1px solid #2e2e38;
    font-size: 0.8rem;
    color: #a0a0b0;
    flex-shrink: 0;
  }

  .scenario-name {
    font-weight: 600;
    color: #c4b5fd;
  }

  .sep {
    opacity: 0.4;
  }

  /* ── Empty state ─────────────────────────────────────────────────── */
  .empty-state {
    flex: 1;
    display: flex;
    align-items: center;
    justify-content: center;
    color: #555566;
    font-size: 1rem;
  }

  /* ── Candidates table ─────────────────────────────────────────────── */
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

  tbody tr:nth-child(odd) {
    background: #111118;
  }

  tbody tr:nth-child(even) {
    background: #0f0f16;
  }

  tbody tr:hover {
    background: #1e1e28;
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
    max-width: 60vw;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }
</style>
