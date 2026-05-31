<script lang="ts">
  import type { CandidateBarDatum } from '../../view/chart-data.js';

  /** Chart data rows, already limited to top-N. */
  export let data: CandidateBarDatum[] = [];
  /** Currently selected index (0-based rank), or null. */
  export let selectedIndex: number | null = null;
  /** Called when a bar is clicked. */
  export let onSelect: (rank: number) => void = () => {};

  const PADDING = { top: 8, right: 16, bottom: 28, left: 48 };
  const ROW_HEIGHT = 18;
  const MIN_HEIGHT = 120;

  $: svgHeight = Math.max(MIN_HEIGHT, data.length * ROW_HEIGHT + PADDING.top + PADDING.bottom);

  let containerWidth = 300;
  $: innerWidth = Math.max(1, containerWidth - PADDING.left - PADDING.right);
  $: innerHeight = svgHeight - PADDING.top - PADDING.bottom;

  $: maxScore = data.reduce((m, d) => Math.max(m, d.score), 0) || 1;

  function barWidth(score: number): number {
    return (score / maxScore) * innerWidth;
  }

  function barY(rank: number): number {
    return rank * ROW_HEIGHT;
  }

  function barOpacity(rank: number): number {
    // Full opacity at rank 0, half opacity at rank 30
    return 1 - (rank / Math.max(data.length - 1, 30)) * 0.5;
  }

  function formatScore(s: number): string {
    return s.toPrecision(4);
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

<div class="chart-wrap" bind:clientWidth={containerWidth}>
  {#if data.length === 0}
    <div class="empty">No candidates to chart.</div>
  {:else}
    <svg
      width={containerWidth}
      height={svgHeight}
      role="img"
      aria-label="Ranked candidates score chart"
    >
      <title>Ranked Candidates Score Chart</title>
      <g transform="translate({PADDING.left},{PADDING.top})">
        <!-- X-axis line -->
        <line x1={0} y1={innerHeight} x2={innerWidth} y2={innerHeight} class="axis-line" />
        <!-- X-axis ticks (simplified: 0 and max) -->
        <text x={0} y={innerHeight + 14} class="axis-label" text-anchor="middle">0</text>
        <text x={innerWidth} y={innerHeight + 14} class="axis-label" text-anchor="middle"
          >{formatScore(maxScore)}</text
        >
        <text x={innerWidth / 2} y={innerHeight + 26} class="axis-title" text-anchor="middle"
          >Score</text
        >

        {#each data as d (d.rank)}
          {@const y = barY(d.rank)}
          {@const bw = barWidth(d.score)}
          {@const opacity = barOpacity(d.rank)}
          {@const isSelected = d.rank === selectedIndex}

          <!-- Bar -->
          <rect
            x={0}
            y={y + 2}
            width={Math.max(1, bw)}
            height={ROW_HEIGHT - 4}
            fill="var(--accent)"
            fill-opacity={opacity}
            stroke={isSelected ? 'var(--accent-hover)' : 'none'}
            stroke-width={isSelected ? 1.5 : 0}
            class="bar"
            role="button"
            tabindex={0}
            aria-label="Rank {d.rank} score {formatScore(d.score)}"
            on:click={() => handleClick(d.rank)}
            on:keydown={(e) => handleKeydown(e, d.rank)}
          >
            <title>Rank {d.rank} · {formatScore(d.score)} · {d.altNames.join(', ')}</title>
          </rect>

          <!-- Y-axis rank label -->
          <text x={-4} y={y + ROW_HEIGHT / 2 + 4} class="rank-label" text-anchor="end">
            {d.rank}
          </text>
        {/each}
      </g>
    </svg>
  {/if}
</div>

<style>
  /* §5.7 Charts */
  .chart-wrap {
    width: 100%;
    overflow: hidden;
    background: var(--bg-surface);
  }

  .empty {
    color: var(--text-secondary);
    font-size: 12px;
    padding: 16px;
    text-align: center;
  }

  svg {
    display: block;
  }

  /* Grid lines at 50% alpha per §5.7 */
  .axis-line {
    stroke: var(--border-subtle);
    stroke-width: 1;
    stroke-opacity: 0.5;
  }

  /* Axis labels: text-secondary, 12px per §5.7 */
  .axis-label {
    fill: var(--text-secondary);
    font-size: 9px;
  }

  .axis-title {
    fill: var(--text-secondary);
    font-size: 9px;
  }

  .rank-label {
    fill: var(--text-secondary);
    font-size: 9px;
  }

  .bar {
    cursor: pointer;
    outline: none;
    transition: fill-opacity 120ms ease-out;
  }

  .bar:hover {
    fill: var(--accent-hover);
  }

  .bar:focus {
    stroke: var(--accent-hover);
    stroke-width: 1.5px;
  }
</style>
