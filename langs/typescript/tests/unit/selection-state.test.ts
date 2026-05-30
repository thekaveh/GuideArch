/**
 * Tests: selectedCandidateIndex in ScenarioState is observable and defaults correctly.
 */
import { describe, it, expect } from 'vitest';
import { makeScenarioVm } from '../../src/viewmodels/scenario-vm.js';
import path from 'path';
import fs from 'fs';
import { fileURLToPath } from 'url';

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

describe('selectedCandidateIndex — initial state', () => {
  it('is null before any scenario is loaded', () => {
    const vm = makeScenarioVm();
    expect(vm.model.selectedCandidateIndex).toBeNull();
  });

  it('is null after newCmd (no candidates)', () => {
    const vm = makeScenarioVm();
    vm.newCmd.execute();
    expect(vm.model.selectedCandidateIndex).toBeNull();
  });
});

describe('selectedCandidateIndex — after openCmd', () => {
  it('is 0 after opening sas.json (candidates exist)', () => {
    const vm = makeScenarioVm();
    vm.openCmd.execute(SAS_JSON);
    expect(vm.model.candidates.length).toBeGreaterThan(0);
    expect(vm.model.selectedCandidateIndex).toBe(0);
  });
});

describe('selectedCandidateIndex — setSelectedCandidateIndex', () => {
  it('can be changed to a valid index', () => {
    const vm = makeScenarioVm();
    vm.openCmd.execute(SAS_JSON);
    vm.setSelectedCandidateIndex(1);
    expect(vm.model.selectedCandidateIndex).toBe(1);
  });

  it('can be set to null to deselect', () => {
    const vm = makeScenarioVm();
    vm.openCmd.execute(SAS_JSON);
    vm.setSelectedCandidateIndex(null);
    expect(vm.model.selectedCandidateIndex).toBeNull();
  });

  it('throws for out-of-range positive index', () => {
    const vm = makeScenarioVm();
    vm.openCmd.execute(SAS_JSON);
    const outOfRange = vm.model.candidates.length + 100;
    expect(() => vm.setSelectedCandidateIndex(outOfRange)).toThrow();
  });

  it('propagates observable change to subscribers', () => {
    const vm = makeScenarioVm();
    vm.openCmd.execute(SAS_JSON);

    const observed: Array<number | null> = [];
    const sub = vm.propertyChanged.subscribe((name) => {
      if (name === 'model') {
        observed.push(vm.model.selectedCandidateIndex);
      }
    });

    vm.setSelectedCandidateIndex(2);
    vm.setSelectedCandidateIndex(null);
    sub.unsubscribe();

    expect(observed).toContain(2);
    expect(observed).toContain(null);
  });

  it('resets to 0 after solveCmd re-runs', () => {
    const vm = makeScenarioVm();
    vm.openCmd.execute(SAS_JSON);
    vm.setSelectedCandidateIndex(3);
    vm.solveCmd.execute();
    // After solve, selectedCandidateIndex resets to 0
    expect(vm.model.selectedCandidateIndex).toBe(0);
  });
});
