"""Coefficient domain model."""

from __future__ import annotations

from dataclasses import dataclass

from guidearch.models.triangular_fuzzy import TriangularFuzzyM


@dataclass(frozen=True)
class CoefficientM:
    """Fuzzy rating of one alternative on one property."""

    alternative_id: str
    property_id: str
    value: TriangularFuzzyM
