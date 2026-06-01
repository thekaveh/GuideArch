<script lang="ts">
  export let tabs: string[];
  export let active: string;
  export let onSelect: (tab: string) => void = () => {};

  // Logical grouping: the first five tabs are AUTHOR tabs (edit the scenario);
  // the last three are ANALYSIS tabs (read solved state). Render a divider
  // between them so the workflow shape is legible at a glance.
  const AUTHORING = new Set(['Decisions', 'Alternatives', 'Properties', 'Coefficients', 'Constraints']);

  // Lucide-style 14px icons, one per tab. Stored as path data so each tab
  // button stays a single button element. Same icon assignments are
  // mirrored in the C# and Python tab strips for cross-impl identity.
  const TAB_ICONS: Record<string, string> = {
    Decisions:
      // git-branch
      'M6 3v12 M18 9a3 3 0 1 0 0-6 3 3 0 0 0 0 6Z M6 21a3 3 0 1 0 0-6 3 3 0 0 0 0 6Z M6 15a9 9 0 0 0 9-9',
    Alternatives:
      // layers
      'M12 2 2 7l10 5 10-5-10-5Z M2 17l10 5 10-5 M2 12l10 5 10-5',
    Properties:
      // sliders-horizontal
      'M21 4H14 M10 4H3 M21 12H12 M8 12H3 M21 20H16 M12 20H3 M14 2v4 M8 10v4 M16 18v4',
    Coefficients:
      // grid-3x3
      'M3 3h18v18H3z M3 9h18 M3 15h18 M9 3v18 M15 3v18',
    Constraints:
      // filter
      'M22 3H2l8 9.46V19l4 2v-8.54L22 3Z',
    Results:
      // bar-chart-3
      'M3 3v18h18 M18 17V9 M13 17V5 M8 17v-3',
    'Critical Decisions':
      // target
      'M12 22a10 10 0 1 0 0-20 10 10 0 0 0 0 20Z M12 18a6 6 0 1 0 0-12 6 6 0 0 0 0 12Z M12 14a2 2 0 1 0 0-4 2 2 0 0 0 0 4Z',
    'Critical Constraints':
      // shield-alert
      'M20 13c0 5-3.5 7.5-7.66 8.95a1 1 0 0 1-.67-.01C7.5 20.5 4 18 4 13V6a1 1 0 0 1 1-1c2 0 4.5-1.2 6.24-2.72a1.17 1.17 0 0 1 1.52 0C14.51 3.81 17 5 19 5a1 1 0 0 1 1 1Z M12 8v4 M12 16h.01',
  };

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
      <svg
        class="tab-icon"
        width="14"
        height="14"
        viewBox="0 0 24 24"
        fill="none"
        stroke="currentColor"
        stroke-width="2"
        stroke-linecap="round"
        stroke-linejoin="round"
        aria-hidden="true"
      >
        <path d={TAB_ICONS[tab] ?? ''} />
      </svg>
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
    display: inline-flex;
    align-items: center;
    gap: 6px;
    padding: 0 14px;
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

  .tab-icon {
    opacity: 0.85;
    flex-shrink: 0;
  }

  .tab-btn.active .tab-icon {
    opacity: 1;
    color: var(--accent);
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
