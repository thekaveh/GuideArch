/**
 * Conformance runner: for each spec/conformance/scenarios/*.json,
 * load, solve, run both analyses, compare against expected outputs.
 *
 * Exit 0 on pass, non-zero on divergence.
 */
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';
import { loadScenario } from '../models/scenario-loader.js';
import { solve } from '../models/topsis/solve.js';
import { criticalDecisions } from '../models/topsis/critical-decisions.js';
import { criticalConstraints } from '../models/topsis/critical-constraints.js';
import type { CandidateM } from '../models/candidate.js';
import type { CriticalDecisionM } from '../models/critical-decision.js';
import type { CriticalConstraintM } from '../models/critical-constraint.js';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

function findRepoRoot(startDir: string): string {
  let dir = startDir;
  while (true) {
    if (fs.existsSync(path.join(dir, '.git'))) return dir;
    const parent = path.dirname(dir);
    if (parent === dir) throw new Error('Could not find repo root (.git) from ' + startDir);
    dir = parent;
  }
}

const REPO_ROOT = findRepoRoot(__dirname);
const SCENARIOS_DIR = path.join(REPO_ROOT, 'spec', 'conformance', 'scenarios');
const EXPECTED_DIR = path.join(REPO_ROOT, 'spec', 'conformance', 'expected');
const TOLERANCES_PATH = path.join(REPO_ROOT, 'spec', 'conformance', 'tolerances.json');

// eslint-disable-next-line @typescript-eslint/no-explicit-any
const tolerances: any = JSON.parse(fs.readFileSync(TOLERANCES_PATH, 'utf-8'));
const SCALAR_TOL: number = tolerances.candidates.score.absolute as number; // 1e-9

// ---------------------------------------------------------------------------
// Comparison helpers
// ---------------------------------------------------------------------------
interface Divergence {
  field: string;
  expected: unknown;
  actual: unknown;
  diff?: number;
}

function closeEnough(a: number, b: number): boolean {
  return Math.abs(a - b) <= SCALAR_TOL;
}

function compareCandidates(
  actual: CandidateM[],
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  expected: any[],
  label: string,
): Divergence[] {
  const divs: Divergence[] = [];

  if (actual.length !== expected.length) {
    divs.push({
      field: `${label}.length`,
      expected: expected.length,
      actual: actual.length,
    });
    return divs;
  }

  for (let i = 0; i < actual.length; i++) {
    const a = actual[i];
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const e = expected[i] as any;
    const prefix = `${label}[${i}]`;

    // alternativeIds
    const eIds = e.alternativeIds as string[];
    if (a.alternativeIds.length !== eIds.length) {
      divs.push({
        field: `${prefix}.alternativeIds.length`,
        expected: eIds.length,
        actual: a.alternativeIds.length,
      });
    } else {
      for (let j = 0; j < eIds.length; j++) {
        if (a.alternativeIds[j] !== eIds[j]) {
          divs.push({
            field: `${prefix}.alternativeIds[${j}]`,
            expected: eIds[j],
            actual: a.alternativeIds[j],
          });
        }
      }
    }

    // triangularValue
    for (const k of ['lower', 'modal', 'upper'] as const) {
      const av = a.triangularValue[k];
      const ev = e.triangularValue[k] as number;
      if (!closeEnough(av, ev)) {
        divs.push({
          field: `${prefix}.triangularValue.${k}`,
          expected: ev,
          actual: av,
          diff: Math.abs(av - ev),
        });
      }
    }

    // normalizedValue
    for (const k of ['positive', 'average', 'negative'] as const) {
      const av = a.normalizedValue[k];
      const ev = e.normalizedValue[k] as number;
      if (!closeEnough(av, ev)) {
        divs.push({
          field: `${prefix}.normalizedValue.${k}`,
          expected: ev,
          actual: av,
          diff: Math.abs(av - ev),
        });
      }
    }

    // score
    if (!closeEnough(a.score, e.score as number)) {
      divs.push({
        field: `${prefix}.score`,
        expected: e.score,
        actual: a.score,
        diff: Math.abs(a.score - (e.score as number)),
      });
    }

    // rank (exact)
    if (a.rank !== (e.rank as number)) {
      divs.push({ field: `${prefix}.rank`, expected: e.rank, actual: a.rank });
    }
  }
  return divs;
}

function compareCriticalDecisions(
  actual: CriticalDecisionM[],
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  expected: any[],
  label: string,
): Divergence[] {
  const divs: Divergence[] = [];

  if (actual.length !== expected.length) {
    divs.push({ field: `${label}.length`, expected: expected.length, actual: actual.length });
    return divs;
  }

  for (let i = 0; i < actual.length; i++) {
    const a = actual[i];
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const e = expected[i] as any;
    const prefix = `${label}[${i}]`;

    if (a.decisionId !== (e.decisionId as string)) {
      divs.push({ field: `${prefix}.decisionId`, expected: e.decisionId, actual: a.decisionId });
    }
    for (const k of ['lower', 'modal', 'upper'] as const) {
      const av = a.triangularValue[k];
      const ev = e.triangularValue[k] as number;
      if (!closeEnough(av, ev)) {
        divs.push({
          field: `${prefix}.triangularValue.${k}`,
          expected: ev,
          actual: av,
          diff: Math.abs(av - ev),
        });
      }
    }
    for (const k of ['positive', 'average', 'negative'] as const) {
      const av = a.normalizedValue[k];
      const ev = e.normalizedValue[k] as number;
      if (!closeEnough(av, ev)) {
        divs.push({
          field: `${prefix}.normalizedValue.${k}`,
          expected: ev,
          actual: av,
          diff: Math.abs(av - ev),
        });
      }
    }
    if (!closeEnough(a.score, e.score as number)) {
      divs.push({
        field: `${prefix}.score`,
        expected: e.score,
        actual: a.score,
        diff: Math.abs(a.score - (e.score as number)),
      });
    }
    if (a.rank !== (e.rank as number)) {
      divs.push({ field: `${prefix}.rank`, expected: e.rank, actual: a.rank });
    }
  }
  return divs;
}

function compareCriticalConstraints(
  actual: CriticalConstraintM[],
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  expected: any[],
  label: string,
): Divergence[] {
  const divs: Divergence[] = [];

  if (actual.length !== expected.length) {
    divs.push({ field: `${label}.length`, expected: expected.length, actual: actual.length });
    return divs;
  }

  for (let i = 0; i < actual.length; i++) {
    const a = actual[i];
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const e = expected[i] as any;
    const prefix = `${label}[${i}]`;

    if (a.constraintIndex !== (e.constraintIndex as number)) {
      divs.push({
        field: `${prefix}.constraintIndex`,
        expected: e.constraintIndex,
        actual: a.constraintIndex,
      });
    }
    if (a.kind !== (e.kind as string)) {
      divs.push({ field: `${prefix}.kind`, expected: e.kind, actual: a.kind });
    }
    if (a.eliminated !== (e.eliminated as number)) {
      divs.push({ field: `${prefix}.eliminated`, expected: e.eliminated, actual: a.eliminated });
    }
    if (a.total !== (e.total as number)) {
      divs.push({ field: `${prefix}.total`, expected: e.total, actual: a.total });
    }
    if (a.redundant !== (e.redundant as boolean)) {
      divs.push({ field: `${prefix}.redundant`, expected: e.redundant, actual: a.redundant });
    }
  }
  return divs;
}

// ---------------------------------------------------------------------------
// Main runner
// ---------------------------------------------------------------------------
export function runConformance(): { passed: number; failed: number; divergences: Divergence[][] } {
  const scenarioFiles = fs
    .readdirSync(SCENARIOS_DIR)
    .filter((f) => f.endsWith('.json'))
    .sort();

  let passed = 0;
  let failed = 0;
  const allDivergences: Divergence[][] = [];

  for (const file of scenarioFiles) {
    const name = path.basename(file, '.json'); // e.g. "sas"
    const scenarioPath = path.join(SCENARIOS_DIR, file);

    console.log(`\n--- ${name} ---`);

    let scenario;
    try {
      scenario = loadScenario(scenarioPath);
    } catch (e) {
      console.error(`  FAIL: loadScenario threw: ${e}`);
      failed++;
      allDivergences.push([{ field: 'loadScenario', expected: 'success', actual: String(e) }]);
      continue;
    }

    const candidates = solve(scenario);
    const critDecs = criticalDecisions(scenario, candidates);
    const critConsts = criticalConstraints(scenario);

    const divs: Divergence[] = [];

    // Compare candidates
    const expectedCandidatesPath = path.join(EXPECTED_DIR, `${name}.candidates.json`);
    if (fs.existsSync(expectedCandidatesPath)) {
      const expectedCandidates = JSON.parse(fs.readFileSync(expectedCandidatesPath, 'utf-8'));
      divs.push(...compareCandidates(candidates, expectedCandidates, `${name}.candidates`));
    }

    // Compare critical decisions
    const expectedCritDecsPath = path.join(EXPECTED_DIR, `${name}.critical-decisions.json`);
    if (fs.existsSync(expectedCritDecsPath)) {
      const expectedCritDecs = JSON.parse(fs.readFileSync(expectedCritDecsPath, 'utf-8'));
      divs.push(
        ...compareCriticalDecisions(critDecs, expectedCritDecs, `${name}.critical-decisions`),
      );
    }

    // Compare critical constraints
    const expectedCritConstsPath = path.join(EXPECTED_DIR, `${name}.critical-constraints.json`);
    if (fs.existsSync(expectedCritConstsPath)) {
      const expectedCritConsts = JSON.parse(fs.readFileSync(expectedCritConstsPath, 'utf-8'));
      divs.push(
        ...compareCriticalConstraints(
          critConsts,
          expectedCritConsts,
          `${name}.critical-constraints`,
        ),
      );
    }

    if (divs.length === 0) {
      console.log(`  PASS`);
      passed++;
    } else {
      console.error(`  FAIL: ${divs.length} divergence(s)`);
      for (const d of divs) {
        if (d.diff !== undefined) {
          console.error(
            `    ${d.field}: expected=${d.expected}, actual=${d.actual}, diff=${d.diff}`,
          );
        } else {
          console.error(
            `    ${d.field}: expected=${JSON.stringify(d.expected)}, actual=${JSON.stringify(d.actual)}`,
          );
        }
      }
      failed++;
      allDivergences.push(divs);
    }
  }

  console.log(`\nResults: ${passed} passed, ${failed} failed`);
  return { passed, failed, divergences: allDivergences };
}

// ---------------------------------------------------------------------------
// CLI entry point — only runs when this file is executed directly
// ---------------------------------------------------------------------------
// Detect if this file is the entry point (not imported as a module)
const isMain =
  typeof process !== 'undefined' &&
  // In ESM, import.meta.url is the canonical way; for tsx/node direct execution
  // process.argv[1] will match this file's path
  process.argv[1] !== undefined &&
  (process.argv[1].endsWith('runner.ts') ||
    process.argv[1].endsWith('runner.js') ||
    process.argv[1].includes('conformance/runner'));

if (isMain) {
  const { failed: exitCode } = runConformance();
  process.exit(exitCode > 0 ? 1 : 0);
}
