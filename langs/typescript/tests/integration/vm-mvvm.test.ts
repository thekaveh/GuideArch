/**
 * vm-mvvm.test.ts
 *
 * Headless MVVM integration tests for ScenarioVM.
 * These tests do NOT mount Svelte components — they exercise only the VM layer.
 *
 * Tests verify:
 *  1. _browserOpen loads SAS sample and produces 720 candidates with expected top score.
 *  2. Editing a property weight triggers re-solve and changes top score.
 *  3. Add decision + alternative + property, then save + reload preserves all three.
 *  4. deleteDecision cascades to its alternatives and their coefficients.
 *  5. solve-trigger fires only for relevant fields (name change does NOT re-solve).
 */
import { describe, it, expect } from 'vitest';
import path from 'path';
import fs from 'fs';
import os from 'os';
import { fileURLToPath } from 'url';
import { makeScenarioVm } from '../../src/viewmodels/scenario-vm.js';

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

// Load raw SAS JSON once for _browserOpen tests
const sasRaw = JSON.parse(fs.readFileSync(SAS_JSON, 'utf-8')) as unknown;

// Load the bundled schema (same one used by the VM) for _browserOpen
const schemaPath = path.join(ROOT, 'langs', 'typescript', 'src', 'samples', 'scenario.schema.json');
const inlineSchema = JSON.parse(fs.readFileSync(schemaPath, 'utf-8')) as object;

import { loadScenarioFromParsed } from '../../src/models/scenario-loader.js';

// ─────────────────────────────────────────────────────────────────────────────
// 1. _browserOpen produces 720 candidates with correct top score
// ─────────────────────────────────────────────────────────────────────────────

describe('_browserOpen — SAS sample', () => {
  it('produces 720 candidates', () => {
    const vm = makeScenarioVm();
    vm._browserOpen(sasRaw, 'sas.json');
    expect(vm.model.candidates.length).toBe(720);
  });

  it('top candidate score matches expected 0.031180695179944085', () => {
    const vm = makeScenarioVm();
    vm._browserOpen(sasRaw, 'sas.json');
    const rank0 = vm.model.candidates[0];
    expect(rank0).toBeDefined();
    expect(rank0.rank).toBe(0);
    expect(Math.abs(rank0.score - 0.031180695179944085)).toBeLessThanOrEqual(1e-9);
  });

  it('sets filePath to the given label', () => {
    const vm = makeScenarioVm();
    vm._browserOpen(sasRaw, 'sas.json');
    expect(vm.model.filePath).toBe('sas.json');
  });

  it('clears isDirty', () => {
    const vm = makeScenarioVm();
    vm._browserOpen(sasRaw, 'sas.json');
    expect(vm.model.isDirty).toBe(false);
  });
});

// ─────────────────────────────────────────────────────────────────────────────
// 2. Editing a property weight triggers re-solve and changes top score
// ─────────────────────────────────────────────────────────────────────────────

describe('property weight edit triggers re-solve', () => {
  it('top score changes after updatePropertyWeight', () => {
    const vm = makeScenarioVm();
    vm._browserOpen(sasRaw, 'sas.json');
    const initialScore = vm.model.candidates[0]?.score;
    expect(initialScore).toBeDefined();

    const propId = vm.model.scenario!.properties[0].id;
    vm.updatePropertyWeight(propId, 99);

    const newScore = vm.model.candidates[0]?.score;
    expect(newScore).not.toBe(initialScore);
  });

  it('candidate count stays the same after weight edit (no structural change)', () => {
    const vm = makeScenarioVm();
    vm._browserOpen(sasRaw, 'sas.json');
    const countBefore = vm.model.candidates.length;

    const propId = vm.model.scenario!.properties[0].id;
    vm.updatePropertyWeight(propId, 5);

    expect(vm.model.candidates.length).toBe(countBefore);
  });
});

// ─────────────────────────────────────────────────────────────────────────────
// 3. Add decision, alternative, property, save, reload → all three preserved
// ─────────────────────────────────────────────────────────────────────────────

describe('add decision + alternative + property, save and reload preserves all three', () => {
  it('roundtrip via saveAsCmd + loadScenario', () => {
    const vm = makeScenarioVm();
    // Start from a minimal fresh scenario so counts are predictable
    vm.newCmd.execute();
    // Add decision
    vm.addDecision('Roundtrip Decision');
    const decId = vm.model.scenario!.decisions.at(-1)!.id;

    // Add alternative under that decision
    vm.addAlternative(decId, 'Roundtrip Alternative');
    const altId = vm.model.scenario!.alternatives.at(-1)!.id;

    // Add property
    vm.addProperty('Roundtrip Property');
    const propId = vm.model.scenario!.properties.at(-1)!.id;

    // Save to temp file
    const tmpFile = path.join(
      os.tmpdir(),
      `guidearch-mvvm-${Date.now()}-${Math.random().toString(36).slice(2)}.json`,
    );
    vm.saveAsCmd.execute(tmpFile);
    expect(fs.existsSync(tmpFile)).toBe(true);

    // Reload via file path (Node path — tests run in Node)
    const vm2 = makeScenarioVm();
    vm2.openCmd.execute(tmpFile);
    const s2 = vm2.model.scenario!;

    expect(s2.decisions.some((d) => d.id === decId && d.name === 'Roundtrip Decision')).toBe(true);
    expect(s2.alternatives.some((a) => a.id === altId && a.name === 'Roundtrip Alternative')).toBe(
      true,
    );
    expect(s2.properties.some((p) => p.id === propId && p.name === 'Roundtrip Property')).toBe(
      true,
    );

    // Clean up
    try {
      fs.unlinkSync(tmpFile);
    } catch {
      // ignore
    }
  });
});

// ─────────────────────────────────────────────────────────────────────────────
// 4. deleteDecision cascades to its alternatives and their coefficients
// ─────────────────────────────────────────────────────────────────────────────

describe('deleteDecision cascade', () => {
  it('removes the decision, its alternatives, and their coefficients', () => {
    const vm = makeScenarioVm();
    vm._browserOpen(sasRaw, 'sas.json');
    const s = vm.model.scenario!;

    const targetDecId = s.decisions[0].id;
    const targetAltIds = s.alternatives
      .filter((a) => a.decisionId === targetDecId)
      .map((a) => a.id);
    expect(targetAltIds.length).toBeGreaterThan(0);

    vm.deleteDecision(targetDecId);
    const s2 = vm.model.scenario!;

    expect(s2.decisions.find((d) => d.id === targetDecId)).toBeUndefined();

    for (const altId of targetAltIds) {
      expect(s2.alternatives.find((a) => a.id === altId)).toBeUndefined();
      const leftoverCoeffs = s2.coefficients.filter((c) => c.alternativeId === altId);
      expect(leftoverCoeffs).toHaveLength(0);
    }
  });

  it('constraints referencing deleted alternatives are removed', () => {
    const vm = makeScenarioVm();
    vm._browserOpen(sasRaw, 'sas.json');
    const s = vm.model.scenario!;
    const targetDecId = s.decisions[0].id;
    const targetAltIds = new Set(
      s.alternatives.filter((a) => a.decisionId === targetDecId).map((a) => a.id),
    );

    vm.deleteDecision(targetDecId);
    const s2 = vm.model.scenario!;

    for (const c of s2.constraints) {
      if (c.kind === 'dependency') {
        expect(targetAltIds.has(c.sourceAlternativeId)).toBe(false);
        expect(targetAltIds.has(c.targetAlternativeId)).toBe(false);
      } else if (c.kind === 'conflict') {
        expect(targetAltIds.has(c.alternativeAId)).toBe(false);
        expect(targetAltIds.has(c.alternativeBId)).toBe(false);
      }
    }
  });
});

// ─────────────────────────────────────────────────────────────────────────────
// 5. solve-trigger fires only for relevant fields (name change does NOT re-solve)
// ─────────────────────────────────────────────────────────────────────────────

describe('solve-trigger selectivity', () => {
  it('changing scenario.name does NOT re-solve (candidate list unchanged)', () => {
    const vm = makeScenarioVm();
    vm._browserOpen(sasRaw, 'sas.json');
    const candidatesBefore = vm.model.candidates;

    vm.updateScenarioName('Changed Name');

    // Exact same array reference means solve was not called
    expect(vm.model.candidates).toBe(candidatesBefore);
    expect(vm.model.scenario!.name).toBe('Changed Name');
  });

  it('changing a decision name does NOT re-solve', () => {
    const vm = makeScenarioVm();
    vm._browserOpen(sasRaw, 'sas.json');
    const candidatesBefore = vm.model.candidates;
    const decId = vm.model.scenario!.decisions[0].id;

    vm.updateDecisionName(decId, 'New Name');

    expect(vm.model.candidates).toBe(candidatesBefore);
  });

  it('changing a property weight DOES re-solve (new array reference)', () => {
    const vm = makeScenarioVm();
    vm._browserOpen(sasRaw, 'sas.json');
    const candidatesBefore = vm.model.candidates;
    const propId = vm.model.scenario!.properties[0].id;

    vm.updatePropertyWeight(propId, 42);

    expect(vm.model.candidates).not.toBe(candidatesBefore);
  });

  it('loadScenarioFromParsed with inline schema loads correctly', () => {
    const scenario = loadScenarioFromParsed(sasRaw, inlineSchema);
    expect(scenario.decisions.length).toBeGreaterThan(0);
    expect(scenario.name).toBe('SAS');
  });
});
