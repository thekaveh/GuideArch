<script lang="ts">
  import type { ComparisonSeries } from '../../view/chart-data.js';

  /** One series per candidate; expected length 1..10. */
  export let series: ComparisonSeries[] = [];
  /** Property labels along X axis. */
  export let propertyNames: string[] = [];
  /** Currently selected candidate rank, or null. Highlights the matching polyline. */
  export let selectedRank: number | null = null;
  /** Click on a polyline or legend chip → select that candidate's rank. */
  export let onSelect: (rank: number) => void = () => {};

  const PADDING = { top: 10, right: 14, bottom: 30, left: 38 };
  const HEIGHT = 220;
  const innerHeight = HEIGHT - PADDING.top - PADDING.bottom;

  let containerWidth = 400;

  $: innerWidth = Math.max(1, containerWidth - PADDING.left - PADDING.right);

  // Y axis = modal value. Scale on the highest observed modal across all
  // series, with a small floor so an empty/all-zero series doesn't blow up.
  $: maxY = series.reduce((m, s) => s.points.reduce((mm, p) => Math.max(mm, p.modal), m), 0) || 1;
  // X axis = property index, 0..N-1. One slot per property.
  $: xCount = Math.max(1, propertyNames.length);

  function xPos(idx: number): number {
    if (xCount === 1) return innerWidth / 2;
    return (idx / (xCount - 1)) * innerWidth;
  }
  function yPos(modal: number): number {
    return innerHeight - (modal / maxY) * innerHeight;
  }
  function toPath(points: { propertyIndex: number; modal: number }[]): string {
    if (points.length === 0) return '';
    const cmds = points.map((p, i) => {
      const cmd = i === 0 ? 'M' : 'L';
      return `${cmd}${xPos(p.propertyIndex).toFixed(2)},${yPos(p.modal).toFixed(2)}`;
    });
    return cmds.join(' ');
  }
  function strokeOpacity(rank: number): number {
    if (selectedRank === null) return 0.9;
    return rank === selectedRank ? 1 : 0.25;
  }
  function strokeWidth(rank: number): number {
    return rank === selectedRank ? 2.5 : 1.4;
  }
  function handleClick(rank: number) {
    onSelect(rank);
  }
  function handleKeydown(e: KeyboardEvent, rank: number) {
    if (e.key === 'Enter' || e.key === ' ') {
      e.preventDefault();
      onSelect(rank);
    }
  }
</script>

<div class="chart" bind:clientWidth={containerWidth}>
  {#if series.length === 0 || propertyNames.length === 0}
    <div class="empty">No candidates to compare yet.</div>
  {:else}
    <svg width={containerWidth} height={HEIGHT} role="img" aria-label="Top candidates comparison">
      <!-- Y axis ticks (4 + zero) -->
      <g transform="translate({PADDING.left},{PADDING.top})" class="axis">
        {#each [0, 0.25, 0.5, 0.75, 1] as t (t)}
          <line x1="0" x2={innerWidth} y1={innerHeight * (1 - t)} y2={innerHeight * (1 - t)} />
          <text x="-6" y={innerHeight * (1 - t) + 3} text-anchor="end">
            {(t * maxY).toFixed(2)}
          </text>
        {/each}
      </g>

      <!-- X axis labels -->
      <g transform="translate({PADDING.left},{PADDING.top + innerHeight})" class="x-labels">
        {#each propertyNames as name, idx (idx)}
          <text x={xPos(idx)} y="14" text-anchor="middle">
            {name.length > 8 ? name.slice(0, 7) + '…' : name}
          </text>
        {/each}
      </g>

      <!-- Polylines (back-to-front so selected sits on top) -->
      <g transform="translate({PADDING.left},{PADDING.top})">
        {#each series.filter((s) => s.rank !== selectedRank) as s (s.rank)}
          <path
            d={toPath(s.points)}
            stroke={s.color}
            stroke-width={strokeWidth(s.rank)}
            stroke-opacity={strokeOpacity(s.rank)}
            fill="none"
            class="line"
            role="button"
            tabindex={0}
            aria-label="Candidate rank {s.rank}, score {s.score}"
            on:click={() => handleClick(s.rank)}
            on:keydown={(e) => handleKeydown(e, s.rank)}
          />
        {/each}
        {#each series.filter((s) => s.rank === selectedRank) as s (s.rank)}
          <path
            d={toPath(s.points)}
            stroke={s.color}
            stroke-width={strokeWidth(s.rank)}
            stroke-opacity={strokeOpacity(s.rank)}
            fill="none"
            class="line selected"
            role="button"
            tabindex={0}
            aria-label="Candidate rank {s.rank}, score {s.score}"
            on:click={() => handleClick(s.rank)}
            on:keydown={(e) => handleKeydown(e, s.rank)}
          />
        {/each}
      </g>
    </svg>

    <!-- Legend chips: click → select that candidate -->
    <div class="legend">
      {#each series as s (s.rank)}
        <button
          class="chip"
          class:active={s.rank === selectedRank}
          on:click={() => handleClick(s.rank)}
        >
          <span class="swatch" style:background-color={s.color}></span>
          <span class="chip-label">{s.label}</span>
        </button>
      {/each}
    </div>
  {/if}
</div>

<style>
  .chart {
    display: flex;
    flex-direction: column;
    gap: 8px;
    width: 100%;
  }

  .empty {
    color: var(--text-muted);
    font-size: 12px;
    padding: 20px 0;
    text-align: center;
  }

  svg {
    display: block;
  }

  .axis line {
    stroke: var(--border-subtle);
    stroke-width: 0.5;
    stroke-dasharray: 2 3;
  }

  .axis text {
    fill: var(--text-muted);
    font-size: 10px;
    font-family: var(--font-mono);
  }

  .x-labels text {
    fill: var(--text-muted);
    font-size: 10px;
  }

  .line {
    cursor: pointer;
    transition:
      stroke-width 80ms ease-out,
      stroke-opacity 80ms ease-out;
  }

  .line:hover {
    stroke-width: 2.5;
    stroke-opacity: 1;
  }

  .legend {
    display: flex;
    flex-wrap: wrap;
    gap: 4px;
  }

  .chip {
    display: inline-flex;
    align-items: center;
    gap: 6px;
    background: var(--bg-surface-2);
    border: 1px solid var(--border-subtle);
    border-radius: 12px;
    padding: 2px 8px;
    font-size: 11px;
    color: var(--text-secondary);
    cursor: pointer;
    font-family: var(--font-mono);
  }

  .chip:hover {
    background: var(--bg-surface-3);
    color: var(--text-primary);
  }

  .chip.active {
    background: var(--accent-muted);
    border-color: var(--accent);
    color: var(--text-primary);
  }

  .swatch {
    width: 10px;
    height: 10px;
    border-radius: 2px;
    flex-shrink: 0;
  }

  .chip-label {
    white-space: nowrap;
  }
</style>
