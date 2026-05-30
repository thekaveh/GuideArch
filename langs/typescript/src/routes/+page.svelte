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
  .app-shell {
    display: flex;
    flex-direction: column;
    height: 100vh;
    overflow: hidden;
    background: var(--bg-page);
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
    background: var(--bg-surface-3);
    border: 1px solid var(--border-strong);
    border-radius: 8px;
    padding: 24px 32px;
    max-width: 28rem;
    width: 90vw;
    box-shadow: 0 8px 32px rgba(0, 0, 0, 0.6);
  }

  .modal-title {
    margin: 0 0 12px;
    font-size: 16px;
    color: var(--danger);
    font-weight: 600;
  }

  .modal-body {
    margin: 0 0 20px;
    font-size: 14px;
    color: var(--text-primary);
    line-height: 1.5;
    word-break: break-word;
  }

  .modal-actions {
    display: flex;
    justify-content: flex-end;
  }

  /* Primary button — §5.1 */
  .btn-ok {
    padding: 8px 16px;
    background: var(--accent);
    color: var(--accent-on);
    border: none;
    border-radius: 6px;
    cursor: pointer;
    font-size: 14px;
    font-weight: 600;
    transition: background 120ms ease-out;
  }

  .btn-ok:hover {
    background: var(--accent-hover);
  }
</style>
