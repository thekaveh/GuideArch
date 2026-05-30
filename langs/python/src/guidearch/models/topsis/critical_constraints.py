"""Critical constraints analysis — topsis.md §6."""

from __future__ import annotations

import itertools
from typing import TYPE_CHECKING

from guidearch.models.constraint import (
    ConflictConstraint,
    DependencyConstraint,
    ThresholdConstraint,
)
from guidearch.models.critical_constraint import CriticalConstraintM
from guidearch.models.triangular_fuzzy import TriangularFuzzyM

if TYPE_CHECKING:
    from guidearch.models.scenario import ScenarioM


def _apply_single_constraint(
    candidate: tuple[str, ...],
    constraint: ConflictConstraint | DependencyConstraint | ThresholdConstraint,
    coeff: dict[tuple[str, str], TriangularFuzzyM],
    prop_kind: dict[str, str],
) -> bool:
    """Return True if candidate passes the given single constraint."""
    c_set = set(candidate)

    if isinstance(constraint, DependencyConstraint):
        src_in = constraint.source_alternative_id in c_set
        tgt_in = constraint.target_alternative_id in c_set
        return src_in == tgt_in  # biconditional

    elif isinstance(constraint, ConflictConstraint):
        return not (constraint.alternative_a_id in c_set and constraint.alternative_b_id in c_set)

    else:  # ThresholdConstraint
        contrib = TriangularFuzzyM.zero()
        for a_id in candidate:
            contrib = contrib + coeff[(a_id, constraint.property_id)]
        defuzzed = contrib.lower  # §4.2: lower vertex only
        kind = prop_kind[constraint.property_id]
        if kind == "min":
            if constraint.max is not None and defuzzed > constraint.max:
                return False
        else:
            if constraint.min is not None and defuzzed < constraint.min:
                return False
        return True


def critical_constraints(scenario: ScenarioM) -> tuple[CriticalConstraintM, ...]:
    """Rank constraints by how many candidates they eliminate — topsis.md §6.

    For each constraint k:
    1. Build unconstrained Cartesian product.
    2. Apply only k and count eliminated candidates.

    Returns constraints sorted by eliminated descending (most-binding first).
    """
    if not scenario.constraints:
        return ()

    # Build unconstrained Cartesian product
    alts_by_dec: dict[str, list[str]] = {}
    for a in scenario.alternatives:
        alts_by_dec.setdefault(a.decision_id, []).append(a.id)

    pools: list[list[str]] = [alts_by_dec.get(d.id, []) for d in scenario.decisions]
    unconstrained: list[tuple[str, ...]] = [
        tuple(combo) for combo in itertools.product(*pools)
    ]
    total = len(unconstrained)

    coeff: dict[tuple[str, str], TriangularFuzzyM] = {
        (c.alternative_id, c.property_id): c.value for c in scenario.coefficients
    }
    prop_kind: dict[str, str] = {p.id: p.kind for p in scenario.properties}

    results: list[CriticalConstraintM] = []
    for idx, constraint in enumerate(scenario.constraints):
        passed = sum(
            1
            for c in unconstrained
            if _apply_single_constraint(c, constraint, coeff, prop_kind)
        )
        eliminated = total - passed
        results.append(
            CriticalConstraintM(
                constraint_index=idx,
                kind=constraint.kind,
                eliminated=eliminated,
                total=total,
                redundant=(eliminated == 0),
            )
        )

    # Sort by eliminated descending (most-binding first)
    results.sort(key=lambda r: -r.eliminated)

    return tuple(results)
