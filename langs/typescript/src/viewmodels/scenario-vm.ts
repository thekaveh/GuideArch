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
import { loadScenario, loadScenarioFromParsed } from '../models/scenario-loader.js';
import inlinedSchema from '../samples/scenario.schema.json';
import { solve } from '../models/topsis/solve.js';
import { criticalDecisions } from '../models/topsis/critical-decisions.js';
import { criticalConstraints } from '../models/topsis/critical-constraints.js';
import type { ScenarioM } from '../models/scenario.js';
import type { CandidateM } from '../models/candidate.js';
import type { CriticalDecisionM } from '../models/critical-decision.js';
import type { CriticalConstraintM } from '../models/critical-constraint.js';
import type { ConstraintM } from '../models/constraint.js';
import { TriangularFuzzyM } from '../models/triangular-fuzzy.js';

// ---------------------------------------------------------------------------
// ScenarioMutationError — thrown when a mutation violates a fatal invariant
// ---------------------------------------------------------------------------
export class ScenarioMutationError extends Error {
  constructor(message: string) {
    super(message);
    this.name = 'ScenarioMutationError';
  }
}

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
  /** Index into `candidates` that is currently selected for chart detail. Null when no candidates. */
  selectedCandidateIndex: number | null;
}

// ---------------------------------------------------------------------------
// ScenarioVM type alias — ComponentVMOf extended with the five commands and
// the M3 mutation helpers
// ---------------------------------------------------------------------------
export type ScenarioVM = ComponentVMOf<ScenarioState> & {
  readonly newCmd: RelayCommand;
  readonly openCmd: RelayCommandOf<string>;
  readonly saveCmd: RelayCommand;
  readonly saveAsCmd: RelayCommandOf<string>;
  readonly solveCmd: RelayCommand;

  // M3 mutations — each throws ScenarioMutationError on fatal invariant violation
  addDecision(name?: string): void;
  deleteDecision(id: string): void;
  updateDecisionName(id: string, name: string): void;

  addAlternative(decisionId: string, name?: string): void;
  deleteAlternative(id: string): void;
  updateAlternativeName(id: string, name: string): void;

  addProperty(name?: string, kind?: 'min' | 'max', weight?: number): void;
  deleteProperty(id: string): void;
  updatePropertyName(id: string, name: string): void;
  updatePropertyKind(id: string, kind: 'min' | 'max'): void;
  updatePropertyWeight(id: string, weight: number): void;

  updateCoefficient(
    alternativeId: string,
    propertyId: string,
    lower: number,
    modal: number,
    upper: number,
  ): void;

  addConstraint(c: ConstraintM): void;
  deleteConstraint(index: number): void;
  updateConstraint(index: number, c: ConstraintM): void;

  updateScenarioName(name: string): void;

  /** Select a candidate by index (0-based) for chart detail. Pass null to deselect. */
  setSelectedCandidateIndex(index: number | null): void;

  /**
   * Browser-mode hook: load a pre-parsed JSON object as a scenario.
   * Called by Toolbar when the user opens a local file in the browser
   * (FileReader path) or clicks "Open Sample …".
   * Uses the bundled inlined schema so no fs access is needed.
   */
  _browserOpen(raw: unknown, fileName: string): void;

  /**
   * Browser-mode hook: clear the dirty flag after a successful out-of-band
   * save (e.g. the anchor-download path in Toolbar.svelte). Optionally also
   * sets filePath. The Tauri/Node fs-based saveCmd handles its own dirty
   * clearing; this is for the browser path that bypasses the VM's IO.
   */
  _browserMarkSaved(filePath?: string): void;
};

// ---------------------------------------------------------------------------
// UUID helper (no crypto dep needed — simple timestamp-based)
// ---------------------------------------------------------------------------
function genId(prefix: string): string {
  return `${prefix}-${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 8)}`;
}

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
    selectedCandidateIndex: null,
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
  // NullDispatcher schedules synchronously (suitable for M2/M3).
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
        selectedCandidateIndex: null,
      });
      return;
    }
    const result = _runSolve(scenario);
    // Match C# preserve-if-in-range semantics so the three impls behave
    // identically across a re-solve: if the user had row k selected and
    // candidates is still long enough to contain k, keep it; otherwise
    // default to rank 0 (or null when there are no candidates).
    const prev = _getModel().selectedCandidateIndex;
    const selectedCandidateIndex =
      result.candidates.length === 0
        ? null
        : prev !== null && prev >= 0 && prev < result.candidates.length
          ? prev
          : 0;
    _setState({ ...result, selectedCandidateIndex });
  }

  function _requireScenario(): ScenarioM {
    const s = _getModel().scenario;
    if (!s) throw new ScenarioMutationError('No scenario loaded.');
    return s;
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
          aggregation: 'max',
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
        selectedCandidateIndex: null,
      });
    })
    .build();

  const openCmd = RelayCommandOf.builder<string>()
    .task((path: string) => {
      try {
        const scenario = loadScenario(path);
        const result = _runSolve(scenario);
        const selectedCandidateIndex = result.candidates.length > 0 ? 0 : null;
        _setState({
          scenario,
          filePath: path,
          isDirty: false,
          warnings: scenario.warnings,
          selectedCandidateIndex,
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
        // Omit the runtime-only `warnings` field — it is not part of the persisted schema.
        // eslint-disable-next-line @typescript-eslint/no-unused-vars
        const { warnings: _w, ...persistable } = scenario as ScenarioM & { warnings?: unknown };
        fs.writeFileSync(filePath, JSON.stringify(persistable, null, 2) + '\n', 'utf-8');
        _setState({ isDirty: false });
      } catch (err) {
        const msg = `Save failed: ${err instanceof Error ? err.message : String(err)}`;
        // Mirror Python and C#: set status AND append the same message to
        // warnings so the warnings tray reflects the error along with the
        // status bar.
        _setState({ status: msg, warnings: [..._getModel().warnings, msg] });
      }
    })
    .build();

  const saveAsCmd = RelayCommandOf.builder<string>()
    .task((path: string) => {
      const { scenario } = _getModel();
      if (scenario === undefined) return;
      try {
        // eslint-disable-next-line @typescript-eslint/no-unused-vars
        const { warnings: _w, ...persistable } = scenario as ScenarioM & { warnings?: unknown };
        fs.writeFileSync(path, JSON.stringify(persistable, null, 2) + '\n', 'utf-8');
      } catch (err) {
        const msg = `Save failed: ${err instanceof Error ? err.message : String(err)}`;
        // Mirror C# + Python: do NOT update filePath on failure — leave it
        // pointing at the last successful destination so the next Ctrl-S
        // doesn't repeat the failure against the same bad path.
        _setState({ status: msg, warnings: [..._getModel().warnings, msg] });
        return;
      }
      // Write succeeded — commit the new filePath and clear isDirty.
      _setState({ filePath: path, isDirty: false });
    })
    .build();

  const solveCmd = RelayCommand.builder()
    .predicate(() => _getModel().scenario !== undefined)
    .task(() => _triggerSolve())
    .build();

  // ── M3 Mutation helpers ──────────────────────────────────────────────────

  function addDecision(name?: string): void {
    const s = _requireScenario();
    const id = genId('d');
    const newDecision = { id, name: name ?? 'New decision' };
    _setState({
      scenario: { ...s, decisions: [...s.decisions, newDecision] },
      isDirty: true,
    });
    _triggerSolve();
  }

  function deleteDecision(id: string): void {
    const s = _requireScenario();
    const dec = s.decisions.find((d) => d.id === id);
    if (!dec) throw new ScenarioMutationError(`Decision '${id}' not found.`);

    // Cascade: collect all alternatives belonging to this decision
    const removedAltIds = new Set(
      s.alternatives.filter((a) => a.decisionId === id).map((a) => a.id),
    );

    const decisions = s.decisions.filter((d) => d.id !== id);
    const alternatives = s.alternatives.filter((a) => a.decisionId !== id);
    const coefficients = s.coefficients.filter((c) => !removedAltIds.has(c.alternativeId));
    const constraints = s.constraints.filter((c) => {
      if (c.kind === 'dependency') {
        return (
          !removedAltIds.has(c.sourceAlternativeId) && !removedAltIds.has(c.targetAlternativeId)
        );
      }
      if (c.kind === 'conflict') {
        return !removedAltIds.has(c.alternativeAId) && !removedAltIds.has(c.alternativeBId);
      }
      return true; // threshold constraints reference properties, not alternatives
    });

    _setState({
      scenario: { ...s, decisions, alternatives, coefficients, constraints },
      isDirty: true,
    });
    _triggerSolve();
  }

  function updateDecisionName(id: string, name: string): void {
    const s = _requireScenario();
    if (!s.decisions.some((d) => d.id === id)) {
      throw new ScenarioMutationError(`Decision '${id}' not found.`);
    }
    const decisions = s.decisions.map((d) => (d.id === id ? { ...d, name } : d));
    _setState({ scenario: { ...s, decisions }, isDirty: true });
    // Name change does not trigger solve (spec §3.3)
  }

  function addAlternative(decisionId: string, name?: string): void {
    const s = _requireScenario();
    if (!s.decisions.find((d) => d.id === decisionId)) {
      throw new ScenarioMutationError(`Decision '${decisionId}' not found.`);
    }
    const id = genId('a');
    const newAlt = { id, decisionId, name: name ?? 'New alternative' };
    // Add zero-fuzzy coefficients for every existing property
    const newCoefficients = s.properties.map((p) => ({
      alternativeId: id,
      propertyId: p.id,
      value: TriangularFuzzyM.zero(),
    }));
    _setState({
      scenario: {
        ...s,
        alternatives: [...s.alternatives, newAlt],
        coefficients: [...s.coefficients, ...newCoefficients],
      },
      isDirty: true,
    });
    _triggerSolve();
  }

  function deleteAlternative(id: string): void {
    const s = _requireScenario();
    if (!s.alternatives.find((a) => a.id === id)) {
      throw new ScenarioMutationError(`Alternative '${id}' not found.`);
    }

    const alternatives = s.alternatives.filter((a) => a.id !== id);
    const coefficients = s.coefficients.filter((c) => c.alternativeId !== id);
    const constraints = s.constraints.filter((c) => {
      if (c.kind === 'dependency') {
        return c.sourceAlternativeId !== id && c.targetAlternativeId !== id;
      }
      if (c.kind === 'conflict') {
        return c.alternativeAId !== id && c.alternativeBId !== id;
      }
      return true;
    });

    _setState({
      scenario: { ...s, alternatives, coefficients, constraints },
      isDirty: true,
    });
    _triggerSolve();
  }

  function updateAlternativeName(id: string, name: string): void {
    const s = _requireScenario();
    if (!s.alternatives.some((a) => a.id === id)) {
      throw new ScenarioMutationError(`Alternative '${id}' not found.`);
    }
    const alternatives = s.alternatives.map((a) => (a.id === id ? { ...a, name } : a));
    _setState({ scenario: { ...s, alternatives }, isDirty: true });
    // Name change doesn't trigger solve
  }

  function addProperty(name?: string, kind?: 'min' | 'max', weight?: number): void {
    const s = _requireScenario();
    // Schema $defs/Property.weight is exclusiveMinimum 0; match Python's
    // add_property guard and C# AddProperty's weight>0 check at the Add
    // boundary so a non-positive weight can't slip past the mutator into
    // the scenario only to fail at save-time schema validation. NaN <= 0
    // is false (JS NaN comparisons all are), so the >0 guard alone would
    // let NaN through and poison every downstream score — hence the
    // explicit finiteness check.
    if (weight !== undefined && (!Number.isFinite(weight) || weight <= 0)) {
      throw new ScenarioMutationError(`Property weight must be > 0 (got ${weight}).`);
    }
    const id = genId('p');
    const newProp = {
      id,
      name: name ?? 'New property',
      kind: kind ?? ('min' as const),
      weight: weight ?? 1,
    };
    // Add zero-fuzzy coefficients for every existing alternative
    const newCoefficients = s.alternatives.map((a) => ({
      alternativeId: a.id,
      propertyId: id,
      value: TriangularFuzzyM.zero(),
    }));
    _setState({
      scenario: {
        ...s,
        properties: [...s.properties, newProp],
        coefficients: [...s.coefficients, ...newCoefficients],
      },
      isDirty: true,
    });
    _triggerSolve();
  }

  function deleteProperty(id: string): void {
    const s = _requireScenario();
    if (!s.properties.find((p) => p.id === id)) {
      throw new ScenarioMutationError(`Property '${id}' not found.`);
    }

    const properties = s.properties.filter((p) => p.id !== id);
    const coefficients = s.coefficients.filter((c) => c.propertyId !== id);
    const constraints = s.constraints.filter((c) => {
      if (c.kind === 'threshold') {
        return c.propertyId !== id;
      }
      return true;
    });

    _setState({
      scenario: { ...s, properties, coefficients, constraints },
      isDirty: true,
    });
    _triggerSolve();
  }

  function _requireProperty(s: ScenarioM, id: string): void {
    if (!s.properties.some((p) => p.id === id)) {
      throw new ScenarioMutationError(`Property '${id}' not found.`);
    }
  }

  function updatePropertyName(id: string, name: string): void {
    const s = _requireScenario();
    _requireProperty(s, id);
    const properties = s.properties.map((p) => (p.id === id ? { ...p, name } : p));
    _setState({ scenario: { ...s, properties }, isDirty: true });
  }

  function updatePropertyKind(id: string, kind: 'min' | 'max'): void {
    const s = _requireScenario();
    _requireProperty(s, id);
    const properties = s.properties.map((p) => (p.id === id ? { ...p, kind } : p));
    _setState({ scenario: { ...s, properties }, isDirty: true });
    _triggerSolve();
  }

  function updatePropertyWeight(id: string, weight: number): void {
    const s = _requireScenario();
    // See addProperty: NaN must not slip past the >0 guard.
    if (!Number.isFinite(weight) || weight <= 0)
      throw new ScenarioMutationError(`Property weight must be > 0 (got ${weight}).`);
    _requireProperty(s, id);
    const properties = s.properties.map((p) => (p.id === id ? { ...p, weight } : p));
    _setState({ scenario: { ...s, properties }, isDirty: true });
    _triggerSolve();
  }

  function updateCoefficient(
    alternativeId: string,
    propertyId: string,
    lower: number,
    modal: number,
    upper: number,
  ): void {
    const s = _requireScenario();
    if (!s.alternatives.some((a) => a.id === alternativeId)) {
      throw new ScenarioMutationError(`Alternative '${alternativeId}' not found.`);
    }
    if (!s.properties.some((p) => p.id === propertyId)) {
      throw new ScenarioMutationError(`Property '${propertyId}' not found.`);
    }
    // JSON cannot encode NaN/Infinity, so a non-finite component would
    // solve "successfully" into NaN scores and then fail at save time.
    if (!Number.isFinite(lower) || !Number.isFinite(modal) || !Number.isFinite(upper)) {
      throw new ScenarioMutationError(
        `Coefficient components must be finite (got ${lower}, ${modal}, ${upper}).`,
      );
    }
    // Drop any prior ordering warning for this (alt, prop) pair before
    // deciding whether to emit one — without this, the warning persists
    // forever even after the user edits the cell back into shape.
    const stalePrefix = `Coefficient (${alternativeId}, ${propertyId}): ordering`;
    const warnings: string[] = _getModel().warnings.filter((w) => !w.startsWith(stalePrefix));
    if (lower > modal || modal > upper) {
      warnings.push(
        `Coefficient (${alternativeId}, ${propertyId}): ordering violated lower=${lower} modal=${modal} upper=${upper}`,
      );
    }
    const coefficients = s.coefficients.map((c) =>
      c.alternativeId === alternativeId && c.propertyId === propertyId
        ? { ...c, value: new TriangularFuzzyM(lower, modal, upper) }
        : c,
    );
    _setState({ scenario: { ...s, coefficients }, isDirty: true, warnings });
    _triggerSolve();
  }

  // Enforces the same load-time invariants the schema and spec/domain/invariants.md
  // require, but at mutation time — so an Add/Update path can't produce a
  // scenario that loads with a schema violation on the very next round-trip.
  // Mirrors the Python ScenarioVM checks added in the same commit family.
  function _validateConstraint(s: ScenarioM, c: ConstraintM): void {
    if (c.kind === 'threshold') {
      if (!s.properties.some((p) => p.id === c.propertyId)) {
        throw new ScenarioMutationError(`Property '${c.propertyId}' not found.`);
      }
      if (c.min === undefined && c.max === undefined) {
        throw new ScenarioMutationError('ThresholdConstraint requires at least one of min or max.');
      }
      // Invariant 6.2: min ≤ max is FATAL. Match Python+C# + loader.
      if (c.min !== undefined && c.max !== undefined && c.min > c.max) {
        throw new ScenarioMutationError(
          `Threshold constraint min (${c.min}) must be ≤ max (${c.max}).`,
        );
      }
      return;
    }
    const altIds = new Set(s.alternatives.map((a) => a.id));
    if (c.kind === 'dependency') {
      if (!altIds.has(c.sourceAlternativeId)) {
        throw new ScenarioMutationError(`Alternative '${c.sourceAlternativeId}' not found.`);
      }
      if (!altIds.has(c.targetAlternativeId)) {
        throw new ScenarioMutationError(`Alternative '${c.targetAlternativeId}' not found.`);
      }
      if (c.sourceAlternativeId === c.targetAlternativeId) {
        throw new ScenarioMutationError(
          'Self-edge on dependency constraint (source must differ from target).',
        );
      }
      return;
    }
    // conflict
    if (!altIds.has(c.alternativeAId)) {
      throw new ScenarioMutationError(`Alternative '${c.alternativeAId}' not found.`);
    }
    if (!altIds.has(c.alternativeBId)) {
      throw new ScenarioMutationError(`Alternative '${c.alternativeBId}' not found.`);
    }
    if (c.alternativeAId === c.alternativeBId) {
      throw new ScenarioMutationError(
        'Self-edge on conflict constraint (alternativeA must differ from alternativeB).',
      );
    }
  }

  function addConstraint(c: ConstraintM): void {
    const s = _requireScenario();
    _validateConstraint(s, c);
    _setState({
      scenario: { ...s, constraints: [...s.constraints, c] },
      isDirty: true,
    });
    _triggerSolve();
  }

  function deleteConstraint(index: number): void {
    const s = _requireScenario();
    if (index < 0 || index >= s.constraints.length) {
      throw new ScenarioMutationError(`Constraint index ${index} out of range.`);
    }
    const constraints = s.constraints.filter((_, i) => i !== index);
    _setState({ scenario: { ...s, constraints }, isDirty: true });
    _triggerSolve();
  }

  function updateConstraint(index: number, c: ConstraintM): void {
    const s = _requireScenario();
    if (index < 0 || index >= s.constraints.length) {
      throw new ScenarioMutationError(`Constraint index ${index} out of range.`);
    }
    // spec/viewmodels.md §5.5 mandates `update*Constraint` preserve the
    // existing kind at `index`; Python+C# typed surfaces assert this. Without
    // the guard, a generic-surface caller could flip threshold→dependency at
    // a global index, breaking the CriticalConstraintM.constraintIndex
    // invariant the typed surfaces uphold.
    const existing = s.constraints[index];
    if (existing.kind !== c.kind) {
      throw new ScenarioMutationError(
        `Constraint at index ${index} is a ${existing.kind} constraint; cannot replace with a ${c.kind} constraint.`,
      );
    }
    _validateConstraint(s, c);
    const constraints = s.constraints.map((old, i) => (i === index ? c : old));
    _setState({ scenario: { ...s, constraints }, isDirty: true });
    _triggerSolve();
  }

  function updateScenarioName(name: string): void {
    const s = _requireScenario();
    _setState({ scenario: { ...s, name }, isDirty: true });
    // Name change does not trigger solve
  }

  function _browserMarkSaved(filePath?: string): void {
    const patch: Partial<ScenarioState> = { isDirty: false };
    if (filePath !== undefined) patch.filePath = filePath;
    _setState(patch);
  }

  function _browserOpen(raw: unknown, fileName: string): void {
    try {
      const scenario = loadScenarioFromParsed(raw, inlinedSchema as object);
      const result = _runSolve(scenario);
      const selectedCandidateIndex = result.candidates.length > 0 ? 0 : null;
      _setState({
        scenario,
        filePath: fileName,
        isDirty: false,
        warnings: scenario.warnings,
        selectedCandidateIndex,
        ...result,
      });
    } catch (err) {
      const msg = err instanceof Error ? err.message : String(err);
      _setState({
        warnings: [..._getModel().warnings, `Open failed: ${msg}`],
        status: `Open failed: ${msg}`,
      });
    }
  }

  function setSelectedCandidateIndex(index: number | null): void {
    const { candidates } = _getModel();
    if (index !== null && (index < 0 || index >= candidates.length)) {
      throw new ScenarioMutationError(
        `Candidate index ${index} out of range (0–${candidates.length - 1}).`,
      );
    }
    _setState({ selectedCandidateIndex: index });
  }

  // ── Build the ComponentVMOf ───────────────────────────────────────────────

  const vm = ComponentVMOf.builder<ScenarioState>()
    .name('scenario-vm')
    .model(_emptyState())
    .services(hub, NullDispatcher.INSTANCE)
    .modeledHinter((m) => m.scenario?.name ?? '(no scenario)')
    .build();

  vm.construct();
  _vm = vm;

  // Attach commands and M3 mutations as own properties
  Object.defineProperties(vm, {
    newCmd: { value: newCmd, writable: false, enumerable: true, configurable: false },
    openCmd: { value: openCmd, writable: false, enumerable: true, configurable: false },
    saveCmd: { value: saveCmd, writable: false, enumerable: true, configurable: false },
    saveAsCmd: { value: saveAsCmd, writable: false, enumerable: true, configurable: false },
    solveCmd: { value: solveCmd, writable: false, enumerable: true, configurable: false },
    addDecision: { value: addDecision, writable: false, enumerable: true, configurable: false },
    deleteDecision: {
      value: deleteDecision,
      writable: false,
      enumerable: true,
      configurable: false,
    },
    updateDecisionName: {
      value: updateDecisionName,
      writable: false,
      enumerable: true,
      configurable: false,
    },
    addAlternative: {
      value: addAlternative,
      writable: false,
      enumerable: true,
      configurable: false,
    },
    deleteAlternative: {
      value: deleteAlternative,
      writable: false,
      enumerable: true,
      configurable: false,
    },
    updateAlternativeName: {
      value: updateAlternativeName,
      writable: false,
      enumerable: true,
      configurable: false,
    },
    addProperty: { value: addProperty, writable: false, enumerable: true, configurable: false },
    deleteProperty: {
      value: deleteProperty,
      writable: false,
      enumerable: true,
      configurable: false,
    },
    updatePropertyName: {
      value: updatePropertyName,
      writable: false,
      enumerable: true,
      configurable: false,
    },
    updatePropertyKind: {
      value: updatePropertyKind,
      writable: false,
      enumerable: true,
      configurable: false,
    },
    updatePropertyWeight: {
      value: updatePropertyWeight,
      writable: false,
      enumerable: true,
      configurable: false,
    },
    updateCoefficient: {
      value: updateCoefficient,
      writable: false,
      enumerable: true,
      configurable: false,
    },
    addConstraint: { value: addConstraint, writable: false, enumerable: true, configurable: false },
    deleteConstraint: {
      value: deleteConstraint,
      writable: false,
      enumerable: true,
      configurable: false,
    },
    updateConstraint: {
      value: updateConstraint,
      writable: false,
      enumerable: true,
      configurable: false,
    },
    updateScenarioName: {
      value: updateScenarioName,
      writable: false,
      enumerable: true,
      configurable: false,
    },
    setSelectedCandidateIndex: {
      value: setSelectedCandidateIndex,
      writable: false,
      enumerable: true,
      configurable: false,
    },
    _browserOpen: {
      value: _browserOpen,
      writable: false,
      enumerable: true,
      configurable: false,
    },
    _browserMarkSaved: {
      value: _browserMarkSaved,
      writable: false,
      enumerable: true,
      configurable: false,
    },
  });

  return vm as ScenarioVM;
}
