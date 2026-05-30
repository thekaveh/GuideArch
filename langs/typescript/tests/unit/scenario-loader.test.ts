import { describe, it, expect } from 'vitest';
import path from 'path';
import fs from 'fs';
import { loadScenario, ScenarioValidationError } from '../../src/models/scenario-loader.js';
import { TriangularFuzzyM } from '../../src/models/triangular-fuzzy.js';

// ---------------------------------------------------------------------------
// Helpers: build a valid minimal scenario JSON on disk
// ---------------------------------------------------------------------------
import os from 'os';

function writeTempScenario(obj: unknown): string {
  const dir = os.tmpdir();
  const p = path.join(dir, `test-scenario-${Date.now()}-${Math.random()}.json`);
  fs.writeFileSync(p, JSON.stringify(obj));
  return p;
}

const VALID_BASE = {
  schemaVersion: '1.0.0',
  name: 'Test',
  decisions: [{ id: 'd1', name: 'D1' }],
  alternatives: [
    { id: 'a1', decisionId: 'd1', name: 'A1' },
    { id: 'a2', decisionId: 'd1', name: 'A2' },
  ],
  properties: [{ id: 'p1', name: 'P1', kind: 'min', weight: 1 }],
  coefficients: [
    { alternativeId: 'a1', propertyId: 'p1', value: { lower: 1, modal: 2, upper: 3 } },
    { alternativeId: 'a2', propertyId: 'p1', value: { lower: 4, modal: 5, upper: 6 } },
  ],
  constraints: [],
  config: {
    aggregation: 'max',
    weights: {
      positive: 0.3333333333333333,
      average: 0.3333333333333334,
      negative: 0.3333333333333333,
    },
  },
};

describe('loadScenario — valid base', () => {
  it('loads successfully', () => {
    const p = writeTempScenario(VALID_BASE);
    const scenario = loadScenario(p);
    expect(scenario.name).toBe('Test');
    expect(scenario.decisions.length).toBe(1);
    expect(scenario.alternatives.length).toBe(2);
    expect(scenario.coefficients[0].value).toBeInstanceOf(TriangularFuzzyM);
  });
});

describe('loadScenario — invariant 1.1: duplicate decision id', () => {
  it('throws ScenarioValidationError', () => {
    const invalid = {
      ...VALID_BASE,
      decisions: [
        { id: 'd1', name: 'D1' },
        { id: 'd1', name: 'D1 dup' },
      ],
      alternatives: [
        { id: 'a1', decisionId: 'd1', name: 'A1' },
        { id: 'a2', decisionId: 'd1', name: 'A2' },
      ],
    };
    const p = writeTempScenario(invalid);
    expect(() => loadScenario(p)).toThrow(ScenarioValidationError);
  });
});

describe('loadScenario — invariant 1.2: duplicate alternative id', () => {
  it('throws ScenarioValidationError', () => {
    const invalid = {
      ...VALID_BASE,
      alternatives: [
        { id: 'a1', decisionId: 'd1', name: 'A1' },
        { id: 'a1', decisionId: 'd1', name: 'A1 dup' },
      ],
    };
    const p = writeTempScenario(invalid);
    expect(() => loadScenario(p)).toThrow(ScenarioValidationError);
  });
});

describe('loadScenario — invariant 1.4: id namespace collision', () => {
  it('throws ScenarioValidationError when decision and alternative share id', () => {
    const invalid = {
      ...VALID_BASE,
      decisions: [{ id: 'shared', name: 'D' }],
      alternatives: [
        { id: 'shared', decisionId: 'shared', name: 'A' },
        { id: 'a2', decisionId: 'shared', name: 'A2' },
      ],
      coefficients: [
        { alternativeId: 'shared', propertyId: 'p1', value: { lower: 1, modal: 2, upper: 3 } },
        { alternativeId: 'a2', propertyId: 'p1', value: { lower: 1, modal: 2, upper: 3 } },
      ],
    };
    const p = writeTempScenario(invalid);
    expect(() => loadScenario(p)).toThrow(ScenarioValidationError);
  });
});

describe('loadScenario — invariant 2.1: unknown decisionId', () => {
  it('throws ScenarioValidationError', () => {
    const invalid = {
      ...VALID_BASE,
      alternatives: [
        { id: 'a1', decisionId: 'nonexistent', name: 'A1' },
        { id: 'a2', decisionId: 'd1', name: 'A2' },
      ],
    };
    const p = writeTempScenario(invalid);
    expect(() => loadScenario(p)).toThrow(ScenarioValidationError);
  });
});

describe('loadScenario — invariant 3.1: missing coefficient', () => {
  it('throws ScenarioValidationError when a coefficient is missing', () => {
    const invalid = {
      ...VALID_BASE,
      coefficients: [
        { alternativeId: 'a1', propertyId: 'p1', value: { lower: 1, modal: 2, upper: 3 } },
        // missing a2/p1
      ],
    };
    const p = writeTempScenario(invalid);
    expect(() => loadScenario(p)).toThrow(ScenarioValidationError);
  });
});

describe('loadScenario — invariant 3.1: duplicate coefficient', () => {
  it('throws ScenarioValidationError for duplicate (alternativeId, propertyId)', () => {
    const invalid = {
      ...VALID_BASE,
      coefficients: [
        { alternativeId: 'a1', propertyId: 'p1', value: { lower: 1, modal: 2, upper: 3 } },
        { alternativeId: 'a1', propertyId: 'p1', value: { lower: 1, modal: 2, upper: 3 } },
        { alternativeId: 'a2', propertyId: 'p1', value: { lower: 4, modal: 5, upper: 6 } },
      ],
    };
    const p = writeTempScenario(invalid);
    expect(() => loadScenario(p)).toThrow(ScenarioValidationError);
  });
});

describe('loadScenario — invariant 5.1: weights do not sum to 1', () => {
  it('throws ScenarioValidationError', () => {
    const invalid = {
      ...VALID_BASE,
      config: {
        aggregation: 'max',
        weights: { positive: 0.5, average: 0.5, negative: 0.5 },
      },
    };
    const p = writeTempScenario(invalid);
    expect(() => loadScenario(p)).toThrow(ScenarioValidationError);
  });
});

describe('loadScenario — invariant 7.1: self-edge dependency', () => {
  it('throws ScenarioValidationError', () => {
    const invalid = {
      ...VALID_BASE,
      constraints: [{ kind: 'dependency', sourceAlternativeId: 'a1', targetAlternativeId: 'a1' }],
    };
    const p = writeTempScenario(invalid);
    expect(() => loadScenario(p)).toThrow(ScenarioValidationError);
  });
});

describe('loadScenario — invariant 8.1: decision with no alternatives', () => {
  it('throws ScenarioValidationError', () => {
    const invalid = {
      ...VALID_BASE,
      decisions: [
        { id: 'd1', name: 'D1' },
        { id: 'd2', name: 'D2' },
      ],
      // d2 has no alternatives
    };
    const p = writeTempScenario(invalid);
    expect(() => loadScenario(p)).toThrow(ScenarioValidationError);
  });
});

describe('loadScenario — invariant 4.1: triangular ordering warning', () => {
  it('loads but emits warning when lower > modal', () => {
    const invalid = {
      ...VALID_BASE,
      coefficients: [
        { alternativeId: 'a1', propertyId: 'p1', value: { lower: 5, modal: 2, upper: 3 } },
        { alternativeId: 'a2', propertyId: 'p1', value: { lower: 4, modal: 5, upper: 6 } },
      ],
    };
    const p = writeTempScenario(invalid);
    const scenario = loadScenario(p);
    expect(scenario.warnings.length).toBeGreaterThan(0);
    expect(scenario.warnings[0]).toContain('Invariant 4.1');
  });
});
