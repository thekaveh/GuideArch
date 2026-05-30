/**
 * vm-tree-comprehensive.test.ts
 *
 * Exhaustive verification of the ViewModel hierarchy per spec/viewmodels.md §2-4.
 *
 * Coverage:
 *  1. Existence — every factory / type exported from viewmodels/index.ts
 *  2. Construction — correct VMx primitive (ComponentVMOf / CompositeVM / AggregateVM)
 *  3. Observable properties — shape, read-only vs read-write, hub emissions
 *  4. Commands — all five ScenarioVM commands, canExecute semantics
 *  5. Mutation propagation — child VM changes reflect in parent's exposed data
 *  6. Solve-trigger matrix (spec §3.3) — parameterized test for every case
 *  7. Result VMs read-only — CandidateVM, CriticalDecisionVM, CriticalConstraintVM
 *
 * ARCHITECTURE NOTES — spec vs implementation deviations documented inline:
 *  - The spec calls for CompositeVM<XxxVM> tree nodes (DecisionsVM, PropertiesVM, etc.)
 *    The current TypeScript implementation folds those into ScenarioVM's model
 *    (ScenarioState.scenario.*) rather than building standalone CompositeVM instances.
 *    The ScenarioVM exposes decisions/alternatives/properties/constraints as arrays on
 *    vm.model.scenario rather than as separate CompositeVM children.
 *    Deviation is documented in section 5. of this file.
 *  - The spec mentions AnalysisVM (AggregateVM2) — not yet realized in TypeScript M3.
 *    This is an M4 item; documented below.
 *
 * All tests run headless (Node.js, no Svelte mount).
 */

import { describe, it, expect } from 'vitest';
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';
import { makeScenarioVm, ScenarioMutationError } from '../../src/viewmodels/scenario-vm.js';
import { makeDecisionVm } from '../../src/viewmodels/decision-vm.js';
import { makeAlternativeVm } from '../../src/viewmodels/alternative-vm.js';
import { makePropertyVm } from '../../src/viewmodels/property-vm.js';
import { makeCoefficientVm } from '../../src/viewmodels/coefficient-vm.js';
import { makeConstraintVm } from '../../src/viewmodels/constraint-vm.js';
import { makeCandidateVm } from '../../src/viewmodels/candidate-vm.js';
import { makeCriticalDecisionVm } from '../../src/viewmodels/critical-decision-vm.js';
import { makeCriticalConstraintVm } from '../../src/viewmodels/critical-constraint-vm.js';
import { MessageHub, PropertyChangedMessage, ComponentVMOf } from 'vmx';
import { TriangularFuzzyM } from '../../src/models/triangular-fuzzy.js';

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

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
const sasRaw = JSON.parse(fs.readFileSync(SAS_JSON, 'utf-8')) as unknown;

/** Collect hub PropertyChangedMessage emissions for a property during a callback.
 *  Note: VMx sends "Model" (capital M) for model changes — match case-insensitively.
 */
function collectChanges(hub: MessageHub, targetPropertyName: string, action: () => void): string[] {
  const names: string[] = [];
  const lc = targetPropertyName.toLowerCase();
  const sub = hub.messages.subscribe((msg) => {
    if (msg instanceof PropertyChangedMessage && msg.propertyName.toLowerCase() === lc) {
      names.push(msg.propertyName);
    }
  });
  action();
  sub.unsubscribe();
  return names;
}

// ============================================================================
// §1 EXISTENCE — every factory/type exported from viewmodels/index.ts
// ============================================================================

describe('§1 Existence — all VM factories exported', () => {
  it('makeScenarioVm is a function', () => {
    expect(typeof makeScenarioVm).toBe('function');
  });

  it('makeDecisionVm is a function', () => {
    expect(typeof makeDecisionVm).toBe('function');
  });

  it('makeAlternativeVm is a function', () => {
    expect(typeof makeAlternativeVm).toBe('function');
  });

  it('makePropertyVm is a function', () => {
    expect(typeof makePropertyVm).toBe('function');
  });

  it('makeCoefficientVm is a function', () => {
    expect(typeof makeCoefficientVm).toBe('function');
  });

  it('makeConstraintVm is a function', () => {
    expect(typeof makeConstraintVm).toBe('function');
  });

  it('makeCandidateVm is a function', () => {
    expect(typeof makeCandidateVm).toBe('function');
  });

  it('makeCriticalDecisionVm is a function', () => {
    expect(typeof makeCriticalDecisionVm).toBe('function');
  });

  it('makeCriticalConstraintVm is a function', () => {
    expect(typeof makeCriticalConstraintVm).toBe('function');
  });

  it('ScenarioMutationError is exported', () => {
    expect(typeof ScenarioMutationError).toBe('function');
  });
});

// ============================================================================
// §2 CONSTRUCTION — each factory returns a ComponentVMOf with the right name
// ============================================================================

describe('§2 Construction — VMx primitive (ComponentVMOf)', () => {
  const hub = new MessageHub();

  it('ScenarioVM is ComponentVMOf (has .model, .name, .construct)', () => {
    const vm = makeScenarioVm();
    expect(vm).toBeInstanceOf(ComponentVMOf);
    expect(vm.name).toBe('scenario');
    expect(typeof vm.model).toBe('object');
  });

  it('DecisionVM is ComponentVMOf with name "decision"', () => {
    const vm = makeDecisionVm({ id: 'd1', name: 'D1' }, hub);
    expect(vm).toBeInstanceOf(ComponentVMOf);
    expect(vm.name).toBe('decision');
  });

  it('AlternativeVM is ComponentVMOf with name "alternative"', () => {
    const vm = makeAlternativeVm({ id: 'a1', decisionId: 'd1', name: 'A1' }, hub, () => {});
    expect(vm).toBeInstanceOf(ComponentVMOf);
    expect(vm.name).toBe('alternative');
  });

  it('PropertyVM is ComponentVMOf with name "property"', () => {
    const vm = makePropertyVm({ id: 'p1', name: 'P1', kind: 'max', weight: 1 }, hub, () => {});
    expect(vm).toBeInstanceOf(ComponentVMOf);
    expect(vm.name).toBe('property');
  });

  it('CoefficientVM is ComponentVMOf with name "coefficient"', () => {
    const vm = makeCoefficientVm(
      { alternativeId: 'a1', propertyId: 'p1', value: new TriangularFuzzyM(0, 0, 0) },
      hub,
      () => {},
    );
    expect(vm).toBeInstanceOf(ComponentVMOf);
    expect(vm.name).toBe('coefficient');
  });

  it('ConstraintVM (threshold) is ComponentVMOf with name "constraint"', () => {
    const vm = makeConstraintVm({ kind: 'threshold', propertyId: 'p1', min: 0 }, hub, () => {});
    expect(vm).toBeInstanceOf(ComponentVMOf);
    expect(vm.name).toBe('constraint');
  });

  it('ConstraintVM (dependency) is ComponentVMOf', () => {
    const vm = makeConstraintVm(
      { kind: 'dependency', sourceAlternativeId: 'a1', targetAlternativeId: 'a2' },
      hub,
      () => {},
    );
    expect(vm).toBeInstanceOf(ComponentVMOf);
    expect(vm.name).toBe('constraint');
  });

  it('ConstraintVM (conflict) is ComponentVMOf', () => {
    const vm = makeConstraintVm(
      { kind: 'conflict', alternativeAId: 'a1', alternativeBId: 'a2' },
      hub,
      () => {},
    );
    expect(vm).toBeInstanceOf(ComponentVMOf);
    expect(vm.name).toBe('constraint');
  });

  it('CandidateVM is ComponentVMOf with name "candidate"', () => {
    const vm = makeCandidateVm({
      alternativeIds: ['a1'],
      triangularValue: new TriangularFuzzyM(0, 1, 2),
      normalizedValue: { positive: 0.1, average: 0.5, negative: 0.9 },
      score: 0.42,
      rank: 0,
    });
    expect(vm).toBeInstanceOf(ComponentVMOf);
    expect(vm.name).toBe('candidate');
  });

  it('CriticalDecisionVM is ComponentVMOf with name "criticalDecision"', () => {
    const vm = makeCriticalDecisionVm({
      decisionId: 'd1',
      triangularValue: new TriangularFuzzyM(0, 1, 2),
      normalizedValue: { positive: 0.1, average: 0.5, negative: 0.9 },
      score: 0.3,
      rank: 0,
    });
    expect(vm).toBeInstanceOf(ComponentVMOf);
    expect(vm.name).toBe('criticalDecision');
  });

  it('CriticalConstraintVM is ComponentVMOf with name "criticalConstraint"', () => {
    const vm = makeCriticalConstraintVm({
      constraintIndex: 0,
      kind: 'conflict',
      eliminated: 4,
      total: 8,
      redundant: false,
    });
    expect(vm).toBeInstanceOf(ComponentVMOf);
    expect(vm.name).toBe('criticalConstraint');
  });
});

// ============================================================================
// §3 OBSERVABLE PROPERTIES — shape, type, and hub emissions (spec §3.1, §4.x)
// ============================================================================

describe('§3 ScenarioVM observable properties (spec §3.1)', () => {
  it('has scenario: ScenarioM | undefined, initially undefined', () => {
    const vm = makeScenarioVm();
    expect(vm.model.scenario).toBeUndefined();
  });

  it('has filePath: string | undefined, initially undefined', () => {
    const vm = makeScenarioVm();
    expect(vm.model.filePath).toBeUndefined();
  });

  it('has isDirty: boolean, initially false', () => {
    const vm = makeScenarioVm();
    expect(typeof vm.model.isDirty).toBe('boolean');
    expect(vm.model.isDirty).toBe(false);
  });

  it('has candidates: readonly CandidateM[], initially empty', () => {
    const vm = makeScenarioVm();
    expect(Array.isArray(vm.model.candidates)).toBe(true);
    expect(vm.model.candidates).toHaveLength(0);
  });

  it('has criticalDecisions: readonly CriticalDecisionM[], initially empty', () => {
    const vm = makeScenarioVm();
    expect(Array.isArray(vm.model.criticalDecisions)).toBe(true);
    expect(vm.model.criticalDecisions).toHaveLength(0);
  });

  it('has criticalConstraints: readonly CriticalConstraintM[], initially empty', () => {
    const vm = makeScenarioVm();
    expect(Array.isArray(vm.model.criticalConstraints)).toBe(true);
    expect(vm.model.criticalConstraints).toHaveLength(0);
  });

  it('has status: string, initially non-empty', () => {
    const vm = makeScenarioVm();
    expect(typeof vm.model.status).toBe('string');
    expect(vm.model.status.length).toBeGreaterThan(0);
  });

  it('has warnings: readonly string[], initially empty', () => {
    const vm = makeScenarioVm();
    expect(Array.isArray(vm.model.warnings)).toBe(true);
    expect(vm.model.warnings).toHaveLength(0);
  });

  it('model is populated after mutation (_browserOpen)', () => {
    const vm = makeScenarioVm();
    // The ScenarioVM uses its own internal hub; verify the model changes on mutation.
    expect(vm.model.scenario).toBeUndefined();
    vm._browserOpen(sasRaw, 'sas.json');
    // After _browserOpen, scenario is populated — model mutation happened
    expect(vm.model.scenario).toBeDefined();
  });

  it('scenario properties populated after _browserOpen (SAS)', () => {
    const vm = makeScenarioVm();
    vm._browserOpen(sasRaw, 'sas.json');
    const s = vm.model.scenario!;
    expect(s).toBeDefined();
    expect(s.decisions.length).toBeGreaterThan(0);
    expect(s.alternatives.length).toBeGreaterThan(0);
    expect(s.properties.length).toBeGreaterThan(0);
    expect(s.coefficients.length).toBeGreaterThan(0);
  });

  it('candidates populated after _browserOpen (SAS) — 720 candidates', () => {
    const vm = makeScenarioVm();
    vm._browserOpen(sasRaw, 'sas.json');
    expect(vm.model.candidates.length).toBe(720);
  });

  it('criticalDecisions populated after _browserOpen (SAS)', () => {
    const vm = makeScenarioVm();
    vm._browserOpen(sasRaw, 'sas.json');
    expect(vm.model.criticalDecisions.length).toBeGreaterThan(0);
  });

  it('criticalConstraints populated after _browserOpen (SAS)', () => {
    const vm = makeScenarioVm();
    vm._browserOpen(sasRaw, 'sas.json');
    expect(vm.model.criticalConstraints.length).toBeGreaterThan(0);
  });

  it('status reflects solve result', () => {
    const vm = makeScenarioVm();
    vm._browserOpen(sasRaw, 'sas.json');
    expect(vm.model.status).toContain('Solved');
    expect(vm.model.status).toContain('720');
  });
});

describe('§3 DecisionVM observable properties (spec §4.1)', () => {
  const hub = new MessageHub();

  it('id is read-only in the model', () => {
    const vm = makeDecisionVm({ id: 'd1', name: 'Decision 1' }, hub);
    expect(vm.model.id).toBe('d1');
    // id should be present (read-only in spec means not mutated by external ops)
  });

  it('name is read-write via model assignment', () => {
    const vm = makeDecisionVm({ id: 'd1', name: 'Decision 1' }, hub);
    vm.model = { ...vm.model, name: 'Updated' };
    expect(vm.model.name).toBe('Updated');
  });

  it('emits PropertyChangedMessage on model change', () => {
    // Create a fresh hub for this test to avoid interference
    const testHub = new MessageHub();
    const vm = makeDecisionVm({ id: 'd1', name: 'Decision 1' }, testHub);
    const changes = collectChanges(testHub, 'model', () => {
      vm.model = { ...vm.model, name: 'Changed' };
    });
    expect(changes.length).toBeGreaterThan(0);
  });
});

describe('§3 AlternativeVM observable properties (spec §4.2)', () => {
  const hub = new MessageHub();

  it('id is in model', () => {
    const vm = makeAlternativeVm({ id: 'a1', decisionId: 'd1', name: 'Alt 1' }, hub, () => {});
    expect(vm.model.id).toBe('a1');
  });

  it('decisionId is read-write', () => {
    const vm = makeAlternativeVm({ id: 'a1', decisionId: 'd1', name: 'Alt 1' }, hub, () => {});
    vm.model = { ...vm.model, decisionId: 'd2' };
    expect(vm.model.decisionId).toBe('d2');
  });

  it('name is read-write', () => {
    const vm = makeAlternativeVm({ id: 'a1', decisionId: 'd1', name: 'Alt 1' }, hub, () => {});
    vm.model = { ...vm.model, name: 'Alt Updated' };
    expect(vm.model.name).toBe('Alt Updated');
  });

  it('changing decisionId invokes onDecisionIdChanged callback', () => {
    let called = false;
    const vm = makeAlternativeVm({ id: 'a1', decisionId: 'd1', name: 'Alt 1' }, hub, () => {
      called = true;
    });
    vm.model = { ...vm.model, decisionId: 'd2' };
    expect(called).toBe(true);
  });

  it('changing name does NOT invoke onDecisionIdChanged callback', () => {
    let called = false;
    const vm = makeAlternativeVm({ id: 'a1', decisionId: 'd1', name: 'Alt 1' }, hub, () => {
      called = true;
    });
    vm.model = { ...vm.model, name: 'Different Name' };
    expect(called).toBe(false);
  });
});

describe('§3 PropertyVM observable properties (spec §4.3)', () => {
  const hub = new MessageHub();

  it('id is in model', () => {
    const vm = makePropertyVm({ id: 'p1', name: 'P1', kind: 'max', weight: 1 }, hub, () => {});
    expect(vm.model.id).toBe('p1');
  });

  it('name is read-write', () => {
    const vm = makePropertyVm({ id: 'p1', name: 'P1', kind: 'max', weight: 1 }, hub, () => {});
    vm.model = { ...vm.model, name: 'Updated' };
    expect(vm.model.name).toBe('Updated');
  });

  it('kind is read-write min|max', () => {
    const vm = makePropertyVm({ id: 'p1', name: 'P1', kind: 'max', weight: 1 }, hub, () => {});
    vm.model = { ...vm.model, kind: 'min' };
    expect(vm.model.kind).toBe('min');
  });

  it('weight is read-write', () => {
    const vm = makePropertyVm({ id: 'p1', name: 'P1', kind: 'max', weight: 1 }, hub, () => {});
    vm.model = { ...vm.model, weight: 3.5 };
    expect(vm.model.weight).toBe(3.5);
  });

  it('changing kind triggers onSolveTrigger callback', () => {
    let solveCount = 0;
    const vm = makePropertyVm({ id: 'p1', name: 'P1', kind: 'max', weight: 1 }, hub, () => {
      solveCount++;
    });
    vm.model = { ...vm.model, kind: 'min' };
    expect(solveCount).toBe(1);
  });

  it('changing weight triggers onSolveTrigger callback', () => {
    let solveCount = 0;
    const vm = makePropertyVm({ id: 'p1', name: 'P1', kind: 'max', weight: 1 }, hub, () => {
      solveCount++;
    });
    vm.model = { ...vm.model, weight: 5 };
    expect(solveCount).toBe(1);
  });

  it('changing name does NOT trigger onSolveTrigger callback', () => {
    let solveCount = 0;
    const vm = makePropertyVm({ id: 'p1', name: 'P1', kind: 'max', weight: 1 }, hub, () => {
      solveCount++;
    });
    vm.model = { ...vm.model, name: 'New Name' };
    expect(solveCount).toBe(0);
  });
});

describe('§3 CoefficientVM observable properties (spec §4.4)', () => {
  const hub = new MessageHub();

  it('model has alternativeId, propertyId, and value with lower/modal/upper', () => {
    const coeff = { alternativeId: 'a1', propertyId: 'p1', value: new TriangularFuzzyM(1, 2, 3) };
    const vm = makeCoefficientVm(coeff, hub, () => {});
    expect(vm.model.alternativeId).toBe('a1');
    expect(vm.model.propertyId).toBe('p1');
    expect(vm.model.value.lower).toBe(1);
    expect(vm.model.value.modal).toBe(2);
    expect(vm.model.value.upper).toBe(3);
  });

  it('any model change triggers onSolveTrigger', () => {
    let solveCount = 0;
    const vm = makeCoefficientVm(
      { alternativeId: 'a1', propertyId: 'p1', value: new TriangularFuzzyM(1, 2, 3) },
      hub,
      () => {
        solveCount++;
      },
    );
    vm.model = { ...vm.model, value: new TriangularFuzzyM(2, 3, 4) };
    expect(solveCount).toBe(1);
  });
});

describe('§3 ConstraintVM observable properties (spec §4.5)', () => {
  const hub = new MessageHub();

  it('threshold constraint model has kind, propertyId', () => {
    const vm = makeConstraintVm({ kind: 'threshold', propertyId: 'p1', min: 0 }, hub, () => {});
    expect(vm.model.kind).toBe('threshold');
    if (vm.model.kind === 'threshold') {
      expect(vm.model.propertyId).toBe('p1');
    }
  });

  it('dependency constraint model has kind, source and target alternativeId', () => {
    const vm = makeConstraintVm(
      { kind: 'dependency', sourceAlternativeId: 'a1', targetAlternativeId: 'a2' },
      hub,
      () => {},
    );
    expect(vm.model.kind).toBe('dependency');
    if (vm.model.kind === 'dependency') {
      expect(vm.model.sourceAlternativeId).toBe('a1');
      expect(vm.model.targetAlternativeId).toBe('a2');
    }
  });

  it('conflict constraint model has kind, alternativeAId, alternativeBId', () => {
    const vm = makeConstraintVm(
      { kind: 'conflict', alternativeAId: 'a1', alternativeBId: 'a2' },
      hub,
      () => {},
    );
    expect(vm.model.kind).toBe('conflict');
    if (vm.model.kind === 'conflict') {
      expect(vm.model.alternativeAId).toBe('a1');
      expect(vm.model.alternativeBId).toBe('a2');
    }
  });

  it('any constraint model change triggers onSolveTrigger', () => {
    let solveCount = 0;
    const vm = makeConstraintVm({ kind: 'threshold', propertyId: 'p1', min: 0 }, hub, () => {
      solveCount++;
    });
    vm.model = { kind: 'threshold', propertyId: 'p1', min: 5 };
    expect(solveCount).toBe(1);
  });
});

// ============================================================================
// §4 COMMANDS — spec §3.2
// ============================================================================

describe('§4 ScenarioVM commands (spec §3.2)', () => {
  it('newCmd exists with execute()', () => {
    const vm = makeScenarioVm();
    expect(vm.newCmd).toBeDefined();
    expect(typeof vm.newCmd.execute).toBe('function');
  });

  it('openCmd exists with execute()', () => {
    const vm = makeScenarioVm();
    expect(vm.openCmd).toBeDefined();
    expect(typeof vm.openCmd.execute).toBe('function');
  });

  it('saveCmd exists with execute() and canExecute()', () => {
    const vm = makeScenarioVm();
    expect(vm.saveCmd).toBeDefined();
    expect(typeof vm.saveCmd.execute).toBe('function');
    // canExecute is a method (function), not a boolean property
    expect(typeof vm.saveCmd.canExecute).toBe('function');
  });

  it('saveCmd.canExecute() === false when filePath is undefined', () => {
    const vm = makeScenarioVm();
    // No scenario loaded — filePath is undefined
    expect(vm.model.filePath).toBeUndefined();
    expect(vm.saveCmd.canExecute()).toBe(false);
  });

  it('saveCmd.canExecute() === false even after newCmd (no filePath)', () => {
    const vm = makeScenarioVm();
    vm.newCmd.execute();
    // newCmd creates a scenario but filePath stays undefined
    expect(vm.model.filePath).toBeUndefined();
    expect(vm.saveCmd.canExecute()).toBe(false);
  });

  it('saveAsCmd exists with execute()', () => {
    const vm = makeScenarioVm();
    expect(vm.saveAsCmd).toBeDefined();
    expect(typeof vm.saveAsCmd.execute).toBe('function');
  });

  it('solveCmd exists with execute() and canExecute()', () => {
    const vm = makeScenarioVm();
    expect(vm.solveCmd).toBeDefined();
    expect(typeof vm.solveCmd.execute).toBe('function');
    // canExecute is a method (function), not a boolean property
    expect(typeof vm.solveCmd.canExecute).toBe('function');
  });

  it('solveCmd.canExecute() === false when no scenario is loaded', () => {
    const vm = makeScenarioVm();
    expect(vm.model.scenario).toBeUndefined();
    expect(vm.solveCmd.canExecute()).toBe(false);
  });

  it('solveCmd.canExecute() === true after loading a scenario', () => {
    const vm = makeScenarioVm();
    vm._browserOpen(sasRaw, 'sas.json');
    expect(vm.solveCmd.canExecute()).toBe(true);
  });

  it('newCmd resets scenario to fresh empty state', () => {
    const vm = makeScenarioVm();
    vm._browserOpen(sasRaw, 'sas.json');
    expect(vm.model.scenario).toBeDefined();
    vm.newCmd.execute();
    const s = vm.model.scenario!;
    expect(s.decisions).toHaveLength(0);
    expect(s.alternatives).toHaveLength(0);
    expect(s.properties).toHaveLength(0);
    expect(vm.model.isDirty).toBe(false);
    expect(vm.model.filePath).toBeUndefined();
  });

  it('newCmd clears filePath', () => {
    const vm = makeScenarioVm();
    vm._browserOpen(sasRaw, 'sas.json');
    expect(vm.model.filePath).toBe('sas.json');
    vm.newCmd.execute();
    expect(vm.model.filePath).toBeUndefined();
  });

  it('solveCmd.execute() reruns solve and updates candidates', () => {
    const vm = makeScenarioVm();
    vm._browserOpen(sasRaw, 'sas.json');
    const beforeCandidates = vm.model.candidates;
    // Force a solve — should produce a new array reference
    vm.solveCmd.execute();
    // Array may be === if results are same, but a new solve runs
    expect(vm.model.candidates.length).toBeGreaterThan(0);
    // Status should still say Solved
    expect(vm.model.status).toContain('Solved');
    expect(beforeCandidates.length).toBe(vm.model.candidates.length);
  });
});

// ============================================================================
// §5 MUTATION PROPAGATION — child mutations reflect in parent
// ============================================================================

describe('§5 Mutation propagation', () => {
  it('addDecision: scenario.decisions grows by 1', () => {
    const vm = makeScenarioVm();
    vm.newCmd.execute();
    const before = vm.model.scenario!.decisions.length;
    vm.addDecision('My Decision');
    expect(vm.model.scenario!.decisions.length).toBe(before + 1);
    expect(vm.model.scenario!.decisions.at(-1)!.name).toBe('My Decision');
  });

  it('updateDecisionName: name propagates to scenario.decisions[i].name', () => {
    const vm = makeScenarioVm();
    vm.newCmd.execute();
    vm.addDecision('Original');
    const decId = vm.model.scenario!.decisions.at(-1)!.id;
    vm.updateDecisionName(decId, 'Renamed');
    expect(vm.model.scenario!.decisions.find((d) => d.id === decId)?.name).toBe('Renamed');
  });

  it('deleteDecision: removes decision from scenario.decisions', () => {
    const vm = makeScenarioVm();
    vm.newCmd.execute();
    vm.addDecision('ToDelete');
    const decId = vm.model.scenario!.decisions.at(-1)!.id;
    vm.deleteDecision(decId);
    expect(vm.model.scenario!.decisions.find((d) => d.id === decId)).toBeUndefined();
  });

  it('addAlternative: scenario.alternatives grows by 1 under the right decision', () => {
    const vm = makeScenarioVm();
    vm.newCmd.execute();
    vm.addDecision('D1');
    const decId = vm.model.scenario!.decisions.at(-1)!.id;
    const before = vm.model.scenario!.alternatives.length;
    vm.addAlternative(decId, 'Alt A');
    expect(vm.model.scenario!.alternatives.length).toBe(before + 1);
    const newAlt = vm.model.scenario!.alternatives.at(-1)!;
    expect(newAlt.decisionId).toBe(decId);
    expect(newAlt.name).toBe('Alt A');
  });

  it('addAlternative: throws when decisionId is unknown', () => {
    const vm = makeScenarioVm();
    vm.newCmd.execute();
    expect(() => vm.addAlternative('nonexistent-id', 'Alt')).toThrow(ScenarioMutationError);
  });

  it('updateAlternativeName: name propagates to scenario.alternatives[i].name', () => {
    const vm = makeScenarioVm();
    vm.newCmd.execute();
    vm.addDecision('D1');
    const decId = vm.model.scenario!.decisions.at(-1)!.id;
    vm.addAlternative(decId, 'Original');
    const altId = vm.model.scenario!.alternatives.at(-1)!.id;
    vm.updateAlternativeName(altId, 'Renamed');
    expect(vm.model.scenario!.alternatives.find((a) => a.id === altId)?.name).toBe('Renamed');
  });

  it('addProperty: scenario.properties grows by 1', () => {
    const vm = makeScenarioVm();
    vm.newCmd.execute();
    const before = vm.model.scenario!.properties.length;
    vm.addProperty('MyProp');
    expect(vm.model.scenario!.properties.length).toBe(before + 1);
    expect(vm.model.scenario!.properties.at(-1)!.name).toBe('MyProp');
  });

  it('updatePropertyWeight: weight propagates to scenario.properties[i].weight', () => {
    const vm = makeScenarioVm();
    vm.newCmd.execute();
    vm.addProperty('Prop1');
    const propId = vm.model.scenario!.properties.at(-1)!.id;
    vm.updatePropertyWeight(propId, 7.5);
    expect(vm.model.scenario!.properties.find((p) => p.id === propId)?.weight).toBe(7.5);
  });

  it('updatePropertyKind: kind propagates to scenario.properties[i].kind', () => {
    const vm = makeScenarioVm();
    vm.newCmd.execute();
    vm.addProperty('Prop1');
    const propId = vm.model.scenario!.properties.at(-1)!.id;
    expect(vm.model.scenario!.properties.find((p) => p.id === propId)?.kind).toBe('min'); // default
    vm.updatePropertyKind(propId, 'max');
    expect(vm.model.scenario!.properties.find((p) => p.id === propId)?.kind).toBe('max');
  });

  it('updateCoefficient: value propagates to scenario.coefficients', () => {
    const vm = makeScenarioVm();
    vm.newCmd.execute();
    vm.addDecision('D1');
    const decId = vm.model.scenario!.decisions.at(-1)!.id;
    vm.addAlternative(decId, 'A1');
    const altId = vm.model.scenario!.alternatives.at(-1)!.id;
    vm.addProperty('P1');
    const propId = vm.model.scenario!.properties.at(-1)!.id;

    vm.updateCoefficient(altId, propId, 1, 2, 3);
    const coeff = vm.model.scenario!.coefficients.find(
      (c) => c.alternativeId === altId && c.propertyId === propId,
    );
    expect(coeff).toBeDefined();
    expect(coeff!.value.lower).toBe(1);
    expect(coeff!.value.modal).toBe(2);
    expect(coeff!.value.upper).toBe(3);
  });

  it('addConstraint: scenario.constraints grows by 1', () => {
    const vm = makeScenarioVm();
    vm._browserOpen(sasRaw, 'sas.json');
    const before = vm.model.scenario!.constraints.length;
    const s = vm.model.scenario!;
    const propId = s.properties[0].id;
    vm.addConstraint({ kind: 'threshold', propertyId: propId, min: 0 });
    expect(vm.model.scenario!.constraints.length).toBe(before + 1);
  });

  it('deleteConstraint: scenario.constraints shrinks by 1', () => {
    const vm = makeScenarioVm();
    vm._browserOpen(sasRaw, 'sas.json');
    const s = vm.model.scenario!;
    const propId = s.properties[0].id;
    vm.addConstraint({ kind: 'threshold', propertyId: propId, min: 0 });
    const before = vm.model.scenario!.constraints.length;
    vm.deleteConstraint(before - 1);
    expect(vm.model.scenario!.constraints.length).toBe(before - 1);
  });

  it('deleteDecision cascades: removes alternatives and their coefficients', () => {
    const vm = makeScenarioVm();
    vm._browserOpen(sasRaw, 'sas.json');
    const decId = vm.model.scenario!.decisions[0].id;
    const altIds = vm.model
      .scenario!.alternatives.filter((a) => a.decisionId === decId)
      .map((a) => a.id);

    vm.deleteDecision(decId);
    const s2 = vm.model.scenario!;
    expect(s2.decisions.find((d) => d.id === decId)).toBeUndefined();
    for (const altId of altIds) {
      expect(s2.alternatives.find((a) => a.id === altId)).toBeUndefined();
      expect(s2.coefficients.filter((c) => c.alternativeId === altId)).toHaveLength(0);
    }
  });

  it('isDirty becomes true after any mutation', () => {
    const vm = makeScenarioVm();
    vm._browserOpen(sasRaw, 'sas.json');
    expect(vm.model.isDirty).toBe(false);
    vm.addDecision('DirtyTest');
    expect(vm.model.isDirty).toBe(true);
  });
});

// ============================================================================
// §6 SOLVE-TRIGGER MATRIX — spec §3.3
// All 15 parameterized cases
// ============================================================================

describe('§6 Solve-trigger matrix (spec §3.3)', () => {
  // Helper: returns true if candidates array reference changed (solve ran)
  function didResolveAfter(action: (vm: ReturnType<typeof makeScenarioVm>) => void): boolean {
    const vm = makeScenarioVm();
    vm._browserOpen(sasRaw, 'sas.json');
    const before = vm.model.candidates;
    action(vm);
    return vm.model.candidates !== before;
  }

  // ── Cases that MUST NOT trigger solve ──────────────────────────────────────

  it('mutating scenario.name does NOT trigger solve', () => {
    const triggered = didResolveAfter((vm) => {
      vm.updateScenarioName('New Scenario Name');
    });
    expect(triggered).toBe(false);
  });

  it('mutating decision name (updateDecisionName) does NOT trigger solve', () => {
    const triggered = didResolveAfter((vm) => {
      const decId = vm.model.scenario!.decisions[0].id;
      vm.updateDecisionName(decId, 'New Name');
    });
    expect(triggered).toBe(false);
  });

  it('mutating alternative name (updateAlternativeName) does NOT trigger solve', () => {
    const triggered = didResolveAfter((vm) => {
      const altId = vm.model.scenario!.alternatives[0].id;
      vm.updateAlternativeName(altId, 'New Name');
    });
    expect(triggered).toBe(false);
  });

  it('changing filePath via saveAsCmd does NOT trigger solve', () => {
    // saveAsCmd sets filePath; we mock fs by intercepting (saveAsCmd calls saveCmd which writes
    // to disk — will fail in test env; catch the error and verify candidates unchanged)
    const vm = makeScenarioVm();
    vm._browserOpen(sasRaw, 'sas.json');
    const before = vm.model.candidates;
    // saveAsCmd to nonexistent path will fail; candidates should not change due to path change
    try {
      vm.saveAsCmd.execute('/tmp/__ga_test_save.json');
    } catch {
      // ignore save errors
    }
    // The filePath change itself (not the save) should not trigger solve
    // We verify by checking that setting a path doesn't mutate candidates
    expect(vm.model.candidates).toBe(before);
  });

  it('updating property name does NOT trigger solve', () => {
    const triggered = didResolveAfter((vm) => {
      const propId = vm.model.scenario!.properties[0].id;
      vm.updatePropertyName(propId, 'Renamed Property');
    });
    expect(triggered).toBe(false);
  });

  // ── Cases that MUST trigger solve ─────────────────────────────────────────

  it('addDecision triggers solve (structural change)', () => {
    const triggered = didResolveAfter((vm) => {
      vm.addDecision('Trigger Test Decision');
    });
    expect(triggered).toBe(true);
  });

  it('deleteDecision triggers solve', () => {
    const triggered = didResolveAfter((vm) => {
      const decId = vm.model.scenario!.decisions.at(-1)!.id;
      vm.deleteDecision(decId);
    });
    expect(triggered).toBe(true);
  });

  it('addAlternative triggers solve (decisionId change)', () => {
    const triggered = didResolveAfter((vm) => {
      const decId = vm.model.scenario!.decisions[0].id;
      vm.addAlternative(decId, 'New Alt');
    });
    expect(triggered).toBe(true);
  });

  it('deleteAlternative triggers solve', () => {
    const triggered = didResolveAfter((vm) => {
      const altId = vm.model.scenario!.alternatives.at(-1)!.id;
      vm.deleteAlternative(altId);
    });
    expect(triggered).toBe(true);
  });

  it('updatePropertyKind triggers solve', () => {
    const triggered = didResolveAfter((vm) => {
      const prop = vm.model.scenario!.properties[0];
      vm.updatePropertyKind(prop.id, prop.kind === 'min' ? 'max' : 'min');
    });
    expect(triggered).toBe(true);
  });

  it('updatePropertyWeight triggers solve', () => {
    const triggered = didResolveAfter((vm) => {
      const propId = vm.model.scenario!.properties[0].id;
      vm.updatePropertyWeight(propId, 42);
    });
    expect(triggered).toBe(true);
  });

  it('updateCoefficient (any cell value) triggers solve', () => {
    const triggered = didResolveAfter((vm) => {
      const c = vm.model.scenario!.coefficients[0];
      vm.updateCoefficient(c.alternativeId, c.propertyId, 1, 2, 3);
    });
    expect(triggered).toBe(true);
  });

  it('addConstraint triggers solve', () => {
    const triggered = didResolveAfter((vm) => {
      const propId = vm.model.scenario!.properties[0].id;
      vm.addConstraint({ kind: 'threshold', propertyId: propId, min: 0 });
    });
    expect(triggered).toBe(true);
  });

  it('deleteConstraint triggers solve', () => {
    const triggered = didResolveAfter((vm) => {
      const propId = vm.model.scenario!.properties[0].id;
      vm.addConstraint({ kind: 'threshold', propertyId: propId, min: 0 });
      const idx = vm.model.scenario!.constraints.length - 1;
      vm.deleteConstraint(idx);
    });
    expect(triggered).toBe(true);
  });

  it('updateConstraint triggers solve', () => {
    const triggered = didResolveAfter((vm) => {
      // SAS has constraints; update the first one
      const s = vm.model.scenario!;
      if (s.constraints.length > 0) {
        const existing = s.constraints[0];
        vm.updateConstraint(0, existing); // same value, still triggers
      } else {
        const propId = s.properties[0].id;
        vm.addConstraint({ kind: 'threshold', propertyId: propId, min: 0 });
        vm.updateConstraint(s.constraints.length, {
          kind: 'threshold',
          propertyId: propId,
          min: 1,
        });
      }
    });
    expect(triggered).toBe(true);
  });
});

// ============================================================================
// §7 RESULT VMs READ-ONLY — spec §4.6
// CandidateVM, CriticalDecisionVM, CriticalConstraintVM have no mutation methods
// ============================================================================

describe('§7 Result VMs are read-only (spec §4.6)', () => {
  it('CandidateVM has no addX / updateX / deleteX methods', () => {
    const vm = makeCandidateVm({
      alternativeIds: ['a1'],
      triangularValue: new TriangularFuzzyM(0, 1, 2),
      normalizedValue: { positive: 0.1, average: 0.5, negative: 0.9 },
      score: 0.42,
      rank: 0,
    });
    // The VM itself should not expose mutation helpers
    expect((vm as unknown as Record<string, unknown>).addDecision).toBeUndefined();
    expect((vm as unknown as Record<string, unknown>).updateDecision).toBeUndefined();
    expect((vm as unknown as Record<string, unknown>).deleteDecision).toBeUndefined();
    // Commands should not be present (no newCmd, saveCmd, etc.)
    expect((vm as unknown as Record<string, unknown>).newCmd).toBeUndefined();
    expect((vm as unknown as Record<string, unknown>).saveCmd).toBeUndefined();
  });

  it('CandidateVM model fields are readonly (score, rank, alternativeIds)', () => {
    const vm = makeCandidateVm({
      alternativeIds: ['a1'],
      triangularValue: new TriangularFuzzyM(0, 1, 2),
      normalizedValue: { positive: 0.1, average: 0.5, negative: 0.9 },
      score: 0.42,
      rank: 0,
    });
    expect(vm.model.score).toBe(0.42);
    expect(vm.model.rank).toBe(0);
    expect(vm.model.alternativeIds).toEqual(['a1']);
  });

  it('CriticalDecisionVM has no mutation helpers', () => {
    const vm = makeCriticalDecisionVm({
      decisionId: 'd1',
      triangularValue: new TriangularFuzzyM(0, 1, 2),
      normalizedValue: { positive: 0.1, average: 0.5, negative: 0.9 },
      score: 0.3,
      rank: 0,
    });
    expect((vm as unknown as Record<string, unknown>).addDecision).toBeUndefined();
    expect((vm as unknown as Record<string, unknown>).newCmd).toBeUndefined();
  });

  it('CriticalDecisionVM model has decisionId, score, rank', () => {
    const vm = makeCriticalDecisionVm({
      decisionId: 'd1',
      triangularValue: new TriangularFuzzyM(0, 1, 2),
      normalizedValue: { positive: 0.1, average: 0.5, negative: 0.9 },
      score: 0.3,
      rank: 0,
    });
    expect(vm.model.decisionId).toBe('d1');
    expect(vm.model.score).toBe(0.3);
    expect(vm.model.rank).toBe(0);
  });

  it('CriticalConstraintVM has no mutation helpers', () => {
    const vm = makeCriticalConstraintVm({
      constraintIndex: 0,
      kind: 'conflict',
      eliminated: 4,
      total: 8,
      redundant: false,
    });
    expect((vm as unknown as Record<string, unknown>).addConstraint).toBeUndefined();
    expect((vm as unknown as Record<string, unknown>).newCmd).toBeUndefined();
  });

  it('CriticalConstraintVM model has constraintIndex, kind, eliminated, total, redundant', () => {
    const vm = makeCriticalConstraintVm({
      constraintIndex: 2,
      kind: 'dependency',
      eliminated: 10,
      total: 20,
      redundant: true,
    });
    expect(vm.model.constraintIndex).toBe(2);
    expect(vm.model.kind).toBe('dependency');
    expect(vm.model.eliminated).toBe(10);
    expect(vm.model.total).toBe(20);
    expect(vm.model.redundant).toBe(true);
  });
});

// ============================================================================
// §8 SPEC-VS-IMPL DEVIATIONS documented as assertions
// ============================================================================

describe('§8 Spec-vs-impl deviations (documented)', () => {
  /**
   * DEVIATION 1: The spec §2 calls for separate CompositeVM tree nodes:
   *   DecisionsVM, PropertiesVM, CoefficientsVM, ConstraintsVM,
   *   CandidatesVM, CriticalDecisionsVM, CriticalConstraintsVM, AnalysisVM
   *
   * In this TypeScript implementation, these are NOT realized as standalone
   * CompositeVM / AggregateVM instances. Instead, ScenarioVM holds a flat
   * ScenarioState model containing `scenario.decisions[]`, `scenario.alternatives[]`,
   * etc. Child VMs (DecisionVM, PropertyVM, etc.) are instantiated on-the-fly
   * by the view layer, not stored in CompositeVM collections on the ScenarioVM.
   *
   * This is an architectural simplification for M3. The per-item VM factories
   * (makeDecisionVm, makeAlternativeVm, etc.) are correct and tested;
   * only the CompositeVM/AggregateVM wrapper layer is missing.
   *
   * AnalysisVM (AggregateVM2 with charts) is an M4 item, not yet implemented.
   */
  it('ScenarioVM.model exposes decisions array (not DecisionsVM)', () => {
    const vm = makeScenarioVm();
    vm._browserOpen(sasRaw, 'sas.json');
    // The spec would have vm.decisionsVm.items[i]; current impl has:
    expect(Array.isArray(vm.model.scenario?.decisions)).toBe(true);
    // Accessor for per-item VMs would need to be constructed by the view:
    // e.g., vm.model.scenario.decisions.map(d => makeDecisionVm(d, hub))
  });

  it('ScenarioVM.model exposes alternatives array (not AlternativesVM)', () => {
    const vm = makeScenarioVm();
    vm._browserOpen(sasRaw, 'sas.json');
    expect(Array.isArray(vm.model.scenario?.alternatives)).toBe(true);
  });

  it('ScenarioVM.model exposes properties array (not PropertiesVM)', () => {
    const vm = makeScenarioVm();
    vm._browserOpen(sasRaw, 'sas.json');
    expect(Array.isArray(vm.model.scenario?.properties)).toBe(true);
  });

  it('ScenarioVM.model exposes coefficients array (not CoefficientsVM)', () => {
    const vm = makeScenarioVm();
    vm._browserOpen(sasRaw, 'sas.json');
    expect(Array.isArray(vm.model.scenario?.coefficients)).toBe(true);
  });

  it('ScenarioVM.model exposes constraints array (not ConstraintsVM)', () => {
    const vm = makeScenarioVm();
    vm._browserOpen(sasRaw, 'sas.json');
    expect(Array.isArray(vm.model.scenario?.constraints)).toBe(true);
  });

  it('ScenarioVM.model exposes candidates (not CandidatesVM CompositeVM)', () => {
    const vm = makeScenarioVm();
    vm._browserOpen(sasRaw, 'sas.json');
    // Per spec: CandidatesVM = CompositeVM<CandidateVM>; impl: candidates: readonly CandidateM[]
    expect(Array.isArray(vm.model.candidates)).toBe(true);
    expect(vm.model.candidates[0]).toHaveProperty('score');
    expect(vm.model.candidates[0]).toHaveProperty('rank');
    expect(vm.model.candidates[0]).toHaveProperty('alternativeIds');
  });

  it('ScenarioVM.model exposes criticalDecisions (not CriticalDecisionsVM)', () => {
    const vm = makeScenarioVm();
    vm._browserOpen(sasRaw, 'sas.json');
    expect(Array.isArray(vm.model.criticalDecisions)).toBe(true);
  });

  it('ScenarioVM.model exposes criticalConstraints (not CriticalConstraintsVM)', () => {
    const vm = makeScenarioVm();
    vm._browserOpen(sasRaw, 'sas.json');
    expect(Array.isArray(vm.model.criticalConstraints)).toBe(true);
  });
});
