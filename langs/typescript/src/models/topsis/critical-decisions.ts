/**
 * Critical decisions analysis — topsis.md §5.
 */
import type { ScenarioM } from '../scenario.js';
import type { CandidateM } from '../candidate.js';
import type { CriticalDecisionM } from '../critical-decision.js';
import { TriangularFuzzyM } from '../triangular-fuzzy.js';
import { altContribution, computeNormalizer, toZ } from './solve.js';
import { solve } from './solve.js';

const TOP_N = 20; // topsis.md §8
const DECAY = 0.1; // topsis.md §8 — exp(-0.1 * rank)

/**
 * Rank decisions by how strongly they drive the top candidates' scores.
 *
 * Implements topsis.md §5. Aggregation is always 'max' (legacy hardcodes Max
 * here, ignoring config.aggregation — topsis.md §5 last paragraph).
 *
 * Returns decisions sorted by score ascending (lower = more critical).
 */
export function criticalDecisions(
  scenario: ScenarioM,
  candidates?: CandidateM[],
): CriticalDecisionM[] {
  const resolvedCandidates = candidates ?? solve(scenario);

  if (!resolvedCandidates.length) return [];

  // Take top-N ranked candidates
  const topN = resolvedCandidates.slice(0, Math.min(TOP_N, resolvedCandidates.length));

  // Build coeff lookup and normalizer
  const coeff = new Map<string, TriangularFuzzyM>();
  for (const c of scenario.coefficients) {
    coeff.set(`${c.alternativeId}|${c.propertyId}`, c.value);
  }
  const M = computeNormalizer(scenario);

  // Map decision_id → index in scenario.decisions
  const decIndex = new Map<string, number>(scenario.decisions.map((d, i) => [d.id, i]));

  // -------------------------------------------------------------------------
  // §5  contribution(di) = sum over c in Rt : exp(-0.1*rank(c)) * altContrib(c[i])
  // -------------------------------------------------------------------------
  const decContributions = new Map<string, TriangularFuzzyM>(
    scenario.decisions.map((d) => [d.id, TriangularFuzzyM.zero()]),
  );

  for (const c of topN) {
    const weightFactor = Math.exp(-DECAY * c.rank);
    for (const d of scenario.decisions) {
      const dIdx = decIndex.get(d.id)!;
      const altId = c.alternativeIds[dIdx];
      const ac = altContribution(altId, scenario, coeff, M);
      decContributions.set(d.id, decContributions.get(d.id)!.add(ac.mul(weightFactor)));
    }
  }

  // -------------------------------------------------------------------------
  // Convert to Z-space, compute PIS/NIS, normalize — same as §3.6-3.8
  // but over the *decision* set, with aggregation = max.
  // -------------------------------------------------------------------------
  const decisionIds = scenario.decisions.map((d) => d.id);
  const triangularValues = decisionIds.map((dId) => decContributions.get(dId)!);
  const zValues = triangularValues.map(toZ);

  if (!zValues.length) return [];

  // PIS/NIS over decision set
  const pisAvg = Math.min(...zValues.map((z) => z.average));
  const pisPos = Math.max(...zValues.map((z) => z.positive));
  const pisNeg = Math.min(...zValues.map((z) => z.negative));

  const nisAvg = Math.max(...zValues.map((z) => z.average));
  const nisPos = Math.min(...zValues.map((z) => z.positive));
  const nisNeg = Math.max(...zValues.map((z) => z.negative));

  const clip01 = (x: number) => Math.max(0.0, Math.min(1.0, x));

  const normValues = zValues.map((z) => {
    const denomAvg = nisAvg - pisAvg;
    const nAvg = denomAvg !== 0.0 ? clip01((z.average - pisAvg) / denomAvg) : 0.0;

    const denomPos = pisPos - nisPos;
    const nPos = denomPos !== 0.0 ? clip01((pisPos - z.positive) / denomPos) : 0.0;

    const denomNeg = nisNeg - pisNeg;
    const nNeg = denomNeg !== 0.0 ? clip01((z.negative - pisNeg) / denomNeg) : 0.0;

    return { positive: nPos, average: nAvg, negative: nNeg };
  });

  const weights = scenario.config.weights;

  // Score with max aggregation (legacy hardcodes Max here)
  const scoreMax = (n: { positive: number; average: number; negative: number }): number =>
    Math.max(
      weights.positive * n.positive,
      weights.average * n.average,
      weights.negative * n.negative,
    );

  const scores = normValues.map(scoreMax);

  // Sort ascending by score, tie-break by decisionId lexicographic
  const indexed = decisionIds.map((dId, i) => ({ i, dId }));
  indexed.sort((a, b) => {
    const diff = scores[a.i] - scores[b.i];
    if (diff !== 0) return diff;
    if (a.dId < b.dId) return -1;
    if (a.dId > b.dId) return 1;
    return 0;
  });

  return indexed.map(({ i, dId }, rank) => ({
    decisionId: dId,
    triangularValue: triangularValues[i],
    normalizedValue: normValues[i],
    score: scores[i],
    rank,
  }));
}
