import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { describe, it, expect } from 'vitest';

function read(rel: string): string {
  return readFileSync(fileURLToPath(new URL(rel, import.meta.url)), 'utf8');
}

describe('chart fills/strokes use tokens so they retint (§5.7)', () => {
  const fuzzy = read('../../src/routes/lib/FuzzyTriangleChart.svelte');

  it('FuzzyTriangleChart uses the three fuzzy-axis tokens', () => {
    expect(fuzzy).toContain('var(--fuzzy-positive)');
    expect(fuzzy).toContain('var(--fuzzy-average)');
    expect(fuzzy).toContain('var(--fuzzy-negative)');
  });

  it('FuzzyTriangleChart COLORS array has no hardcoded hex', () => {
    // Isolate the COLORS array body and assert no `#rrggbb` literal remains.
    const m = fuzzy.match(/const COLORS\s*=\s*\[([\s\S]*?)\]/);
    expect(m).not.toBeNull();
    expect(m![1]).not.toMatch(/#[0-9a-fA-F]{3,6}/);
  });

  it('RankedCandidatesChart fills bars with the accent token', () => {
    const ranked = read('../../src/routes/lib/RankedCandidatesChart.svelte');
    expect(ranked).toContain('var(--accent)');
  });

  it('CandidateComparisonChart sources colors from the Tableau-10 palette (intentional fixed hex)', () => {
    // The comparison palette is qualitative + theme-stable by design
    // (spec/charts.md §4) — it lives in chart-data.ts, and the component
    // consumes it via s.color rather than declaring its own hex.
    const compare = read('../../src/routes/lib/CandidateComparisonChart.svelte');
    expect(compare).toContain('s.color');
    const data = read('../../src/view/chart-data.ts');
    expect(data).toContain('COMPARISON_PALETTE');
    expect(data).toContain('#4e79a7');
  });
});
