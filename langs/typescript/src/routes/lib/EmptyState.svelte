<script lang="ts">
  /** Headline — short, sentence case. */
  export let headline: string;
  /** Body — one sentence describing what to do next. */
  export let body: string;
  /** Optional kicker shown above the headline (e.g. "Welcome"). */
  export let kicker: string | null = null;
  /**
   * Layout: 'hero' centers a large composition for first-launch / top-level
   * empties; 'compact' is a smaller inline empty for secondary contexts
   * (a tab with no rows once a scenario IS loaded).
   */
  export let variant: 'hero' | 'compact' = 'compact';
  /** Optional primary action button. */
  export let primary: { label: string; onClick: () => void } | null = null;
  /** Optional secondary action button (rendered next to primary). */
  export let secondary: { label: string; onClick: () => void } | null = null;
  /**
   * Optional muted action shown beneath the buttons (e.g. "or open a file").
   * Rendered as a text-only button.
   */
  export let tertiary: { label: string; onClick: () => void } | null = null;
</script>

<div class="empty-state" class:hero={variant === 'hero'} class:compact={variant === 'compact'}>
  {#if variant === 'hero'}
    <!-- §1 — Hero illustration: stylized fuzzy-triangle motif tying back to the
         domain (triangular fuzzy numbers). Pure SVG so it themes cleanly. -->
    <svg
      class="illustration"
      width="120"
      height="96"
      viewBox="0 0 120 96"
      fill="none"
      aria-hidden="true"
    >
      <path
        d="M10 78 L40 18 L70 78 Z"
        stroke="currentColor"
        stroke-width="1.4"
        fill="currentColor"
        fill-opacity="0.08"
      />
      <path
        d="M40 78 L70 12 L100 78 Z"
        stroke="currentColor"
        stroke-width="1.4"
        fill="currentColor"
        fill-opacity="0.16"
      />
      <path
        d="M70 78 L92 36 L114 78 Z"
        stroke="currentColor"
        stroke-width="1.4"
        fill="currentColor"
        fill-opacity="0.24"
      />
      <line
        x1="6"
        x2="118"
        y1="80"
        y2="80"
        stroke="currentColor"
        stroke-opacity="0.4"
        stroke-width="1"
      />
    </svg>
  {/if}

  {#if kicker !== null}
    <div class="kicker">{kicker}</div>
  {/if}
  <div class="headline">{headline}</div>
  <div class="body">{body}</div>

  {#if primary !== null || secondary !== null}
    <div class="actions">
      {#if primary !== null}
        <button class="btn btn-primary" on:click={primary.onClick}>{primary.label}</button>
      {/if}
      {#if secondary !== null}
        <button class="btn btn-secondary" on:click={secondary.onClick}>{secondary.label}</button>
      {/if}
    </div>
  {/if}

  {#if tertiary !== null}
    <button class="btn-text" on:click={tertiary.onClick}>{tertiary.label}</button>
  {/if}
</div>

<style>
  .empty-state {
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    text-align: center;
    color: var(--text-secondary);
  }

  .empty-state.hero {
    gap: 12px;
    padding: 64px 32px;
    flex: 1;
    min-height: 60vh;
  }

  .empty-state.compact {
    gap: 6px;
    padding: 28px 24px;
    flex: 1;
  }

  .illustration {
    color: var(--accent);
    margin-bottom: 8px;
  }

  .kicker {
    color: var(--accent-hover);
    font-size: 11px;
    font-weight: 600;
    letter-spacing: 0.08em;
    text-transform: uppercase;
  }

  .headline {
    color: var(--text-primary);
    font-weight: 600;
    line-height: 1.3;
  }

  .empty-state.hero .headline {
    font-size: 22px;
  }

  .empty-state.compact .headline {
    font-size: 14px;
  }

  .body {
    color: var(--text-muted);
    line-height: 1.55;
    max-width: 36rem;
  }

  .empty-state.hero .body {
    font-size: 14px;
    margin-bottom: 8px;
  }

  .empty-state.compact .body {
    font-size: 13px;
  }

  .actions {
    display: flex;
    gap: 8px;
    margin-top: 8px;
    flex-wrap: wrap;
    justify-content: center;
  }

  .btn {
    height: 36px;
    padding: 0 16px;
    border-radius: 6px;
    font-size: 13px;
    font-weight: 500;
    cursor: pointer;
    border: 1px solid transparent;
    transition:
      background-color 80ms ease-out,
      border-color 80ms ease-out;
  }

  .btn-primary {
    background: var(--accent);
    color: var(--accent-on);
    border-color: var(--accent);
  }

  .btn-primary:hover {
    background: var(--accent-hover);
    border-color: var(--accent-hover);
  }

  .btn-secondary {
    background: var(--bg-surface-2);
    color: var(--text-primary);
    border-color: var(--border-strong);
  }

  .btn-secondary:hover {
    background: var(--bg-surface-3);
    border-color: var(--accent);
  }

  .btn-text {
    background: none;
    border: none;
    color: var(--text-muted);
    font-size: 12px;
    margin-top: 4px;
    cursor: pointer;
    padding: 4px 8px;
  }

  .btn-text:hover {
    color: var(--accent-hover);
    text-decoration: underline;
  }
</style>
