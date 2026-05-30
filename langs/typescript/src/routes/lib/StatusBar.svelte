<script lang="ts">
  import type { ScenarioVM } from '../../viewmodels/scenario-vm.js';
  import { vmxToStore } from '../../view/adapters/vmx-to-svelte.js';

  export let vm: ScenarioVM;

  $: scenarioStore = vmxToStore(vm, 'scenario');
  $: statusStore = vmxToStore(vm, 'status');
  $: warningsStore = vmxToStore(vm, 'warnings');
  $: isDirtyStore = vmxToStore(vm, 'isDirty');
</script>

<footer class="status-bar">
  {#if $scenarioStore !== undefined}
    <span class="scenario-name">{$scenarioStore.name}</span>
    <span class="sep">·</span>
  {/if}
  <span class="status-text">{$statusStore}</span>
  {#if $isDirtyStore}
    <span class="dirty-chip">unsaved</span>
  {/if}
  {#if $warningsStore.length > 0}
    <span class="warn-chip" title={$warningsStore.join('\n')}>
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

  .sep {
    opacity: 0.4;
  }

  /* §5.6 Status chips — 20px tall, 4/8 padding, 10px radius */
  .dirty-chip {
    display: inline-flex;
    align-items: center;
    height: 20px;
    padding: 0 8px;
    background: color-mix(in srgb, var(--warning) 12%, transparent);
    color: var(--warning);
    border-radius: 10px;
    font-size: 12px;
    font-weight: 500;
  }

  .warn-chip {
    display: inline-flex;
    align-items: center;
    height: 20px;
    padding: 0 8px;
    background: color-mix(in srgb, var(--warning) 12%, transparent);
    color: var(--warning);
    border-radius: 10px;
    font-size: 12px;
    font-weight: 500;
    cursor: help;
  }
</style>
