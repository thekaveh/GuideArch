<script lang="ts">
  /** Tab title — short, headline case. e.g. "Decisions". */
  export let title: string;
  /**
   * One-line context describing what's in this section. e.g.
   * "10 architectural choices" or "Quality criteria with kind + weight".
   * Updates live as state changes.
   */
  export let subtitle: string = '';
  /**
   * Optional primary action — usually "+ Add X". Rendered on the right.
   * When null no button is rendered so tabs without a primary action
   * (Results, Critical Decisions, Critical Constraints) can still use
   * the same header.
   */
  export let action: { label: string; onClick: () => void } | null = null;
</script>

<header class="section-header">
  <div class="text-block">
    <div class="title">{title}</div>
    {#if subtitle}
      <div class="subtitle">{subtitle}</div>
    {/if}
  </div>
  {#if action !== null}
    <button class="btn-add" on:click={action.onClick}>{action.label}</button>
  {/if}
</header>

<style>
  .section-header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 16px;
    padding: 16px 24px 12px 24px;
    border-bottom: 1px solid var(--border-subtle);
    flex-shrink: 0;
  }

  .text-block {
    min-width: 0;
  }

  .title {
    font-size: 16px;
    font-weight: 600;
    color: var(--text-primary);
    letter-spacing: -0.01em;
    line-height: 1.2;
  }

  .subtitle {
    font-size: 12px;
    color: var(--text-muted);
    margin-top: 2px;
  }

  .btn-add {
    display: inline-flex;
    align-items: center;
    gap: 6px;
    height: 30px;
    padding: 0 14px;
    background: var(--accent);
    color: var(--accent-on);
    border: none;
    border-radius: 6px;
    cursor: pointer;
    font-size: 13px;
    font-weight: 500;
    flex-shrink: 0;
    transition: background-color 80ms ease-out;
  }

  .btn-add:hover {
    background: var(--accent-hover);
  }
</style>
