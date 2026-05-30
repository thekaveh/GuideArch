<script lang="ts">
  import { makeScenarioVm } from '$lib/../viewmodels/scenario-vm.js';
  import Toolbar from './lib/Toolbar.svelte';
  import TabStrip from './lib/TabStrip.svelte';
  import StatusBar from './lib/StatusBar.svelte';
  import DecisionsTab from './lib/DecisionsTab.svelte';
  import AlternativesTab from './lib/AlternativesTab.svelte';
  import PropertiesTab from './lib/PropertiesTab.svelte';
  import CoefficientsTab from './lib/CoefficientsTab.svelte';
  import ConstraintsTab from './lib/ConstraintsTab.svelte';
  import ResultsTab from './lib/ResultsTab.svelte';
  import CriticalDecisionsTab from './lib/CriticalDecisionsTab.svelte';
  import CriticalConstraintsTab from './lib/CriticalConstraintsTab.svelte';

  const vm = makeScenarioVm();

  const TABS = [
    'Decisions',
    'Alternatives',
    'Properties',
    'Coefficients',
    'Constraints',
    'Results',
    'Critical Decisions',
    'Critical Constraints',
  ] as const;
  type Tab = (typeof TABS)[number];

  let activeTab: Tab = 'Results';

  // Error/alert modal
  let errorMessage: string | null = null;

  function showError(msg: string) {
    errorMessage = msg;
  }

  function dismissError() {
    errorMessage = null;
  }
</script>

<div class="app-shell">
  <Toolbar {vm} onError={showError} />
  <TabStrip tabs={[...TABS]} active={activeTab} onSelect={(t) => (activeTab = t as Tab)} />

  <div class="tab-body">
    {#if activeTab === 'Decisions'}
      <DecisionsTab {vm} onError={showError} />
    {:else if activeTab === 'Alternatives'}
      <AlternativesTab {vm} onError={showError} />
    {:else if activeTab === 'Properties'}
      <PropertiesTab {vm} onError={showError} />
    {:else if activeTab === 'Coefficients'}
      <CoefficientsTab {vm} onError={showError} />
    {:else if activeTab === 'Constraints'}
      <ConstraintsTab {vm} onError={showError} />
    {:else if activeTab === 'Critical Decisions'}
      <CriticalDecisionsTab {vm} />
    {:else if activeTab === 'Critical Constraints'}
      <CriticalConstraintsTab {vm} />
    {:else}
      <ResultsTab {vm} />
    {/if}
  </div>

  <StatusBar {vm} />
</div>

<!-- Error modal -->
{#if errorMessage !== null}
  <div class="modal-overlay" on:click={dismissError} role="presentation">
    <div
      class="modal"
      role="alertdialog"
      aria-modal="true"
      aria-label="Error"
      tabindex="-1"
      on:click|stopPropagation
      on:keydown={(e) => e.key === 'Escape' && dismissError()}
    >
      <h2 class="modal-title">Error</h2>
      <p class="modal-body">{errorMessage}</p>
      <div class="modal-actions">
        <button class="btn-ok" on:click={dismissError}>OK</button>
      </div>
    </div>
  </div>
{/if}

<style>
  :global(body) {
    margin: 0;
    font-family: system-ui, sans-serif;
    background: #0f0f11;
    color: #e8e8ec;
    overflow: hidden;
  }

  .app-shell {
    display: flex;
    flex-direction: column;
    height: 100vh;
    overflow: hidden;
  }

  .tab-body {
    flex: 1;
    overflow: hidden;
    display: flex;
    flex-direction: column;
    min-height: 0;
  }

  /* ── Error modal ─────────────────────────────────────────────────── */
  .modal-overlay {
    position: fixed;
    inset: 0;
    background: rgba(0, 0, 0, 0.65);
    display: flex;
    align-items: center;
    justify-content: center;
    z-index: 100;
  }

  .modal {
    background: #1e1e2e;
    border: 1px solid #3e3e50;
    border-radius: 8px;
    padding: 1.5rem 2rem;
    max-width: 28rem;
    width: 90vw;
    box-shadow: 0 8px 32px rgba(0, 0, 0, 0.6);
  }

  .modal-title {
    margin: 0 0 0.75rem;
    font-size: 1rem;
    color: #f87171;
    font-weight: 700;
  }

  .modal-body {
    margin: 0 0 1.25rem;
    font-size: 0.88rem;
    color: #e8e8ec;
    line-height: 1.5;
    word-break: break-word;
  }

  .modal-actions {
    display: flex;
    justify-content: flex-end;
  }

  .btn-ok {
    padding: 0.35rem 1.2rem;
    background: #4f46e5;
    color: #fff;
    border: none;
    border-radius: 4px;
    cursor: pointer;
    font-size: 0.85rem;
    font-weight: 600;
    transition: background 0.12s;
  }

  .btn-ok:hover {
    background: #6366f1;
  }
</style>
