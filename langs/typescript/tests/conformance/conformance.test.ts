import { describe, it, expect } from 'vitest';
import { runConformance } from '../../src/conformance/runner.js';

describe('conformance', () => {
  it('all scenarios pass within tolerance', () => {
    const { failed, divergences } = runConformance();
    if (failed > 0) {
      const summary = divergences.flat().slice(0, 10);
      throw new Error(
        `${failed} scenario(s) failed conformance:\n` +
          summary
            .map(
              (d) =>
                `  ${d.field}: expected=${JSON.stringify(d.expected)}, actual=${JSON.stringify(d.actual)}`,
            )
            .join('\n'),
      );
    }
    expect(failed).toBe(0);
  });
});
