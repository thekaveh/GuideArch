/**
 * TOPSIS solver — implements topsis.md §3 step-by-step.
 *
 * Every numbered comment maps to the corresponding §3.x step.
 */
import type { ScenarioM } from '../scenario.js';
import type { CandidateM } from '../candidate.js';
import type { NormalizedFuzzyM } from '../normalized-fuzzy.js';
import { TriangularFuzzyM } from '../triangular-fuzzy.js';

// ---------------------------------------------------------------------------
// Internal helpers
// ---------------------------------------------------------------------------

function clip01(x: number): number {
  return Math.max(0.0, Math.min(1.0, x));
}

/** Convert TriangularFuzzy to Z-space — topsis.md §3.6 */
export function toZ(t: TriangularFuzzyM): NormalizedFuzzyM {
  return {
    positive: Math.abs(t.modal - t.lower),
    average: t.modal,
    negative: Math.abs(t.upper - t.modal),
  };
}

/** Per-property normalizer M(pₖ) — topsis.md §3.4.
 * Uses the original alternative pool (not filtered C).
 */
export function computeNormalizer(scenario: ScenarioM): Map<string, number> {
  const coeff = new Map<string, TriangularFuzzyM>();
  for (const c of scenario.coefficients) {
    coeff.set(`${c.alternativeId}|${c.propertyId}`, c.value);
  }

  // Group alternatives by decision
  const altsByDec = new Map<string, string[]>();
  for (const d of scenario.decisions) {
    altsByDec.set(d.id, []);
  }
  for (const a of scenario.alternatives) {
    altsByDec.get(a.decisionId)!.push(a.id);
  }

  const M = new Map<string, number>();
  for (const p of scenario.properties) {
    let total = 0.0;
    for (const d of scenario.decisions) {
      const groupAlts = altsByDec.get(d.id) ?? [];
      if (!groupAlts.length) continue;
      let best: number;
      if (p.kind === 'max') {
        best = Math.max(...groupAlts.map((aId) => coeff.get(`${aId}|${p.id}`)!.upper));
      } else {
        best = Math.max(...groupAlts.map((aId) => coeff.get(`${aId}|${p.id}`)!.lower));
      }
      total += best;
    }
    M.set(p.id, total);
  }
  return M;
}

/** Per-alternative contribution — used in §3.5 and §5.
 *  sign(p) = +1 for min, -1 for max (topsis.md §3.5).
 */
export function altContribution(
  altId: string,
  scenario: ScenarioM,
  coeff: Map<string, TriangularFuzzyM>,
  M: Map<string, number>,
): TriangularFuzzyM {
  let result = TriangularFuzzyM.zero();
  for (const p of scenario.properties) {
    const mP = M.get(p.id)!;
    if (mP === 0.0) {
      // Invariant 10.1: degenerate — skip to avoid division by zero
      console.warn(`Property '${p.id}' has M=0; skipping to avoid division by zero`);
      continue;
    }
    const sign = p.kind === 'min' ? 1.0 : -1.0;
    const contribution = coeff
      .get(`${altId}|${p.id}`)!
      .mul(sign * p.weight)
      .div(mP);
    result = result.add(contribution);
  }
  return result;
}

/** Aggregate contribution for a full candidate — topsis.md §3.5 */
function candidateTotalValue(
  candidateAltIds: readonly string[],
  scenario: ScenarioM,
  coeff: Map<string, TriangularFuzzyM>,
  M: Map<string, number>,
): TriangularFuzzyM {
  let result = TriangularFuzzyM.zero();
  for (const altId of candidateAltIds) {
    result = result.add(altContribution(altId, scenario, coeff, M));
  }
  return result;
}

/** Compute PIS/NIS in Z-space and normalize — topsis.md §3.7, §3.8 */
export function normalizeCandidates(zValues: NormalizedFuzzyM[]): NormalizedFuzzyM[] {
  if (!zValues.length) return [];

  // §3.7 PIS and NIS
  const pisAvg = Math.min(...zValues.map((z) => z.average));
  const pisPos = Math.max(...zValues.map((z) => z.positive));
  const pisNeg = Math.min(...zValues.map((z) => z.negative));

  const nisAvg = Math.max(...zValues.map((z) => z.average));
  const nisPos = Math.min(...zValues.map((z) => z.positive));
  const nisNeg = Math.max(...zValues.map((z) => z.negative));

  const normalized: NormalizedFuzzyM[] = [];
  for (const z of zValues) {
    // §3.8 — clip01 and 0/0 → 0
    const denomAvg = nisAvg - pisAvg;
    const nAvg = denomAvg !== 0.0 ? clip01((z.average - pisAvg) / denomAvg) : 0.0;

    const denomPos = pisPos - nisPos;
    const nPos = denomPos !== 0.0 ? clip01((pisPos - z.positive) / denomPos) : 0.0;

    const denomNeg = nisNeg - pisNeg;
    const nNeg = denomNeg !== 0.0 ? clip01((z.negative - pisNeg) / denomNeg) : 0.0;

    normalized.push({ positive: nPos, average: nAvg, negative: nNeg });
  }
  return normalized;
}

/** Compute per-candidate score — topsis.md §3.9, §3.10 */
export function score(n: NormalizedFuzzyM, weights: NormalizedFuzzyM, aggregation: string): number {
  const phiPos = weights.positive * n.positive;
  const phiAvg = weights.average * n.average;
  const phiNeg = weights.negative * n.negative;
  if (aggregation === 'sum') {
    return phiPos + phiAvg + phiNeg;
  } else {
    // 'max'
    return Math.max(phiPos, phiAvg, phiNeg);
  }
}

// ---------------------------------------------------------------------------
// Public API
// ---------------------------------------------------------------------------

/**
 * Run the full TOPSIS pipeline — topsis.md §3.
 *
 * Returns an array of CandidateM sorted by score ascending (lower = better),
 * with lexicographic tie-breaking on alternativeIds (§3.10).
 */
export function solve(scenario: ScenarioM): CandidateM[] {
  // -------------------------------------------------------------------------
  // §3.1  Enumerate raw candidates
  // -------------------------------------------------------------------------
  const decOrder = scenario.decisions;
  const altsByDec = new Map<string, string[]>();
  for (const d of decOrder) {
    altsByDec.set(d.id, []);
  }
  for (const a of scenario.alternatives) {
    altsByDec.get(a.decisionId)!.push(a.id);
  }

  const pools: string[][] = decOrder.map((d) => altsByDec.get(d.id) ?? []);

  // Cartesian product
  function cartesian(pools: string[][]): string[][] {
    if (!pools.length) return [[]];
    const [first, ...rest] = pools;
    const restProduct = cartesian(rest);
    const result: string[][] = [];
    for (const a of first) {
      for (const r of restProduct) {
        result.push([a, ...r]);
      }
    }
    return result;
  }

  const rawCandidates = cartesian(pools);

  if (!rawCandidates.length) return [];

  // -------------------------------------------------------------------------
  // §3.2  Filter candidates
  // -------------------------------------------------------------------------
  const depConstraints = scenario.constraints.filter((c) => c.kind === 'dependency') as Extract<
    (typeof scenario.constraints)[number],
    { kind: 'dependency' }
  >[];
  const confConstraints = scenario.constraints.filter((c) => c.kind === 'conflict') as Extract<
    (typeof scenario.constraints)[number],
    { kind: 'conflict' }
  >[];
  const threshConstraints = scenario.constraints.filter((c) => c.kind === 'threshold') as Extract<
    (typeof scenario.constraints)[number],
    { kind: 'threshold' }
  >[];

  // Build coeff lookup
  const coeff = new Map<string, TriangularFuzzyM>();
  for (const c of scenario.coefficients) {
    coeff.set(`${c.alternativeId}|${c.propertyId}`, c.value);
  }
  const propMap = new Map(scenario.properties.map((p) => [p.id, p]));

  function passesFilters(candidate: string[]): boolean {
    const cSet = new Set(candidate);

    // 1. Dependency (biconditional) — topsis.md §3.2 step 1
    for (const dc of depConstraints) {
      const srcIn = cSet.has(dc.sourceAlternativeId);
      const tgtIn = cSet.has(dc.targetAlternativeId);
      if (srcIn !== tgtIn) return false; // XOR — violates biconditional
    }

    // 2. Conflict — topsis.md §3.2 step 2
    for (const cc of confConstraints) {
      if (cSet.has(cc.alternativeAId) && cSet.has(cc.alternativeBId)) return false;
    }

    // 3. Threshold — topsis.md §3.2 step 3
    for (const tc of threshConstraints) {
      let contrib = TriangularFuzzyM.zero();
      for (const aId of candidate) {
        contrib = contrib.add(coeff.get(`${aId}|${tc.propertyId}`)!);
      }
      const defuzzed = contrib.lower; // §4.2: lower vertex only
      const prop = propMap.get(tc.propertyId)!;
      if (prop.kind === 'min') {
        if (tc.max !== undefined && defuzzed > tc.max) return false;
      } else {
        // max
        if (tc.min !== undefined && defuzzed < tc.min) return false;
      }
    }

    return true;
  }

  const feasible = rawCandidates.filter(passesFilters);
  if (!feasible.length) return [];

  // -------------------------------------------------------------------------
  // §3.4  Per-property normalizer (over original alternative pool)
  // -------------------------------------------------------------------------
  const M = computeNormalizer(scenario);

  // -------------------------------------------------------------------------
  // §3.5  Total triangular value per candidate
  // -------------------------------------------------------------------------
  const totalValues = feasible.map((c) => candidateTotalValue(c, scenario, coeff, M));

  // -------------------------------------------------------------------------
  // §3.6  Convert to Z-space
  // -------------------------------------------------------------------------
  const zValues = totalValues.map(toZ);

  // -------------------------------------------------------------------------
  // §3.7-3.8  PIS/NIS normalization
  // -------------------------------------------------------------------------
  const normValues = normalizeCandidates(zValues);

  // -------------------------------------------------------------------------
  // §3.9-3.10  Score, sort, rank
  // -------------------------------------------------------------------------
  const scores = normValues.map((n) =>
    score(n, scenario.config.weights, scenario.config.aggregation),
  );

  // Combine and sort: primary key = score ascending; secondary = lex on alt_ids
  const indexed = feasible.map((altIds, i) => ({ i, altIds }));
  indexed.sort((a, b) => {
    const scoreDiff = scores[a.i] - scores[b.i];
    if (scoreDiff !== 0) return scoreDiff;
    // Lexicographic tie-break on alternativeIds (§3.10)
    for (let k = 0; k < a.altIds.length; k++) {
      if (a.altIds[k] < b.altIds[k]) return -1;
      if (a.altIds[k] > b.altIds[k]) return 1;
    }
    return 0;
  });

  const candidates: CandidateM[] = indexed.map(({ i, altIds }, rank) => ({
    alternativeIds: altIds,
    triangularValue: totalValues[i],
    normalizedValue: normValues[i],
    score: scores[i],
    rank,
  }));

  return candidates;
}
