import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { describe, it, expect } from 'vitest';

function read(rel: string): string {
  return readFileSync(fileURLToPath(new URL(rel, import.meta.url)), 'utf8');
}

const TABS = [
  'PropertiesTab',
  'CriticalDecisionsTab',
  'CriticalConstraintsTab',
  'ResultsTab',
] as const;

describe('empty states route through the shared EmptyState (§5.8)', () => {
  for (const tab of TABS) {
    const src = read(`../../src/routes/lib/${tab}.svelte`);

    it(`${tab} imports EmptyState`, () => {
      expect(src).toMatch(/import\s+EmptyState\s+from\s+'\.\/EmptyState\.svelte'/);
    });

    it(`${tab} has no hand-rolled .empty block`, () => {
      expect(src).not.toContain('class="empty"');
    });

    it(`${tab} no longer defines the .empty CSS rule`, () => {
      expect(src).not.toMatch(/\.empty\s*\{/);
      expect(src).not.toMatch(/\.empty-headline\s*\{/);
      expect(src).not.toMatch(/\.empty-body\s*\{/);
    });
  }
});
