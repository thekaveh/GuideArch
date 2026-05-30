"""Candidate domain model — TOPSIS output per architecture candidate."""

from __future__ import annotations

from dataclasses import dataclass

from guidearch.models.normalized_fuzzy import NormalizedFuzzyM
from guidearch.models.triangular_fuzzy import TriangularFuzzyM


@dataclass(frozen=True)
class CandidateM:
    """A ranked architecture candidate produced by solve().

    alternative_ids: one alternative per decision (in scenario.decisions order).
    triangular_value: aggregated TriangularFuzzy over all properties (§3.5).
    normalized_value: NormalizedFuzzy in [0, 1]^3 (§3.8).
    score: scalar in [0, 1]; lower is better (§3.10).
    rank: 0-based position after sorting by score ascending.
    """

    alternative_ids: tuple[str, ...]
    triangular_value: TriangularFuzzyM
    normalized_value: NormalizedFuzzyM
    score: float
    rank: int
