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
  .status-bar {
    display: flex;
    align-items: center;
    gap: 0.5rem;
    padding: 0.35rem 1.25rem;
    background: #13131a;
    border-top: 1px solid #2e2e38;
    font-size: 0.78rem;
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

  .dirty-chip {
    padding: 0.1rem 0.45rem;
    background: #3d2a00;
    color: #fbbf24;
    border-radius: 10px;
    font-size: 0.7rem;
    font-weight: 600;
    border: 1px solid #78400a;
  }

  .warn-chip {
    padding: 0.1rem 0.45rem;
    background: #2d2000;
    color: #fcd34d;
    border-radius: 10px;
    font-size: 0.7rem;
    font-weight: 600;
    border: 1px solid #78400a;
    cursor: help;
  }
</style>
