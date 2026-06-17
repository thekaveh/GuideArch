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
  <!-- Persistent live region: the wrapper stays mounted so a 0->N warning
       transition is announced. The visible danger-red chip (per design-system
       §5.6 / spec table; distinct from the amber unsaved chip) renders inside. -->
  <span class="warn-live" aria-live="polite" aria-atomic="true">
    {#if $warningsStore.length > 0}
      <span class="warn-chip" title={$warningsStore.join('\n')}>
        ⚠ {$warningsStore.length} warning{$warningsStore.length !== 1 ? 's' : ''}
      </span>
    {/if}
  </span>
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

  /* Wrapper is display:contents so the always-mounted live region adds no
     flex gap when empty; the visible chip lays out as before. */
  .warn-live {
    display: contents;
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
