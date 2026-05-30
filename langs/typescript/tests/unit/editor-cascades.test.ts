/**
 * editor-cascades.test.ts
 *
 * Verifies that delete operations cascade correctly:
 * - deleteDecision removes the decision's alternatives, their coefficients,
 *   and constraints referencing those alternatives.
 * - deleteAlternative removes its coefficients and referencing constraints.
 * - deleteProperty removes its coefficients and threshold constraints.
 *
 * All tests load sas.json as the starting scenario.
 */
import { describe, it, expect, beforeEach } from 'vitest';
import path from 'path';
import fs from 'fs';
import { fileURLToPath } from 'url';
import { makeScenarioVm, ScenarioMutationError } from '../../src/viewmodels/scenario-vm.js';
import type { ScenarioVM } from '../../src/viewmodels/scenario-vm.js';

const __dirname = path.dirname(fileURLToPath(import.meta.url));

function repoRoot(): string {
  let dir = __dirname;
  while (true) {
    if (fs.existsSync(path.join(dir, '.git'))) return dir;
    const parent = path.dirname(dir);
    if (parent === dir) throw new Error('repo root not found');
    dir = parent;
  }
}

const ROOT = repoRoot();
const SAS_JSON = path.join(ROOT, 'spec', 'conformance', 'scenarios', 'sas.json');

function loadVm(): ScenarioVM {
  const vm = makeScenarioVm();
  vm.openCmd.execute(SAS_JSON);
  return vm;
}

// ---------------------------------------------------------------------------
// deleteDecision cascade
// ---------------------------------------------------------------------------

describe('deleteDecision cascade', () => {
  let vm: ScenarioVM;
  let decisionId: string;
  let altIdsForDecision: string[];

  beforeEach(() => {
    vm = loadVm();
    const s = vm.model.scenario!;
    // Pick the first decision
    decisionId = s.decisions[0].id;
    altIdsForDecision = s.alternatives.filter((a) => a.decisionId === decisionId).map((a) => a.id);
    expect(altIdsForDecision.length).toBeGreaterThan(0);
    vm.deleteDecision(decisionId);
  });

  it('removes the decision', () => {
    const s = vm.model.scenario!;
    expect(s.decisions.find((d) => d.id === decisionId)).toBeUndefined();
  });

  it('removes alternatives belonging to the decision', () => {
    const s = vm.model.scenario!;
    for (const altId of altIdsForDecision) {
      expect(s.alternatives.find((a) => a.id === altId)).toBeUndefined();
    }
  });

  it('removes coefficients for those alternatives', () => {
    const s = vm.model.scenario!;
    for (const altId of altIdsForDecision) {
      const leftover = s.coefficients.filter((c) => c.alternativeId === altId);
      expect(leftover).toHaveLength(0);
    }
  });

  it('removes dependency/conflict constraints referencing those alternatives', () => {
    const s = vm.model.scenario!;
    const removedSet = new Set(altIdsForDecision);
    for (const c of s.constraints) {
      if (c.kind === 'dependency') {
        expect(removedSet.has(c.sourceAlternativeId)).toBe(false);
        expect(removedSet.has(c.targetAlternativeId)).toBe(false);
      } else if (c.kind === 'conflict') {
        expect(removedSet.has(c.alternativeAId)).toBe(false);
        expect(removedSet.has(c.alternativeBId)).toBe(false);
      }
    }
  });

  it('throws ScenarioMutationError for unknown id', () => {
    expect(() => vm.deleteDecision('does-not-exist')).toThrow(ScenarioMutationError);
  });
});

// ---------------------------------------------------------------------------
// deleteAlternative cascade
// ---------------------------------------------------------------------------

describe('deleteAlternative cascade', () => {
  let vm: ScenarioVM;
  let altId: string;

  beforeEach(() => {
    vm = loadVm();
    const s = vm.model.scenario!;
    // Pick an alternative that has coefficients
    altId = s.alternatives[0].id;
    vm.deleteAlternative(altId);
  });

  it('removes the alternative', () => {
    expect(vm.model.scenario!.alternatives.find((a) => a.id === altId)).toBeUndefined();
  });

  it('removes coefficients for that alternative', () => {
    const leftover = vm.model.scenario!.coefficients.filter((c) => c.alternativeId === altId);
    expect(leftover).toHaveLength(0);
  });

  it('removes dependency/conflict constraints referencing the alternative', () => {
    for (const c of vm.model.scenario!.constraints) {
      if (c.kind === 'dependency') {
        expect(c.sourceAlternativeId).not.toBe(altId);
        expect(c.targetAlternativeId).not.toBe(altId);
      } else if (c.kind === 'conflict') {
        expect(c.alternativeAId).not.toBe(altId);
        expect(c.alternativeBId).not.toBe(altId);
      }
    }
  });

  it('throws ScenarioMutationError for unknown id', () => {
    expect(() => vm.deleteAlternative('no-such-alt')).toThrow(ScenarioMutationError);
  });
});

// ---------------------------------------------------------------------------
// deleteProperty cascade
// ---------------------------------------------------------------------------

describe('deleteProperty cascade', () => {
  let vm: ScenarioVM;
  let propId: string;

  beforeEach(() => {
    vm = loadVm();
    const s = vm.model.scenario!;
    propId = s.properties[0].id;
    vm.deleteProperty(propId);
  });

  it('removes the property', () => {
    expect(vm.model.scenario!.properties.find((p) => p.id === propId)).toBeUndefined();
  });

  it('removes coefficients for that property', () => {
    const leftover = vm.model.scenario!.coefficients.filter((c) => c.propertyId === propId);
    expect(leftover).toHaveLength(0);
  });

  it('removes threshold constraints referencing the property', () => {
    for (const c of vm.model.scenario!.constraints) {
      if (c.kind === 'threshold') {
        expect(c.propertyId).not.toBe(propId);
      }
    }
  });

  it('throws ScenarioMutationError for unknown id', () => {
    expect(() => vm.deleteProperty('no-such-prop')).toThrow(ScenarioMutationError);
  });
});

// ---------------------------------------------------------------------------
// addDecision / addAlternative / addProperty maintain coefficient completeness
// ---------------------------------------------------------------------------

describe('addAlternative fills coefficients for existing properties', () => {
  it('adds zero-fuzzy coefficients for every property', () => {
    const vm = loadVm();
    const s = vm.model.scenario!;
    const decisionId = s.decisions[0].id;
    const propCount = s.properties.length;
    const prevAltCount = s.alternatives.length;

    vm.addAlternative(decisionId, 'Test Alt');

    const newS = vm.model.scenario!;
    expect(newS.alternatives.length).toBe(prevAltCount + 1);
    const newAlt = newS.alternatives[newS.alternatives.length - 1];
    const newCoeffs = newS.coefficients.filter((c) => c.alternativeId === newAlt.id);
    expect(newCoeffs.length).toBe(propCount);
    for (const coeff of newCoeffs) {
      expect(coeff.value.lower).toBe(0);
      expect(coeff.value.modal).toBe(0);
      expect(coeff.value.upper).toBe(0);
    }
  });
});

describe('addProperty fills coefficients for existing alternatives', () => {
  it('adds zero-fuzzy coefficients for every alternative', () => {
    const vm = loadVm();
    const s = vm.model.scenario!;
    const altCount = s.alternatives.length;
    const prevPropCount = s.properties.length;

    vm.addProperty('New Prop');

    const newS = vm.model.scenario!;
    expect(newS.properties.length).toBe(prevPropCount + 1);
    const newProp = newS.properties[newS.properties.length - 1];
    const newCoeffs = newS.coefficients.filter((c) => c.propertyId === newProp.id);
    expect(newCoeffs.length).toBe(altCount);
    for (const coeff of newCoeffs) {
      expect(coeff.value.lower).toBe(0);
      expect(coeff.value.modal).toBe(0);
      expect(coeff.value.upper).toBe(0);
    }
  });
});

// ---------------------------------------------------------------------------
// ScenarioMutationError on invalid weight
// ---------------------------------------------------------------------------

describe('updatePropertyWeight validation', () => {
  it('throws ScenarioMutationError when weight <= 0', () => {
    const vm = loadVm();
    const propId = vm.model.scenario!.properties[0].id;
    expect(() => vm.updatePropertyWeight(propId, 0)).toThrow(ScenarioMutationError);
    expect(() => vm.updatePropertyWeight(propId, -1)).toThrow(ScenarioMutationError);
  });

  it('updates weight and triggers re-solve when valid', () => {
    const vm = loadVm();
    const s = vm.model.scenario!;
    const propId = s.properties[0].id;
    const prevScore = vm.model.candidates[0]?.score;
    vm.updatePropertyWeight(propId, 99);
    // Score should have changed after re-solve
    const newScore = vm.model.candidates[0]?.score;
    expect(newScore).not.toBe(prevScore);
  });
});
