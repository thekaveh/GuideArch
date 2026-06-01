<script lang="ts">
  import { onMount } from 'svelte';
  import { makeAppVm } from '../viewmodels/app-vm.js';
  import { vmxToStore } from '../view/adapters/vmx-to-svelte.js';
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
  import EmptyState from './lib/EmptyState.svelte';
  import ConfirmDialog from './lib/ConfirmDialog.svelte';
  import { SAMPLES } from '../samples/index.js';

  // AppVM is the root VM. Tabs and the toolbar continue to bind to
  // ScenarioVM (reached via app.scenario) — only app-shell concerns live
  // on AppVM itself.
  const app = makeAppVm();
  const vm = app.scenario;

  // Theme observable → <html data-theme="…"> attribute. Subscribed once
  // on mount; the readable store re-emits whenever AppVM's model swaps.
  const themeStore = vmxToStore(app, 'theme');
  onMount(() => {
    const unsub = themeStore.subscribe((theme) => {
      document.documentElement.dataset.theme = theme;
    });
    return unsub;
  });

  // Dev escape hatch: expose AppVM on window in dev builds so the theme
  // observable is exercisable from devtools (e.g. `__app.setTheme('light')`)
  // until a proper picker lands with the upcoming UI redesign.
  if (typeof window !== 'undefined' && import.meta.env.DEV) {
    (window as unknown as Record<string, unknown>).__app = app;
  }

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

  // Default to Decisions on first launch — matches Python and C# initial-tab
  // UX so a user comparing screenshots across impls sees the same empty
  // state. Results lands as the active tab only after a scenario is loaded.
  let activeTab: Tab = 'Decisions';

  // Error/alert modal
  let errorMessage: string | null = null;

  function showError(msg: string) {
    errorMessage = msg;
  }

  function dismissError() {
    errorMessage = null;
  }

  // First-launch hero: reactive to the scenario observable so the hero
  // disappears the moment a scenario is loaded (regardless of how).
  const scenarioStore = vmxToStore(vm, 'scenario');
  function openSample(index: number) {
    const sample = SAMPLES[index];
    vm._browserOpen(sample.raw, sample.id + '.json');
  }
</script>

<div class="app-shell">
  <Toolbar {vm} {app} onError={showError} />
  <TabStrip tabs={[...TABS]} active={activeTab} onSelect={(t) => (activeTab = t as Tab)} />

  <div class="tab-body">
    {#if $scenarioStore === undefined}
      <!-- First-launch hero — dominates the tab body until a scenario is
           loaded, regardless of which tab the user has selected. The
           per-tab "no scenario" branches still exist as a fallback but
           normally never render. -->
      <EmptyState
        variant="hero"
        kicker="Welcome to GuideArch"
        headline="Pick a software architecture, with fuzzy TOPSIS."
        body="Model decisions, alternatives, weighted quality properties, and constraints. GuideArch ranks every feasible candidate, then shows which decisions move the result most and which constraints bind hardest. Start with a bundled sample to see it in action."
        primary={{ label: 'Open Sample SAS', onClick: () => openSample(0) }}
        secondary={{ label: 'Open Sample EDS', onClick: () => openSample(1) }}
      />
    {:else if activeTab === 'Decisions'}
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

<ConfirmDialog />

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
