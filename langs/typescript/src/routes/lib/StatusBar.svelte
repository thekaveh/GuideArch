<script lang="ts">
  import type { ScenarioVM } from '../../viewmodels/scenario-vm.js';
  import { vmxToStore } from '../../view/adapters/vmx-to-svelte.js';

  export let vm: ScenarioVM;

  $: scenarioStore = vmxToStore(vm, 'scenario');
  $: statusStore = vmxToStore(vm, 'status');
  $: warningsStore = vmxToStore(vm, 'warnings');
  $: isDirtyStore = vmxToStore(vm, 'isDirty');
  $: filePathStore = vmxToStore(vm, 'filePath');
  $: candidatesStore = vmxToStore(vm, 'candidates');

  function basename(path: string): string {
    // Display tail only — full path goes in the title attribute on hover.
    const parts = path.split(/[\\/]/);
    return parts[parts.length - 1] || path;
  }
</script>

<footer class="status-bar">
  {#if $scenarioStore !== undefined}
    <span class="scenario-name">{$scenarioStore.name}</span>
    {#if $filePathStore !== undefined}
      <span class="sep">·</span>
      <span class="file-path" title={$filePathStore}>{basename($filePathStore)}</span>
    {/if}
    <span class="sep">·</span>
  {/if}
  <span class="status-text">{$statusStore}</span>
  <span class="spacer"></span>
  {#if $scenarioStore !== undefined && $candidatesStore.length > 0}
    <span class="info-chip"
      >{$candidatesStore.length} candidate{$candidatesStore.length !== 1 ? 's' : ''}</span
    >
  {/if}
  {#if $isDirtyStore}
    <span class="dirty-chip">unsaved</span>
  {/if}
  <!-- Always-mounted visually-hidden live region: announcing changed text
       inside a persistent region is more reliable than mounting an already-
       populated one, so a 0->N warning transition is announced. -->
  <span class="sr-only" aria-live="polite" aria-atomic="true">
    {#if $warningsStore.length > 0}
      {$warningsStore.length} warning{$warningsStore.length !== 1 ? 's' : ''}
    {/if}
  </span>
  {#if $warningsStore.length > 0}
    <!-- Visible danger-red chip (design-system §5.6 / spec table; distinct
         from the amber unsaved chip). Decorative: the sr-only region above
         carries the accessible text, so this is aria-hidden to avoid a
         double announcement and the bare ⚠ reading as "warning sign". -->
    <span class="warn-chip" title={$warningsStore.join('\n')} aria-hidden="true">
      ⚠ {$warningsStore.length} warning{$warningsStore.length !== 1 ? 's' : ''}
    </span>
  {/if}
</footer>

<style>
  /* §6 Status bar — 32px tall, 24px horizontal padding, 12px text */
  .status-bar {
    display: flex;
    align-items: center;
    gap: 8px;
    height: 32px;
    padding: 0 24px;
    background: var(--bg-surface);
    border-top: 1px solid var(--border-subtle);
    font-size: 12px;
    color: var(--text-secondary);
    flex-shrink: 0;
  }

  .scenario-name {
    font-weight: 600;
    color: var(--accent-hover);
  }

  .file-path {
    color: var(--text-muted);
    font-family: var(--font-mono);
    font-size: 11px;
    cursor: help;
    max-width: 24rem;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  .sep {
    opacity: 0.4;
  }

  .spacer {
    flex: 1;
  }

  /* §5.6 Status chips — 20px tall, 4/8 padding, 10px radius */
  .info-chip {
    display: inline-flex;
    align-items: center;
    height: 20px;
    padding: 0 8px;
    background: color-mix(in srgb, var(--info) 14%, transparent);
    color: var(--info);
    border-radius: 10px;
    font-size: 11px;
    font-weight: 500;
    font-variant-numeric: tabular-nums;
  }

  .dirty-chip {
    display: inline-flex;
    align-items: center;
    height: 20px;
    padding: 0 8px;
    background: color-mix(in srgb, var(--warning) 12%, transparent);
    color: var(--warning);
    border-radius: 10px;
    font-size: 11px;
    font-weight: 500;
  }

  /* Visually-hidden live region. position:absolute keeps the always-mounted
     host out of the flex flow so it adds no gap when empty. */
  .sr-only {
    position: absolute;
    width: 1px;
    height: 1px;
    padding: 0;
    margin: -1px;
    overflow: hidden;
    clip: rect(0, 0, 0, 0);
    white-space: nowrap;
    border: 0;
  }

  .warn-chip {
    display: inline-flex;
    align-items: center;
    height: 20px;
    padding: 0 8px;
    background: color-mix(in srgb, var(--danger) 14%, transparent);
    color: var(--danger);
    border-radius: 10px;
    font-size: 11px;
    font-weight: 500;
    cursor: help;
  }
</style>
