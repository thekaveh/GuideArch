/**
 * Integration test: ScenarioVM loads sas.json, runs solve, and matches the
 * M1 baseline (spec/conformance/expected/sas.candidates.json rank 0 score).
 */
import { describe, it, expect } from 'vitest';
import path from 'path';
import fs from 'fs';
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
const SAS_EXPECTED = path.join(ROOT, 'spec', 'conformance', 'expected', 'sas.candidates.json');

describe('ScenarioVM — sas.json integration', () => {
  it('openCmd sets candidates', () => {
    const vm = makeScenarioVm();
    expect(vm.model.candidates.length).toBe(0);

    vm.openCmd.execute(SAS_JSON);

    expect(vm.model.candidates.length).toBeGreaterThan(0);
  });

  it('rank 0 score matches M1 baseline within 1e-9', () => {
    const vm = makeScenarioVm();
    vm.openCmd.execute(SAS_JSON);

    const rank0 = vm.model.candidates[0];
    expect(rank0).toBeDefined();
    expect(rank0.rank).toBe(0);

    const expected = JSON.parse(fs.readFileSync(SAS_EXPECTED, 'utf-8')) as Array<{
      score: number;
      rank: number;
    }>;
    const expectedRank0 = expected.find((c) => c.rank === 0);
    expect(expectedRank0).toBeDefined();

    const diff = Math.abs(rank0.score - expectedRank0!.score);
    expect(diff).toBeLessThanOrEqual(1e-9);
  });

  it('status contains candidate count after open', () => {
    const vm = makeScenarioVm();
    vm.openCmd.execute(SAS_JSON);
    expect(vm.model.status).toContain('Solved:');
  });

  it('filePath is set after openCmd', () => {
    const vm = makeScenarioVm();
    vm.openCmd.execute(SAS_JSON);
    expect(vm.model.filePath).toBe(SAS_JSON);
  });

  it('isDirty is false after openCmd', () => {
    const vm = makeScenarioVm();
    vm.openCmd.execute(SAS_JSON);
    expect(vm.model.isDirty).toBe(false);
  });

  it('newCmd resets to empty state', () => {
    const vm = makeScenarioVm();
    vm.openCmd.execute(SAS_JSON);
    expect(vm.model.candidates.length).toBeGreaterThan(0);

    vm.newCmd.execute();
    expect(vm.model.filePath).toBeUndefined();
    expect(vm.model.isDirty).toBe(false);
    expect(vm.model.candidates.length).toBe(0);
  });

  it('solveCmd re-runs solve', () => {
    const vm = makeScenarioVm();
    vm.openCmd.execute(SAS_JSON);
    const initialCount = vm.model.candidates.length;

    vm.solveCmd.execute();
    expect(vm.model.candidates.length).toBe(initialCount);
  });

  it('saveCmd is disabled when no filePath', () => {
    const vm = makeScenarioVm();
    expect(vm.saveCmd.canExecute()).toBe(false);
  });

  describe('_browserMarkSaved (browser out-of-band save hook)', () => {
    it('clears isDirty and sets filePath when a path is given', () => {
      const vm = makeScenarioVm();
      vm.openCmd.execute(SAS_JSON);
      vm.addDecision('Dirtying edit');
      expect(vm.model.isDirty).toBe(true);

      vm._browserMarkSaved('/downloads/sas-copy.json');

      expect(vm.model.isDirty).toBe(false);
      expect(vm.model.filePath).toBe('/downloads/sas-copy.json');
    });

    it('clears isDirty but leaves filePath unchanged when no path is given', () => {
      const vm = makeScenarioVm();
      vm.openCmd.execute(SAS_JSON);
      vm.addDecision('Dirtying edit');
      expect(vm.model.isDirty).toBe(true);

      vm._browserMarkSaved();

      expect(vm.model.isDirty).toBe(false);
      expect(vm.model.filePath).toBe(SAS_JSON);
    });
  });
});
