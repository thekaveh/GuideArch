/**
 * ScenarioVM — root ViewModel that owns a loaded scenario and brokers re-solve.
 *
 * Uses ComponentVMOf<ScenarioState> with the VMx builder pattern.
 * A live MessageHub is used so adapters can subscribe to PropertyChangedMessage
 * on the hub's messages stream. NullDispatcher provides synchronous scheduling.
 *
 * Solve trigger: any mutating child change calls _triggerSolve() which updates
 * candidates, criticalDecisions, criticalConstraints, and status on the model.
 *
 * Per spec §3.3, re-solve is NOT triggered when only scenario.name, description,
 * or filePath change.
 */
import fs from 'fs';
import { ComponentVMOf, RelayCommand, RelayCommandOf, MessageHub, NullDispatcher } from 'vmx';
import { loadScenario } from '../models/scenario-loader.js';
import { solve } from '../models/topsis/solve.js';
import { criticalDecisions } from '../models/topsis/critical-decisions.js';
import { criticalConstraints } from '../models/topsis/critical-constraints.js';
import type { ScenarioM } from '../models/scenario.js';
import type { CandidateM } from '../models/candidate.js';
import type { CriticalDecisionM } from '../models/critical-decision.js';
import type { CriticalConstraintM } from '../models/critical-constraint.js';

// ---------------------------------------------------------------------------
// State shape — the typed model for the ScenarioVM
// ---------------------------------------------------------------------------
export interface ScenarioState {
  scenario: ScenarioM | undefined;
  filePath: string | undefined;
  isDirty: boolean;
  candidates: readonly CandidateM[];
  criticalDecisions: readonly CriticalDecisionM[];
  criticalConstraints: readonly CriticalConstraintM[];
  status: string;
  warnings: readonly string[];
}

// ---------------------------------------------------------------------------
// ScenarioVM type alias — ComponentVMOf extended with the five commands
// ---------------------------------------------------------------------------
export type ScenarioVM = ComponentVMOf<ScenarioState> & {
  readonly newCmd: RelayCommand;
  readonly openCmd: RelayCommandOf<string>;
  readonly saveCmd: RelayCommand;
  readonly saveAsCmd: RelayCommandOf<string>;
  readonly solveCmd: RelayCommand;
};

// ---------------------------------------------------------------------------
// Internal helpers
// ---------------------------------------------------------------------------

function _emptyState(): ScenarioState {
  return {
    scenario: undefined,
    filePath: undefined,
    isDirty: false,
    candidates: [],
    criticalDecisions: [],
    criticalConstraints: [],
    status: 'No scenario loaded.',
    warnings: [],
  };
}

function _runSolve(
  scenario: ScenarioM,
): Pick<ScenarioState, 'candidates' | 'criticalDecisions' | 'criticalConstraints' | 'status'> {
  try {
    const candidates = solve(scenario);
    const decisionAnalysis = criticalDecisions(scenario, candidates);
    const constraintAnalysis = criticalConstraints(scenario);
    const status = `Solved: ${candidates.length} candidates`;
    return {
      candidates,
      criticalDecisions: decisionAnalysis,
      criticalConstraints: constraintAnalysis,
      status,
    };
  } catch (err) {
    const msg = err instanceof Error ? err.message : String(err);
    return {
      candidates: [],
      criticalDecisions: [],
      criticalConstraints: [],
      status: `Solve error: ${msg}`,
    };
  }
}

// ---------------------------------------------------------------------------
// Factory
// ---------------------------------------------------------------------------
export function makeScenarioVm(): ScenarioVM {
  // Live MessageHub so adapters can subscribe to PropertyChangedMessage.
  // NullDispatcher schedules synchronously (suitable for M2).
  const hub = new MessageHub();

  // Mutable reference — set immediately after vm.construct()
  let _vm: ComponentVMOf<ScenarioState> | null = null;

  function _getModel(): ScenarioState {
    if (_vm === null) return _emptyState();
    return _vm.model;
  }

  function _setState(patch: Partial<ScenarioState>): void {
    if (_vm === null) return;
    _vm.model = { ..._vm.model, ...patch };
  }

  function _triggerSolve(): void {
    const { scenario } = _getModel();
    if (scenario === undefined) {
      _setState({
        candidates: [],
        criticalDecisions: [],
        criticalConstraints: [],
        status: 'No scenario loaded.',
      });
      return;
    }
    const result = _runSolve(scenario);
    _setState(result);
  }

  // ── Commands ─────────────────────────────────────────────────────────────

  const newCmd = RelayCommand.builder()
    .task(() => {
      const fresh: ScenarioM = {
        schemaVersion: '1.0.0',
        name: 'New scenario',
        description: '',
        decisions: [],
        alternatives: [],
        properties: [],
        coefficients: [],
        constraints: [],
        config: {
          aggregation: 'sum',
          weights: {
            positive: 1 / 3,
            average: 1 / 3,
            negative: 1 / 3,
          },
        },
        warnings: [],
      };
      _setState({
        scenario: fresh,
        filePath: undefined,
        isDirty: false,
        candidates: [],
        criticalDecisions: [],
        criticalConstraints: [],
        status: 'New scenario — nothing to solve.',
        warnings: [],
      });
    })
    .build();

  const openCmd = RelayCommandOf.builder<string>()
    .task((path: string) => {
      try {
        const scenario = loadScenario(path);
        const result = _runSolve(scenario);
        _setState({
          scenario,
          filePath: path,
          isDirty: false,
          warnings: scenario.warnings,
          ...result,
        });
      } catch (err) {
        const msg = err instanceof Error ? err.message : String(err);
        _setState({
          warnings: [..._getModel().warnings, `Open failed: ${msg}`],
          status: `Open failed: ${msg}`,
        });
      }
    })
    .build();

  const saveCmd = RelayCommand.builder()
    .predicate(() => {
      const { filePath, scenario } = _getModel();
      return filePath !== undefined && scenario !== undefined;
    })
    .task(() => {
      const { scenario, filePath } = _getModel();
      if (scenario === undefined || filePath === undefined) return;
      try {
        fs.writeFileSync(filePath, JSON.stringify(scenario, null, 2) + '\n', 'utf-8');
        _setState({ isDirty: false });
      } catch (err) {
        const msg = err instanceof Error ? err.message : String(err);
        _setState({ status: `Save failed: ${msg}` });
      }
    })
    .build();

  const saveAsCmd = RelayCommandOf.builder<string>()
    .task((path: string) => {
      _setState({ filePath: path });
      saveCmd.execute();
    })
    .build();

  const solveCmd = RelayCommand.builder()
    .predicate(() => _getModel().scenario !== undefined)
    .task(() => _triggerSolve())
    .build();

  // ── Build the ComponentVMOf ───────────────────────────────────────────────

  const vm = ComponentVMOf.builder<ScenarioState>()
    .name('scenario')
    .model(_emptyState())
    .services(hub, NullDispatcher.INSTANCE)
    .modeledHinter((m) => m.scenario?.name ?? '(no scenario)')
    .build();

  vm.construct();
  _vm = vm;

  // Attach commands as own properties so the type cast below is safe
  Object.defineProperties(vm, {
    newCmd: { value: newCmd, writable: false, enumerable: true, configurable: false },
    openCmd: { value: openCmd, writable: false, enumerable: true, configurable: false },
    saveCmd: { value: saveCmd, writable: false, enumerable: true, configurable: false },
    saveAsCmd: { value: saveAsCmd, writable: false, enumerable: true, configurable: false },
    solveCmd: { value: solveCmd, writable: false, enumerable: true, configurable: false },
  });

  return vm as ScenarioVM;
}
