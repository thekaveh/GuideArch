"""CriticalDecisionM — output of the critical-decisions analysis."""

from __future__ import annotations

from dataclasses import dataclass

from guidearch.models.normalized_fuzzy import NormalizedFuzzyM
from guidearch.models.triangular_fuzzy import TriangularFuzzyM


@dataclass(frozen=True)
class CriticalDecisionM:
    """Sensitivity ranking of one decision.

    decision_id: references scenario.decisions[*].id.
    triangular_value: aggregate TriangularFuzzy (pre-normalization, §5).
    normalized_value: NormalizedFuzzy in [0, 1]^3.
    score: scalar in [0, 1]; lower = more critical.
    rank: 0-based position after sorting by score ascending.
    """

    decision_id: str
    triangular_value: TriangularFuzzyM
    normalized_value: NormalizedFuzzyM
    score: float
    rank: int
