"""Alternative domain model."""

from __future__ import annotations

from dataclasses import dataclass


@dataclass(frozen=True)
class AlternativeM:
    """One concrete option for a single decision."""

    id: str
    decision_id: str
    name: str
