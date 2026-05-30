<script lang="ts">
  export let tabs: string[];
  export let active: string;
  export let onSelect: (tab: string) => void = () => {};
</script>

<nav class="tab-strip">
  {#each tabs as tab (tab)}
    <button class="tab-btn" class:active={tab === active} on:click={() => onSelect(tab)}>
      {tab}
    </button>
  {/each}
</nav>

<style>
  /* §5.4 Tabs — 40px tall strip, 1px border-subtle below */
  .tab-strip {
    display: flex;
    gap: 0;
    padding: 0 24px;
    height: 40px;
    background: var(--bg-surface);
    border-bottom: 1px solid var(--border-subtle);
    flex-shrink: 0;
    align-items: stretch;
  }

  .tab-btn {
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

  .tab-btn:hover {
    color: var(--text-primary);
    background: var(--bg-surface-2);
  }

  .tab-btn.active {
    color: var(--text-primary);
    border-bottom-color: var(--accent);
    font-weight: 600;
  }
</style>
