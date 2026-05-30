/**
 * Critical constraints analysis — topsis.md §6.
 */
import type { ScenarioM } from '../scenario.js';
import type { ConstraintM } from '../constraint.js';
import type { CriticalConstraintM } from '../critical-constraint.js';
import { TriangularFuzzyM } from '../triangular-fuzzy.js';

function applySingleConstraint(
  candidate: string[],
  constraint: ConstraintM,
  coeff: Map<string, TriangularFuzzyM>,
  propKind: Map<string, string>,
): boolean {
  const cSet = new Set(candidate);

  if (constraint.kind === 'dependency') {
    const srcIn = cSet.has(constraint.sourceAlternativeId);
    const tgtIn = cSet.has(constraint.targetAlternativeId);
    return srcIn === tgtIn; // biconditional
  } else if (constraint.kind === 'conflict') {
    return !(cSet.has(constraint.alternativeAId) && cSet.has(constraint.alternativeBId));
  } else {
    // threshold
    let contrib = TriangularFuzzyM.zero();
    for (const aId of candidate) {
      contrib = contrib.add(coeff.get(`${aId}|${constraint.propertyId}`)!);
    }
    const defuzzed = contrib.lower; // §4.2: lower vertex only
    const kind = propKind.get(constraint.propertyId)!;
    if (kind === 'min') {
      if (constraint.max !== undefined && defuzzed > constraint.max) return false;
    } else {
      if (constraint.min !== undefined && defuzzed < constraint.min) return false;
    }
    return true;
  }
}

/**
 * Rank constraints by how many candidates they eliminate — topsis.md §6.
 *
 * For each constraint k:
 * 1. Build unconstrained Cartesian product.
 * 2. Apply only k and count eliminated candidates.
 *
 * Returns constraints sorted by eliminated descending (most-binding first).
 */
export function criticalConstraints(scenario: ScenarioM): CriticalConstraintM[] {
  if (!scenario.constraints.length) return [];

  // Build unconstrained Cartesian product
  const altsByDec = new Map<string, string[]>();
  for (const d of scenario.decisions) {
    altsByDec.set(d.id, []);
  }
  for (const a of scenario.alternatives) {
    altsByDec.get(a.decisionId)!.push(a.id);
  }

  const pools = scenario.decisions.map((d) => altsByDec.get(d.id) ?? []);

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

  const unconstrained = cartesian(pools);
  const total = unconstrained.length;

  const coeff = new Map<string, TriangularFuzzyM>();
  for (const c of scenario.coefficients) {
    coeff.set(`${c.alternativeId}|${c.propertyId}`, c.value);
  }
  const propKind = new Map<string, string>(scenario.properties.map((p) => [p.id, p.kind]));

  const results: CriticalConstraintM[] = scenario.constraints.map((constraint, idx) => {
    const passed = unconstrained.filter((c) =>
      applySingleConstraint(c, constraint, coeff, propKind),
    ).length;
    const eliminated = total - passed;
    return {
      constraintIndex: idx,
      kind: constraint.kind,
      eliminated,
      total,
      redundant: eliminated === 0,
    };
  });

  // Sort by eliminated descending (most-binding first)
  results.sort((a, b) => b.eliminated - a.eliminated);

  return results;
}
