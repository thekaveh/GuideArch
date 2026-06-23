import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { describe, it, expect } from 'vitest';

const src = readFileSync(
  fileURLToPath(new URL('../../src/routes/lib/Toolbar.svelte', import.meta.url)),
  'utf8',
);

describe('Toolbar markup', () => {
  it('the Save As… button ships an inline SVG icon', () => {
    // Grab the <button …>…</button> whose body contains the Save As… label.
    const m = src.match(/<button[^>]*handleSaveAs[^>]*>([\s\S]*?)<\/button>/);
    expect(m, 'Save As… button not found').not.toBeNull();
    expect(m![1], 'Save As… button has no <svg> icon').toContain('<svg');
  });

  it('defines a :focus-visible ring in the toolbar styles', () => {
    expect(src).toContain(':focus-visible');
  });

  it('keeps the icon-only theme toggle at 4px radius', () => {
    // The .btn-icon rule must declare 4px (spec §5.1 icon-only radius).
    const m = src.match(/\.btn-icon\s*\{([\s\S]*?)\}/);
    expect(m, '.btn-icon rule not found').not.toBeNull();
    expect(m![1]).toMatch(/border-radius:\s*4px/);
  });
});
