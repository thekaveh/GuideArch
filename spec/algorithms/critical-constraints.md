# Critical constraints — formal specification

**Status:** Authoritative. See [`topsis.md`](topsis.md) §6 for the canonical algorithm — this document is the reference card.

## 1. Summary

`criticalConstraints(scenario) → RankedConstraints` measures, for each constraint, how many candidates it eliminates from the unconstrained Cartesian product.

## 2. Inputs

- A `ScenarioM`.

## 3. Outputs

A list of `CriticalConstraintM` sorted by `eliminated` descending (most-binding first), with fields:

- `constraintIndex`: 0-based index into `scenario.constraints`
- `kind`: `"threshold" | "dependency" | "conflict"` (echoed for convenience)
- `eliminated`: integer count of candidates the constraint removed from the unconstrained Cartesian product
- `total`: `|unconstrained Cartesian product|` (same for every constraint in a scenario, but echoed for context)
- `redundant`: `eliminated == 0`

## 4. Algorithm

See `topsis.md` §6.

## 5. Edge cases

- **Empty `constraints` array**: returns an empty list.
- **A constraint eliminates all candidates**: not an error here; the constraint gets a maximal `eliminated` value. The downstream solve will then report "no feasible candidates" via the §9 invariant warning.
