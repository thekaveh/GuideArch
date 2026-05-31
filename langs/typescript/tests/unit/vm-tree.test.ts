/**
 * Structural tests: verify the VM tree exists with expected names and commands.
 *
 * Uses VMx introspection (vm.name, command existence) to guard against
 * accidental renames or missing exports.
 */
import { describe, it, expect } from 'vitest';
import { makeScenarioVm } from '../../src/viewmodels/scenario-vm.js';
import { makeCandidateVm } from '../../src/viewmodels/candidate-vm.js';
import { makeCriticalDecisionVm } from '../../src/viewmodels/critical-decision-vm.js';
import { makeCriticalConstraintVm } from '../../src/viewmodels/critical-constraint-vm.js';
import { makeDecisionVm } from '../../src/viewmodels/decision-vm.js';
import { makeAlternativeVm } from '../../src/viewmodels/alternative-vm.js';
import { makePropertyVm } from '../../src/viewmodels/property-vm.js';
import { makeCoefficientVm } from '../../src/viewmodels/coefficient-vm.js';
import { makeConstraintVm } from '../../src/viewmodels/constraint-vm.js';
import { TriangularFuzzyM } from '../../src/models/triangular-fuzzy.js';
import { MessageHub } from 'vmx';

// ---------------------------------------------------------------------------
// ScenarioVM — structure
// ---------------------------------------------------------------------------

describe('ScenarioVM — VM name', () => {
  it('has name "scenario-vm"', () => {
    const vm = makeScenarioVm();
    expect(vm.name).toBe('scenario-vm');
  });
});

describe('ScenarioVM — commands exist', () => {
  const vm = makeScenarioVm();

  it('has newCmd', () => {
    expect(vm.newCmd).toBeDefined();
    expect(typeof vm.newCmd.execute).toBe('function');
  });

  it('has openCmd', () => {
    expect(vm.openCmd).toBeDefined();
    expect(typeof vm.openCmd.execute).toBe('function');
  });

  it('has saveCmd', () => {
    expect(vm.saveCmd).toBeDefined();
    expect(typeof vm.saveCmd.execute).toBe('function');
  });

  it('has saveAsCmd', () => {
    expect(vm.saveAsCmd).toBeDefined();
    expect(typeof vm.saveAsCmd.execute).toBe('function');
  });

  it('has solveCmd', () => {
    expect(vm.solveCmd).toBeDefined();
    expect(typeof vm.solveCmd.execute).toBe('function');
  });
});

describe('ScenarioVM — initial model shape', () => {
  const vm = makeScenarioVm();

  it('scenario is undefined initially', () => {
    expect(vm.model.scenario).toBeUndefined();
  });

  it('filePath is undefined initially', () => {
    expect(vm.model.filePath).toBeUndefined();
  });

  it('isDirty is false initially', () => {
    expect(vm.model.isDirty).toBe(false);
  });

  it('candidates is empty initially', () => {
    expect(vm.model.candidates).toHaveLength(0);
  });

  it('criticalDecisions is empty initially', () => {
    expect(vm.model.criticalDecisions).toHaveLength(0);
  });

  it('criticalConstraints is empty initially', () => {
    expect(vm.model.criticalConstraints).toHaveLength(0);
  });

  it('status is a non-empty string initially', () => {
    expect(typeof vm.model.status).toBe('string');
    expect(vm.model.status.length).toBeGreaterThan(0);
  });

  it('warnings is empty initially', () => {
    expect(vm.model.warnings).toHaveLength(0);
  });
});

// ---------------------------------------------------------------------------
// Child VM factories — names
// ---------------------------------------------------------------------------

const hub = new MessageHub();

describe('DecisionVM', () => {
  it('has name "decision"', () => {
    const vm = makeDecisionVm({ id: 'd1', name: 'Test Decision' }, hub);
    expect(vm.name).toBe('decision');
  });
  it('model contains decision data', () => {
    const vm = makeDecisionVm({ id: 'd1', name: 'Test Decision' }, hub);
    expect(vm.model.id).toBe('d1');
    expect(vm.model.name).toBe('Test Decision');
  });
});

describe('AlternativeVM', () => {
  it('has name "alternative"', () => {
    const vm = makeAlternativeVm({ id: 'a1', decisionId: 'd1', name: 'Alt 1' }, hub, () => {});
    expect(vm.name).toBe('alternative');
  });
});

describe('PropertyVM', () => {
  it('has name "property"', () => {
    const vm = makePropertyVm({ id: 'p1', name: 'Prop 1', kind: 'max', weight: 1 }, hub, () => {});
    expect(vm.name).toBe('property');
  });
});

describe('CoefficientVM', () => {
  it('has name "coefficient"', () => {
    const coeff = {
      alternativeId: 'a1',
      propertyId: 'p1',
      value: new TriangularFuzzyM(1, 2, 3),
    };
    const vm = makeCoefficientVm(coeff, hub, () => {});
    expect(vm.name).toBe('coefficient');
  });
});

describe('ConstraintVM', () => {
  it('has name "constraint"', () => {
    const vm = makeConstraintVm(
      { kind: 'conflict', alternativeAId: 'a1', alternativeBId: 'a2' },
      hub,
      () => {},
    );
    expect(vm.name).toBe('constraint');
  });
});

// ---------------------------------------------------------------------------
// Read-only result VMs
// ---------------------------------------------------------------------------

describe('CandidateVM', () => {
  it('has name "candidate"', () => {
    const candidate = {
      alternativeIds: ['a1'],
      triangularValue: new TriangularFuzzyM(1, 2, 3),
      normalizedValue: { positive: 0, average: 0, negative: 0 },
      score: 0,
      rank: 0,
    };
    const vm = makeCandidateVm(candidate);
    expect(vm.name).toBe('candidate');
  });
});

describe('CriticalDecisionVM', () => {
  it('has name "criticalDecision"', () => {
    const decision = {
      decisionId: 'd1',
      triangularValue: new TriangularFuzzyM(0, 0, 0),
      normalizedValue: { positive: 0, average: 0, negative: 0 },
      score: 0,
      rank: 0,
    };
    const vm = makeCriticalDecisionVm(decision);
    expect(vm.name).toBe('criticalDecision');
  });
});

describe('CriticalConstraintVM', () => {
  it('has name "criticalConstraint"', () => {
    const constraint = {
      constraintIndex: 0,
      kind: 'conflict',
      eliminated: 4,
      total: 8,
      redundant: false,
    };
    const vm = makeCriticalConstraintVm(constraint);
    expect(vm.name).toBe('criticalConstraint');
  });
});
