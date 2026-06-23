import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { describe, it, expect } from 'vitest';

const css = readFileSync(fileURLToPath(new URL('../../src/app.css', import.meta.url)), 'utf8');

/** Extract the `--token` names declared inside the first `{…}` after `selector`. */
function tokensIn(selector: string): Set<string> {
  const start = css.indexOf(selector);
  if (start === -1) throw new Error(`selector not found: ${selector}`);
  const open = css.indexOf('{', start);
  const close = css.indexOf('}', open);
  const body = css.slice(open + 1, close);
  return new Set([...body.matchAll(/--([a-z0-9-]+)\s*:/g)].map((m) => m[1]));
}

const COLOR_TOKENS = [
  'bg-page',
  'bg-surface',
  'bg-surface-2',
  'bg-surface-3',
  'border-subtle',
  'border-strong',
  'text-primary',
  'text-secondary',
  'text-muted',
  'text-inverse',
  'accent',
  'accent-hover',
  'accent-muted',
  'accent-on',
  'success',
  'warning',
  'danger',
  'info',
  'fuzzy-positive',
  'fuzzy-average',
  'fuzzy-negative',
];

describe('design-system color tokens', () => {
  it('light theme overrides every color token defined in :root', () => {
    const light = tokensIn("[data-theme='light']");
    for (const t of COLOR_TOKENS) expect(light, `light missing --${t}`).toContain(t);
  });

  it(':root defines every required color token', () => {
    const root = tokensIn(':root');
    for (const t of COLOR_TOKENS) expect(root, `:root missing --${t}`).toContain(t);
  });
});
