/**
 * Load and validate a scenario JSON file → ScenarioM.
 *
 * Path from this file to the repo root:
 *   scenario-loader.ts → models(0) → src(1) → typescript(2) → langs(3) → root(4)
 */
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';
import Ajv from 'ajv/dist/2020.js';
import type { ScenarioM } from './scenario.js';
import type { DecisionM } from './decision.js';
import type { AlternativeM } from './alternative.js';
import type { PropertyM } from './property.js';
import { TriangularFuzzyM } from './triangular-fuzzy.js';
import type { CoefficientM } from './coefficient.js';
import type { ConstraintM } from './constraint.js';
import type { ConfigM } from './config.js';

// ---------------------------------------------------------------------------
// Custom error type
// ---------------------------------------------------------------------------
export class ScenarioValidationError extends Error {
  constructor(
    message: string,
    public readonly errors: string[],
  ) {
    super(message);
    this.name = 'ScenarioValidationError';
  }
}

// ---------------------------------------------------------------------------
// Schema path discovery
// ---------------------------------------------------------------------------
function findRepoRoot(startDir: string): string {
  let dir = startDir;
  while (true) {
    if (fs.existsSync(path.join(dir, '.git'))) return dir;
    const parent = path.dirname(dir);
    if (parent === dir) throw new Error('Could not find repo root (.git) from ' + startDir);
    dir = parent;
  }
}

// Schema path discovery is LAZY — must not run at module-init time, because the
// browser shims of fileURLToPath / fs throw on call. The loader is only invoked
// when openCmd actually fires; in the browser it's served the schema via a
// separate channel (the Toolbar reads the file in browser and calls
// loadScenarioFromParsed instead).
let _defaultSchemaPathCache: string | null = null;
function _defaultSchemaPath(): string {
  if (_defaultSchemaPathCache !== null) return _defaultSchemaPathCache;
  const __filename = fileURLToPath(import.meta.url);
  const __dirname = path.dirname(__filename);
  const repoRoot = findRepoRoot(__dirname);
  _defaultSchemaPathCache = path.join(repoRoot, 'spec', 'domain', 'scenario.schema.json');
  return _defaultSchemaPathCache;
}

// ---------------------------------------------------------------------------
// Loader
// ---------------------------------------------------------------------------
// eslint-disable-next-line @typescript-eslint/no-explicit-any
type RawJson = Record<string, any>;

/**
 * Parse, validate, and construct a ScenarioM from an already-parsed JSON
 * object. This is the browser-safe path — no filesystem access required.
 *
 * @param raw          The parsed JSON object (unknown type, validated by AJV).
 * @param inlineSchema Optional pre-loaded schema object. When provided, AJV
 *                     uses it directly and no fs access is performed for the
 *                     schema. Required in browser builds where the schema
 *                     cannot be resolved via the filesystem.
 */
export function loadScenarioFromParsed(raw: unknown, inlineSchema?: object): ScenarioM {
  // Resolve schema: use inline if provided, otherwise read from filesystem.
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  let schema: Record<string, any>;
  if (inlineSchema !== undefined) {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    schema = inlineSchema as Record<string, any>;
  } else {
    const schemaPath = _defaultSchemaPath();
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    schema = JSON.parse(fs.readFileSync(schemaPath, 'utf-8')) as Record<string, any>;
  }

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const rawObj = raw as Record<string, any>;

  // ------------------------------------------------------------------ //
  // JSON Schema structural validation via ajv
  // ------------------------------------------------------------------ //
  const ajv = new Ajv({ strict: false });
  const validate = ajv.compile(schema);
  if (!validate(rawObj)) {
    const msgs = (validate.errors ?? []).slice(0, 5).map((e) => `${e.instancePath} ${e.message}`);
    throw new ScenarioValidationError(`JSON Schema validation failed: ${msgs.join('; ')}`, msgs);
  }

  const warnings: string[] = [];
  return _parseRaw(rawObj, warnings);
}

export function loadScenario(jsonPath: string, schemaPath?: string): ScenarioM {
  const resolvedSchema = schemaPath ?? _defaultSchemaPath();

  const raw: RawJson = JSON.parse(fs.readFileSync(jsonPath, 'utf-8'));
  const schema: RawJson = JSON.parse(fs.readFileSync(resolvedSchema, 'utf-8'));

  // ------------------------------------------------------------------ //
  // JSON Schema structural validation via ajv
  // ------------------------------------------------------------------ //
  const ajv = new Ajv({ strict: false });
  const validate = ajv.compile(schema);
  if (!validate(raw)) {
    const msgs = (validate.errors ?? []).slice(0, 5).map((e) => `${e.instancePath} ${e.message}`);
    throw new ScenarioValidationError(`JSON Schema validation failed: ${msgs.join('; ')}`, msgs);
  }

  const warnings: string[] = [];
  return _parseRaw(raw, warnings);
}

// eslint-disable-next-line @typescript-eslint/no-explicit-any
function _parseRaw(raw: Record<string, any>, warnings: string[]): ScenarioM {
  // ------------------------------------------------------------------ //
  // Parse raw objects
  // ------------------------------------------------------------------ //
  const decisions: DecisionM[] = (raw['decisions'] as RawJson[]).map((d) => ({
    id: d['id'] as string,
    name: d['name'] as string,
  }));

  const alternatives: AlternativeM[] = (raw['alternatives'] as RawJson[]).map((a) => ({
    id: a['id'] as string,
    decisionId: a['decisionId'] as string,
    name: a['name'] as string,
  }));

  const properties: PropertyM[] = (raw['properties'] as RawJson[]).map((p) => ({
    id: p['id'] as string,
    name: p['name'] as string,
    kind: p['kind'] as 'min' | 'max',
    weight: p['weight'] as number,
  }));

  const coefficients: CoefficientM[] = (raw['coefficients'] as RawJson[]).map((c) => ({
    alternativeId: c['alternativeId'] as string,
    propertyId: c['propertyId'] as string,
    value: new TriangularFuzzyM(
      c['value']['lower'] as number,
      c['value']['modal'] as number,
      c['value']['upper'] as number,
    ),
  }));

  const constraints: ConstraintM[] = (raw['constraints'] as RawJson[]).map((c) => {
    if (c['kind'] === 'threshold') {
      return {
        kind: 'threshold' as const,
        propertyId: c['propertyId'] as string,
        min: c['min'] as number | undefined,
        max: c['max'] as number | undefined,
      };
    } else if (c['kind'] === 'dependency') {
      return {
        kind: 'dependency' as const,
        sourceAlternativeId: c['sourceAlternativeId'] as string,
        targetAlternativeId: c['targetAlternativeId'] as string,
      };
    } else {
      return {
        kind: 'conflict' as const,
        alternativeAId: c['alternativeAId'] as string,
        alternativeBId: c['alternativeBId'] as string,
      };
    }
  });

  const wRaw = (raw['config'] as RawJson)['weights'] as RawJson;
  const config: ConfigM = {
    aggregation: (raw['config'] as RawJson)['aggregation'] as 'sum' | 'max',
    weights: {
      positive: wRaw['positive'] as number,
      average: wRaw['average'] as number,
      negative: wRaw['negative'] as number,
    },
  };

  // ------------------------------------------------------------------ //
  // Invariant 1: Identifier uniqueness (fatal)
  // ------------------------------------------------------------------ //
  const decIds = decisions.map((d) => d.id);
  const altIds = alternatives.map((a) => a.id);
  const propIds = properties.map((p) => p.id);

  if (new Set(decIds).size !== decIds.length) {
    throw new ScenarioValidationError('Invariant 1.1: duplicate decision id(s)', [
      'duplicate decision id(s)',
    ]);
  }
  if (new Set(altIds).size !== altIds.length) {
    throw new ScenarioValidationError('Invariant 1.2: duplicate alternative id(s)', [
      'duplicate alternative id(s)',
    ]);
  }
  if (new Set(propIds).size !== propIds.length) {
    throw new ScenarioValidationError('Invariant 1.3: duplicate property id(s)', [
      'duplicate property id(s)',
    ]);
  }

  const decIdSet = new Set(decIds);
  const altIdSet = new Set(altIds);
  const propIdSet = new Set(propIds);

  const overlapDA = decIds.filter((id) => altIdSet.has(id));
  const overlapDP = decIds.filter((id) => propIdSet.has(id));
  const overlapAP = altIds.filter((id) => propIdSet.has(id));
  if (overlapDA.length || overlapDP.length || overlapAP.length) {
    throw new ScenarioValidationError(
      `Invariant 1.4: id namespace collision: d∩a=${JSON.stringify(overlapDA)}, d∩p=${JSON.stringify(overlapDP)}, a∩p=${JSON.stringify(overlapAP)}`,
      ['id namespace collision'],
    );
  }

  // ------------------------------------------------------------------ //
  // Invariant 2: Cross-reference validity (fatal)
  // ------------------------------------------------------------------ //
  for (const a of alternatives) {
    if (!decIdSet.has(a.decisionId)) {
      throw new ScenarioValidationError(
        `Invariant 2.1: alternative '${a.id}' references unknown decision '${a.decisionId}'`,
        [`alternative '${a.id}' references unknown decision '${a.decisionId}'`],
      );
    }
  }
  for (const c of coefficients) {
    if (!altIdSet.has(c.alternativeId)) {
      throw new ScenarioValidationError(
        `Invariant 2.2: coefficient references unknown alternative '${c.alternativeId}'`,
        [`coefficient references unknown alternative '${c.alternativeId}'`],
      );
    }
    if (!propIdSet.has(c.propertyId)) {
      throw new ScenarioValidationError(
        `Invariant 2.3: coefficient references unknown property '${c.propertyId}'`,
        [`coefficient references unknown property '${c.propertyId}'`],
      );
    }
  }
  for (const con of constraints) {
    if (con.kind === 'threshold') {
      if (!propIdSet.has(con.propertyId)) {
        throw new ScenarioValidationError(
          `Invariant 2.4: threshold constraint references unknown property '${con.propertyId}'`,
          [`threshold constraint references unknown property '${con.propertyId}'`],
        );
      }
    } else if (con.kind === 'dependency') {
      if (!altIdSet.has(con.sourceAlternativeId)) {
        throw new ScenarioValidationError(
          `Invariant 2.5: dependency constraint references unknown source '${con.sourceAlternativeId}'`,
          [`dependency constraint references unknown source '${con.sourceAlternativeId}'`],
        );
      }
      if (!altIdSet.has(con.targetAlternativeId)) {
        throw new ScenarioValidationError(
          `Invariant 2.5: dependency constraint references unknown target '${con.targetAlternativeId}'`,
          [`dependency constraint references unknown target '${con.targetAlternativeId}'`],
        );
      }
    } else {
      // conflict
      if (!altIdSet.has(con.alternativeAId)) {
        throw new ScenarioValidationError(
          `Invariant 2.5: conflict constraint references unknown alternative A '${con.alternativeAId}'`,
          [`conflict constraint references unknown alternative A '${con.alternativeAId}'`],
        );
      }
      if (!altIdSet.has(con.alternativeBId)) {
        throw new ScenarioValidationError(
          `Invariant 2.5: conflict constraint references unknown alternative B '${con.alternativeBId}'`,
          [`conflict constraint references unknown alternative B '${con.alternativeBId}'`],
        );
      }
    }
  }

  // ------------------------------------------------------------------ //
  // Invariant 3: Coefficient completeness (fatal)
  // ------------------------------------------------------------------ //
  const coeffPairs = coefficients.map((c) => `${c.alternativeId}|${c.propertyId}`);
  const coeffPairSet = new Set(coeffPairs);
  if (coeffPairs.length !== coeffPairSet.size) {
    throw new ScenarioValidationError(
      'Invariant 3.1: duplicate (alternativeId, propertyId) coefficient',
      ['duplicate (alternativeId, propertyId) coefficient'],
    );
  }
  for (const aId of altIds) {
    for (const pId of propIds) {
      if (!coeffPairSet.has(`${aId}|${pId}`)) {
        throw new ScenarioValidationError(
          `Invariant 3.1: missing coefficient for (alternative=${aId}, property=${pId})`,
          [`missing coefficient for (alternative=${aId}, property=${pId})`],
        );
      }
    }
  }

  // ------------------------------------------------------------------ //
  // Invariant 4: Triangular ordering (warning)
  // ------------------------------------------------------------------ //
  for (const c of coefficients) {
    const v = c.value;
    if (!(v.lower <= v.modal && v.modal <= v.upper)) {
      warnings.push(
        `Invariant 4.1: coefficient (${c.alternativeId}, ${c.propertyId}) has lower=${v.lower} modal=${v.modal} upper=${v.upper} — ordering violated`,
      );
    }
  }

  // ------------------------------------------------------------------ //
  // Invariant 5: Weights (fatal)
  // ------------------------------------------------------------------ //
  const w = config.weights;
  for (const [val, label] of [
    [w.positive, 'positive'],
    [w.average, 'average'],
    [w.negative, 'negative'],
  ] as [number, string][]) {
    if (val < 0 || val > 1) {
      throw new ScenarioValidationError(`Invariant 5.2: weight.${label}=${val} is outside [0, 1]`, [
        `weight.${label}=${val} is outside [0, 1]`,
      ]);
    }
  }
  const wSum = w.positive + w.average + w.negative;
  if (Math.abs(wSum - 1.0) > 1e-9) {
    throw new ScenarioValidationError(
      `Invariant 5.1: weights sum to ${wSum}, expected 1.0 (tolerance 1e-9)`,
      [`weights sum to ${wSum}, expected 1.0`],
    );
  }

  // ------------------------------------------------------------------ //
  // Invariant 6: Threshold constraints (fatal)
  // ------------------------------------------------------------------ //
  for (let i = 0; i < constraints.length; i++) {
    const con = constraints[i];
    if (con.kind === 'threshold') {
      if (con.min === undefined && con.max === undefined) {
        throw new ScenarioValidationError(
          `Invariant 6.1: threshold constraint[${i}] has neither min nor max`,
          [`threshold constraint[${i}] has neither min nor max`],
        );
      }
      if (con.min !== undefined && con.max !== undefined && con.min > con.max) {
        throw new ScenarioValidationError(
          `Invariant 6.2: threshold constraint[${i}] has min=${con.min} > max=${con.max}`,
          [`threshold constraint[${i}] has min=${con.min} > max=${con.max}`],
        );
      }
    }
  }

  // ------------------------------------------------------------------ //
  // Invariant 7: Dependency / conflict self-edges (fatal + warning)
  // ------------------------------------------------------------------ //
  const decOf: Map<string, string> = new Map(alternatives.map((a) => [a.id, a.decisionId]));
  for (let i = 0; i < constraints.length; i++) {
    const con = constraints[i];
    if (con.kind === 'dependency') {
      if (con.sourceAlternativeId === con.targetAlternativeId) {
        throw new ScenarioValidationError(
          `Invariant 7.1: dependency constraint[${i}] is a self-edge`,
          [`dependency constraint[${i}] is a self-edge`],
        );
      }
      if (decOf.get(con.sourceAlternativeId) === decOf.get(con.targetAlternativeId)) {
        warnings.push(
          `Invariant 7.2: dependency constraint[${i}] connects alternatives of the same decision — rarely meaningful`,
        );
      }
    } else if (con.kind === 'conflict') {
      if (con.alternativeAId === con.alternativeBId) {
        throw new ScenarioValidationError(
          `Invariant 7.1: conflict constraint[${i}] is a self-edge`,
          [`conflict constraint[${i}] is a self-edge`],
        );
      }
      if (decOf.get(con.alternativeAId) === decOf.get(con.alternativeBId)) {
        warnings.push(
          `Invariant 7.2: conflict constraint[${i}] connects alternatives of the same decision — rarely meaningful`,
        );
      }
    }
  }

  // ------------------------------------------------------------------ //
  // Invariant 8: Decision occupancy (fatal)
  // ------------------------------------------------------------------ //
  const altsByDec: Map<string, string[]> = new Map(decisions.map((d) => [d.id, []]));
  for (const a of alternatives) {
    altsByDec.get(a.decisionId)!.push(a.id);
  }
  for (const d of decisions) {
    if (!altsByDec.get(d.id)?.length) {
      throw new ScenarioValidationError(
        `Invariant 8.1: decision '${d.id}' ('${d.name}') has no alternatives`,
        [`decision '${d.id}' has no alternatives`],
      );
    }
  }

  return {
    schemaVersion: raw['schemaVersion'] as string,
    name: raw['name'] as string,
    description: (raw['description'] as string | undefined) ?? '',
    decisions,
    alternatives,
    properties,
    coefficients,
    constraints,
    config,
    warnings,
  };
}
