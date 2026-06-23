import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { describe, it, expect } from 'vitest';

function read(rel: string): string {
  return readFileSync(fileURLToPath(new URL(rel, import.meta.url)), 'utf8');
}

const TABS = ['CriticalDecisionsTab', 'CriticalConstraintsTab'] as const;

describe('analysis tabs render a SectionHeader (§5.9)', () => {
  for (const tab of TABS) {
    const src = read(`../../src/routes/lib/${tab}.svelte`);

    it(`${tab} imports SectionHeader`, () => {
      expect(src).toMatch(/import\s+SectionHeader\s+from\s+'\.\/SectionHeader\.svelte'/);
    });

    it(`${tab} uses <SectionHeader> with a title`, () => {
      expect(src).toMatch(/<SectionHeader\b[\s\S]*?title=/);
    });
  }
});
