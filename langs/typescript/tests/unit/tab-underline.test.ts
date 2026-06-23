import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { describe, it, expect } from 'vitest';

function read(rel: string): string {
  return readFileSync(fileURLToPath(new URL(rel, import.meta.url)), 'utf8');
}
const tabStrip = read('../../src/routes/lib/TabStrip.svelte');
const resultsTab = read('../../src/routes/lib/ResultsTab.svelte');

/** Body of the first CSS rule whose selector text contains `needle`. */
function rule(css: string, needle: string): string {
  const i = css.indexOf(needle);
  if (i === -1) throw new Error(`selector not found: ${needle}`);
  const open = css.indexOf('{', i);
  const close = css.indexOf('}', open);
  return css.slice(open + 1, close);
}

describe('main tab strip', () => {
  it('defines a :focus-visible ring on tab buttons', () => {
    expect(tabStrip).toContain('.tab-btn:focus-visible');
  });
});

describe('Results right-rail sub-tabs use the underline idiom (§6.4)', () => {
  it('the active sub-tab draws a 2px accent underline', () => {
    const active = rule(resultsTab, '.right-tab.active');
    expect(active).toMatch(/border-bottom[^;]*var\(--accent\)/);
  });

  it('the active sub-tab is NOT a full accent fill', () => {
    const active = rule(resultsTab, '.right-tab.active');
    expect(active).not.toMatch(/background:\s*var\(--accent\)/);
  });

  it('sub-tab buttons get a :focus-visible ring', () => {
    expect(resultsTab).toContain('.right-tab:focus-visible');
  });
});
