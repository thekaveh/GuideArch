"""Constraint domain models — tagged union of three kinds."""

from __future__ import annotations

from dataclasses import dataclass
from typing import Literal


@dataclass(frozen=True)
class ThresholdConstraint:
    """A property's aggregate over a candidate must lie in [min, max].

    At least one of min/max must be present (invariant 6.1).
    """

    kind: Literal["threshold"]
    property_id: str
    min: float | None
    max: float | None


@dataclass(frozen=True)
class DependencyConstraint:
    """Biconditional: source ∈ candidate ↔ target ∈ candidate.

    The spec mandates the biconditional (topsis.md §3.2 note), NOT the
    buggy literal from Space.cs line 975.
    """

    kind: Literal["dependency"]
    source_alternative_id: str
    target_alternative_id: str


@dataclass(frozen=True)
class ConflictConstraint:
    """A and B cannot both be in a candidate."""

    kind: Literal["conflict"]
    alternative_a_id: str
    alternative_b_id: str


Constraint = ThresholdConstraint | DependencyConstraint | ConflictConstraint
