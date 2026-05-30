"""Scenario domain model — top-level container for a TOPSIS scenario."""

from __future__ import annotations

from dataclasses import dataclass, field
from typing import Literal

from guidearch.models.alternative import AlternativeM
from guidearch.models.coefficient import CoefficientM
from guidearch.models.constraint import Constraint
from guidearch.models.decision import DecisionM
from guidearch.models.normalized_fuzzy import NormalizedFuzzyM
from guidearch.models.property import PropertyM

# Weights triple: (positive, average, negative) summing to 1.
# Reuses the NormalizedFuzzyM shape (positive/average/negative fields).
Weights = NormalizedFuzzyM


@dataclass(frozen=True)
class ConfigM:
    """Scenario configuration.

    aggregation: 'sum' or 'max' (PhiCalculationApproach); default 'max'.
    weights: (w+, wa, w-) summing to 1 (SolutionApproach presets).
    """

    aggregation: Literal["sum", "max"]
    weights: Weights


@dataclass(frozen=True)
class ScenarioM:
    """Complete decision-analysis scenario for fuzzy TOPSIS ranking."""

    schema_version: str
    name: str
    description: str
    decisions: tuple[DecisionM, ...]
    alternatives: tuple[AlternativeM, ...]
    properties: tuple[PropertyM, ...]
    coefficients: tuple[CoefficientM, ...]
    constraints: tuple[Constraint, ...]
    config: ConfigM
    warnings: tuple[str, ...] = field(default_factory=tuple)
