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
  .tab-strip {
    display: flex;
    gap: 0;
    padding: 0 1.25rem;
    background: #16161e;
    border-bottom: 1px solid #2e2e38;
    flex-shrink: 0;
  }

  .tab-btn {
    padding: 0.55rem 1.1rem;
    background: transparent;
    color: #888899;
    border: none;
    border-bottom: 2px solid transparent;
    cursor: pointer;
    font-size: 0.83rem;
    font-weight: 500;
    transition:
      color 0.12s,
      border-color 0.12s;
    white-space: nowrap;
  }

  .tab-btn:hover {
    color: #c4b5fd;
  }

  .tab-btn.active {
    color: #e8e8ec;
    border-bottom-color: #7c3aed;
    font-weight: 600;
  }
</style>
