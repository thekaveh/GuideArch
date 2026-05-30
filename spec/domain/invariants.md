# Scenario invariants

The scenario JSON schema enforces structure. These invariants enforce **meaning** — the things the schema can't express. Every implementation's scenario loader MUST validate them at load time and reject scenarios that violate any.

Errors are categorized **fatal** (cannot solve) or **warning** (legacy was lax; we keep loading but the impl reports the issue).

## 1. Identifier uniqueness (fatal)

1.1 Every `decisions[*].id` must be unique within the scenario.
1.2 Every `alternatives[*].id` must be unique within the scenario.
1.3 Every `properties[*].id` must be unique within the scenario.
1.4 The three id sets (`decisions`, `alternatives`, `properties`) must be pairwise disjoint. (No id may name both a decision and a property, etc.)

## 2. Cross-reference validity (fatal)

2.1 Every `alternatives[*].decisionId` must exist in `decisions`.
2.2 Every `coefficients[*].alternativeId` must exist in `alternatives`.
2.3 Every `coefficients[*].propertyId` must exist in `properties`.
2.4 Every `constraints[*].propertyId` (where present) must exist in `properties`.
2.5 Every `constraints[*].sourceAlternativeId` / `targetAlternativeId` / `alternativeAId` / `alternativeBId` must exist in `alternatives`.

## 3. Coefficient completeness (fatal)

3.1 For every pair `(alternativeId, propertyId)` in the Cartesian product `alternatives × properties`, there must be exactly one coefficient. Duplicates and omissions are both errors.

## 4. Triangular ordering (warning)

4.1 For every `TriangularFuzzy` in the scenario (`coefficients[*].value`), `lower ≤ modal ≤ upper` should hold. The legacy code did not enforce this; impls should report a warning but still load.

## 5. Weights (fatal)

5.1 `config.weights.positive + config.weights.average + config.weights.negative` must equal `1`, within an absolute tolerance of `1e-9`.
5.2 Each individual weight must be in `[0, 1]`.

## 6. Threshold constraints (fatal)

6.1 At least one of `min` or `max` must be present on every threshold constraint. (Schema enforces this with `anyOf`.)
6.2 If both `min` and `max` are present, `min ≤ max`.

## 7. Dependency / conflict constraints (fatal)

7.1 Self-edges are not allowed: `sourceAlternativeId ≠ targetAlternativeId` on dependency constraints; `alternativeAId ≠ alternativeBId` on conflict constraints.
7.2 Cross-decision constraints are fine; same-decision dependency/conflict constraints are technically allowed but rarely meaningful (same decision picks one alternative; both being "in a candidate" is impossible by construction). Impls should report a warning.

## 8. Decision occupancy (fatal)

8.1 Every decision must have at least one alternative.
8.2 Empty `alternatives` array → no candidates → fatal at solve time.

## 9. Solvability (warning)

9.1 If the constraint set eliminates **all** raw candidates, `solve()` returns an empty ranking. Impls should report this as an informational message in the UI rather than an error.

## 10. Numerical stability (warning)

10.1 Per-property normalizer `M(p)` (see `topsis.md` §3.4) being zero is a degenerate case; impls should detect and emit a warning rather than divide by zero.
10.2 PIS/NIS being equal on a Z-dimension (see `topsis.md` §3.8) produces a `0/0` ratio; the spec defines this as `0`. Impls must implement that explicit branch.
