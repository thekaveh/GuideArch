import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { describe, it, expect } from 'vitest';

function read(rel: string): string {
  return readFileSync(fileURLToPath(new URL(rel, import.meta.url)), 'utf8');
}

const SRC = read('../../src/routes/lib/ConfirmDialog.svelte');

describe('ConfirmDialog honors the §7 motion rule (no entrance animations)', () => {
  it('declares no @keyframes', () => {
    expect(SRC).not.toMatch(/@keyframes/);
  });

  it('sets no animation: property', () => {
    // Catches `animation:`, `animation-name:`, etc. — any CSS animation hook.
    expect(SRC).not.toMatch(/\banimation(-name|-duration)?\s*:/);
  });

  it('still renders the styled card, scrim, and Esc/Enter handler (§5.10 intact)', () => {
    expect(SRC).toContain('var(--bg-surface-3)');
    expect(SRC).toContain('confirm-overlay');
    expect(SRC).toMatch(/Escape/);
    expect(SRC).toMatch(/Enter/);
  });
});
