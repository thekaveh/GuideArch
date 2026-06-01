<script lang="ts">
  export let tabs: string[];
  export let active: string;
  export let onSelect: (tab: string) => void = () => {};

  // Logical grouping: the first five tabs are AUTHOR tabs (edit the scenario);
  // the last three are ANALYSIS tabs (read solved state). Render a divider
  // between them so the workflow shape is legible at a glance.
  const AUTHORING = new Set(['Decisions', 'Alternatives', 'Properties', 'Coefficients', 'Constraints']);

  // The divider is rendered before the first tab that crosses the boundary,
  // i.e. before the first Analysis tab in the list. Memoise its index.
  $: dividerBefore = tabs.findIndex((t) => !AUTHORING.has(t));
</script>

<nav class="tab-strip">
  {#each tabs as tab, i (tab)}
    {#if i === dividerBefore && dividerBefore > 0}
      <span class="group-divider" aria-hidden="true"></span>
      <span class="group-label">Analysis</span>
    {/if}
    {#if i === 0}
      <span class="group-label">Author</span>
    {/if}
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

  .group-label {
    display: inline-flex;
    align-items: center;
    padding: 0 10px 0 0;
    color: var(--text-muted);
    font-size: 10px;
    font-weight: 600;
    letter-spacing: 0.08em;
    text-transform: uppercase;
    align-self: center;
  }

  .group-divider {
    display: inline-block;
    width: 1px;
    background: var(--border-strong);
    align-self: center;
    height: 18px;
    margin: 0 16px;
    flex-shrink: 0;
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
