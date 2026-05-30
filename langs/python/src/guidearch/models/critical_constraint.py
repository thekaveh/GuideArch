"""CriticalConstraintM — output of the critical-constraints analysis."""

from __future__ import annotations

from dataclasses import dataclass


@dataclass(frozen=True)
class CriticalConstraintM:
    """Binding-strength ranking of one constraint.

    constraint_index: 0-based index into scenario.constraints.
    kind: 'threshold' | 'dependency' | 'conflict' (echoed for context).
    eliminated: number of candidates this constraint removes from the
        unconstrained Cartesian product.
    total: size of the unconstrained Cartesian product (same for every
        constraint in a scenario, echoed for context).
    redundant: eliminated == 0.
    """

    constraint_index: int
    kind: str
    eliminated: int
    total: int
    redundant: bool
