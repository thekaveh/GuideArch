<script lang="ts">
  import type { PropertyTriangleSeries } from '../../view/chart-data.js';

  export let series: PropertyTriangleSeries[] = [];

  const PADDING = { top: 8, right: 90, bottom: 28, left: 40 };
  const CHART_HEIGHT = 140;

  // Distinct property colors (accent palette)
  const COLORS = [
    '#7c3aed',
    '#10b981',
    '#f59e0b',
    '#ef4444',
    '#3b82f6',
    '#ec4899',
    '#14b8a6',
    '#f97316',
    '#a855f7',
    '#22c55e',
  ];

  function seriesColor(i: number): string {
    return COLORS[i % COLORS.length];
  }

  let containerWidth = 300;
  $: innerWidth = Math.max(1, containerWidth - PADDING.left - PADDING.right);
  const innerHeight = CHART_HEIGHT - PADDING.top - PADDING.bottom;

  // Compute unified x domain across all series
  $: xDomain = computeXDomain(series);
  $: xMin = xDomain[0];
  $: xMax = xDomain[1];
  $: xRange = Math.max(xMax - xMin, 1e-9);

  function computeXDomain(s: PropertyTriangleSeries[]): [number, number] {
    if (s.length === 0) return [0, 1];
    const lo = s.reduce((m, x) => Math.min(m, x.points[0].x), s[0].points[0].x);
    const hi = s.reduce((m, x) => Math.max(m, x.points[2].x), s[0].points[2].x);
    return [lo, hi];
  }

  function scaleX(v: number): number {
    return ((v - xMin) / xRange) * innerWidth;
  }

  function scaleY(v: number): number {
    // v in [0,1], map 0→bottom, 1→top
    return innerHeight - v * innerHeight;
  }

  function pointsAttr(s: PropertyTriangleSeries): string {
    return s.points.map((p) => `${scaleX(p.x).toFixed(1)},${scaleY(p.y).toFixed(1)}`).join(' ');
  }

  function formatX(v: number): string {
    return v.toPrecision(3);
  }
</script>

<div class="chart-wrap" bind:clientWidth={containerWidth}>
  {#if series.length === 0}
    <div class="empty">Select a candidate to see its fuzzy profile.</div>
  {:else}
    <svg
      width={containerWidth}
      height={CHART_HEIGHT}
      role="img"
      aria-label="Fuzzy triangle property chart"
    >
      <title>Fuzzy Triangle Visualizer</title>
      <g transform="translate({PADDING.left},{PADDING.top})">
        <!-- Y-axis line -->
        <line x1={0} y1={0} x2={0} y2={innerHeight} class="axis-line" />
        <!-- X-axis line -->
        <line x1={0} y1={innerHeight} x2={innerWidth} y2={innerHeight} class="axis-line" />

        <!-- Y-axis labels -->
        <text x={-4} y={scaleY(0) + 4} class="axis-label" text-anchor="end">0</text>
        <text x={-4} y={scaleY(1) + 4} class="axis-label" text-anchor="end">1</text>
        <text
          x={-28}
          y={innerHeight / 2}
          class="axis-title"
          text-anchor="middle"
          transform="rotate(-90,-28,{innerHeight / 2})">μ</text
        >

        <!-- X-axis labels -->
        <text x={0} y={innerHeight + 14} class="axis-label" text-anchor="middle"
          >{formatX(xMin)}</text
        >
        <text x={innerWidth} y={innerHeight + 14} class="axis-label" text-anchor="middle"
          >{formatX(xMax)}</text
        >
        <text x={innerWidth / 2} y={innerHeight + 24} class="axis-title" text-anchor="middle"
          >Value</text
        >

        <!-- Triangle polylines -->
        {#each series as s, i (s.propertyId)}
          <polyline
            points={pointsAttr(s)}
            fill={seriesColor(i)}
            fill-opacity="0.15"
            stroke={seriesColor(i)}
            stroke-width="1.5"
          />
        {/each}

        <!-- Legend (right side, outside chart area) -->
        {#each series as s, i (s.propertyId + '-legend')}
          <g transform="translate({innerWidth + 6},{i * 14})">
            <rect width={8} height={8} y={1} fill={seriesColor(i)} rx={1} />
            <text x={11} y={9} class="legend-label">{s.propertyName}</text>
          </g>
        {/each}
      </g>
    </svg>
  {/if}
</div>

<style>
  .chart-wrap {
    width: 100%;
    overflow: hidden;
  }

  .empty {
    color: #555566;
    font-size: 0.8rem;
    padding: 0.75rem;
    text-align: center;
  }

  svg {
    display: block;
  }

  .axis-line {
    stroke: #3e3e50;
    stroke-width: 1;
  }

  .axis-label {
    fill: #666677;
    font-size: 9px;
  }

  .axis-title {
    fill: #666677;
    font-size: 9px;
  }

  .legend-label {
    fill: #aaaabc;
    font-size: 9px;
  }
</style>
