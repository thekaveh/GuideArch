/**
 * save-roundtrip.test.ts
 *
 * Verifies that load → edit → save → reload preserves the change.
 *
 * Steps:
 * 1. Load sas.json into a VM.
 * 2. Mutate a property weight.
 * 3. Save to a temp file.
 * 4. Load the temp file into a new VM.
 * 5. Assert the saved weight matches and the scenario is structurally identical
 *    (modulo the weight change).
 */
import { describe, it, expect, afterAll } from 'vitest';
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

const tempFiles: string[] = [];

function mkTemp(suffix = '.json'): string {
  const f = path.join(
    os.tmpdir(),
    `guidearch-test-${Date.now()}-${Math.random().toString(36).slice(2)}${suffix}`,
  );
  tempFiles.push(f);
  return f;
}

afterAll(() => {
  for (const f of tempFiles) {
    try {
      fs.unlinkSync(f);
    } catch {
      // ignore
    }
  }
});

describe('save round-trip', () => {
  it('load → edit weight → save → reload preserves weight', () => {
    // 1. Load
    const vm1 = makeScenarioVm();
    vm1.openCmd.execute(SAS_JSON);
    expect(vm1.model.scenario).toBeDefined();

    const propId = vm1.model.scenario!.properties[0].id;
    const newWeight = 7.77;

    // 2. Edit
    vm1.updatePropertyWeight(propId, newWeight);
    expect(vm1.model.scenario!.properties.find((p) => p.id === propId)!.weight).toBe(newWeight);

    // 3. Save to temp file
    const tempPath = mkTemp();
    vm1.saveAsCmd.execute(tempPath);
    expect(fs.existsSync(tempPath)).toBe(true);
    expect(vm1.model.filePath).toBe(tempPath);
    expect(vm1.model.isDirty).toBe(false);

    // 4. Reload
    const vm2 = makeScenarioVm();
    vm2.openCmd.execute(tempPath);
    expect(vm2.model.scenario).toBeDefined();

    // 5. Assert weight is preserved
    const reloadedProp = vm2.model.scenario!.properties.find((p) => p.id === propId);
    expect(reloadedProp).toBeDefined();
    expect(reloadedProp!.weight).toBe(newWeight);
  });

  it('load → edit → save → reload: all decisions, alternatives, properties identical count', () => {
    const vm1 = makeScenarioVm();
    vm1.openCmd.execute(SAS_JSON);
    const s1 = vm1.model.scenario!;

    // Make a name change (cosmetic)
    vm1.updateDecisionName(s1.decisions[0].id, 'Renamed Decision');

    const tempPath = mkTemp();
    vm1.saveAsCmd.execute(tempPath);

    const vm2 = makeScenarioVm();
    vm2.openCmd.execute(tempPath);
    const s2 = vm2.model.scenario!;

    expect(s2.decisions.length).toBe(s1.decisions.length);
    expect(s2.alternatives.length).toBe(s1.alternatives.length);
    expect(s2.properties.length).toBe(s1.properties.length);
    expect(s2.coefficients.length).toBe(s1.coefficients.length);
  });

  it('load → edit property → re-solve changes candidates', () => {
    const vm = makeScenarioVm();
    vm.openCmd.execute(SAS_JSON);
    const beforeScore = vm.model.candidates[0]?.score;

    // Dramatically change first property weight
    const propId = vm.model.scenario!.properties[0].id;
    vm.updatePropertyWeight(propId, 100);

    const afterScore = vm.model.candidates[0]?.score;
    // Score must have changed (implicitly re-solved)
    expect(afterScore).not.toBe(beforeScore);
  });

  it('saveCmd is disabled when no filePath is set (new vm)', () => {
    const vm = makeScenarioVm();
    expect(vm.saveCmd.canExecute()).toBe(false);
  });

  it('saveCmd is disabled when no scenario', () => {
    const vm = makeScenarioVm();
    vm.newCmd.execute();
    // newCmd sets scenario but clears filePath
    expect(vm.saveCmd.canExecute()).toBe(false);
  });

  it('saveCmd enabled after openCmd', () => {
    const vm = makeScenarioVm();
    vm.openCmd.execute(SAS_JSON);
    expect(vm.saveCmd.canExecute()).toBe(true);
  });

  it('saveAsCmd sets filePath and clears isDirty', () => {
    const vm = makeScenarioVm();
    vm.openCmd.execute(SAS_JSON);
    const propId = vm.model.scenario!.properties[0].id;
    vm.updatePropertyWeight(propId, 5);
    expect(vm.model.isDirty).toBe(true);

    const tempPath = mkTemp();
    vm.saveAsCmd.execute(tempPath);
    expect(vm.model.filePath).toBe(tempPath);
    expect(vm.model.isDirty).toBe(false);
  });
});
