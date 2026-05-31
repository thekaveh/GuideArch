"""Unit tests for scenario_loader invariant enforcement."""

from __future__ import annotations

import copy
import json
import re
import tempfile
from pathlib import Path
from typing import Any

import pytest

from guidearch.models.scenario_loader import ScenarioValidationError, load_scenario

# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

_BASE_SCENARIO: dict[str, Any] = {
    "schemaVersion": "1.0.0",
    "name": "test",
    "description": "",
    "decisions": [
        {"id": "d1", "name": "D1"},
    ],
    "alternatives": [
        {"id": "a1", "decisionId": "d1", "name": "A1"},
        {"id": "a2", "decisionId": "d1", "name": "A2"},
    ],
    "properties": [
        {"id": "p1", "name": "Prop", "kind": "min", "weight": 1.0},
    ],
    "coefficients": [
        {
            "alternativeId": "a1",
            "propertyId": "p1",
            "value": {"lower": 1.0, "modal": 2.0, "upper": 3.0},
        },
        {
            "alternativeId": "a2",
            "propertyId": "p1",
            "value": {"lower": 1.0, "modal": 2.0, "upper": 3.0},
        },
    ],
    "constraints": [],
    "config": {
        "aggregation": "max",
        "weights": {
            "positive": 0.3333333333,
            "average": 0.3333333334,
            "negative": 0.3333333333,
        },
    },
}


def _write_scenario(data: dict[str, Any]) -> Path:
    with tempfile.NamedTemporaryFile(mode="w", suffix=".json", delete=False, encoding="utf-8") as f:
        json.dump(data, f)
        return Path(f.name)


def _clone() -> dict[str, Any]:
    return copy.deepcopy(_BASE_SCENARIO)


# ---------------------------------------------------------------------------
# Invariant 1: Identifier uniqueness
# ---------------------------------------------------------------------------


def test_inv1_1_duplicate_decision_id() -> None:
    s = _clone()
    s["decisions"].append({"id": "d1", "name": "D1 dup"})
    path = _write_scenario(s)
    with pytest.raises(ScenarioValidationError, match=re.escape("1.1")):
        load_scenario(path)


def test_inv1_2_duplicate_alternative_id() -> None:
    s = _clone()
    s["alternatives"].append({"id": "a1", "decisionId": "d1", "name": "A1 dup"})
    path = _write_scenario(s)
    with pytest.raises(ScenarioValidationError, match=re.escape("1.2")):
        load_scenario(path)


def test_inv1_3_duplicate_property_id() -> None:
    s = _clone()
    s["properties"].append({"id": "p1", "name": "P1 dup", "kind": "max", "weight": 1.0})
    path = _write_scenario(s)
    with pytest.raises(ScenarioValidationError, match=re.escape("1.3")):
        load_scenario(path)


def test_inv1_4_decision_and_alternative_share_id() -> None:
    s = _clone()
    s["alternatives"][0]["id"] = "d1"
    path = _write_scenario(s)
    with pytest.raises(ScenarioValidationError, match=re.escape("1.4")):
        load_scenario(path)


# ---------------------------------------------------------------------------
# Invariant 2: Cross-reference validity
# ---------------------------------------------------------------------------


def test_inv2_1_unknown_decision_in_alternative() -> None:
    s = _clone()
    s["alternatives"][0]["decisionId"] = "d_unknown"
    path = _write_scenario(s)
    with pytest.raises(ScenarioValidationError, match=re.escape("2.1")):
        load_scenario(path)


def test_inv2_2_unknown_alternative_in_coefficient() -> None:
    s = _clone()
    s["coefficients"][0]["alternativeId"] = "a_unknown"
    path = _write_scenario(s)
    with pytest.raises(ScenarioValidationError, match=re.escape("2.2")):
        load_scenario(path)


def test_inv2_3_unknown_property_in_coefficient() -> None:
    s = _clone()
    s["coefficients"][0]["propertyId"] = "p_unknown"
    path = _write_scenario(s)
    with pytest.raises(ScenarioValidationError, match=re.escape("2.3")):
        load_scenario(path)


# ---------------------------------------------------------------------------
# Invariant 3: Coefficient completeness
# ---------------------------------------------------------------------------


def test_inv3_1_missing_coefficient() -> None:
    s = _clone()
    s["coefficients"] = [s["coefficients"][0]]
    path = _write_scenario(s)
    with pytest.raises(ScenarioValidationError, match=re.escape("3.1")):
        load_scenario(path)


def test_inv3_1_duplicate_coefficient() -> None:
    s = _clone()
    s["coefficients"].append(copy.deepcopy(s["coefficients"][0]))
    path = _write_scenario(s)
    with pytest.raises(ScenarioValidationError, match=re.escape("3.1")):
        load_scenario(path)


# ---------------------------------------------------------------------------
# Invariant 5: Weights
# ---------------------------------------------------------------------------


def test_inv5_1_weights_dont_sum_to_one() -> None:
    s = _clone()
    s["config"]["weights"] = {"positive": 0.4, "average": 0.4, "negative": 0.4}
    path = _write_scenario(s)
    with pytest.raises(ScenarioValidationError, match=re.escape("5.1")):
        load_scenario(path)


# ---------------------------------------------------------------------------
# Invariant 7: Dependency/conflict self-edges
# ---------------------------------------------------------------------------


def test_inv7_1_dependency_self_edge() -> None:
    s = _clone()
    s["constraints"] = [
        {
            "kind": "dependency",
            "sourceAlternativeId": "a1",
            "targetAlternativeId": "a1",
        }
    ]
    path = _write_scenario(s)
    with pytest.raises(ScenarioValidationError, match=re.escape("7.1")):
        load_scenario(path)


def test_inv7_1_conflict_self_edge() -> None:
    s = _clone()
    s["constraints"] = [{"kind": "conflict", "alternativeAId": "a1", "alternativeBId": "a1"}]
    path = _write_scenario(s)
    with pytest.raises(ScenarioValidationError, match=re.escape("7.1")):
        load_scenario(path)


# ---------------------------------------------------------------------------
# Invariant 8: Decision occupancy
# ---------------------------------------------------------------------------


def test_inv8_1_decision_with_no_alternatives() -> None:
    s = _clone()
    s["decisions"].append({"id": "d2", "name": "D2 empty"})
    path = _write_scenario(s)
    with pytest.raises(ScenarioValidationError, match=re.escape("8.1")):
        load_scenario(path)


# ---------------------------------------------------------------------------
# Happy path
# ---------------------------------------------------------------------------


def test_valid_scenario_loads() -> None:
    path = _write_scenario(_BASE_SCENARIO)
    scenario = load_scenario(path)
    assert scenario.name == "test"
    assert len(scenario.decisions) == 1
    assert len(scenario.alternatives) == 2
    assert len(scenario.properties) == 1
    assert len(scenario.coefficients) == 2


def test_triangular_ordering_warning() -> None:
    s = _clone()
    s["coefficients"][0]["value"] = {"lower": 3.0, "modal": 2.0, "upper": 3.0}
    path = _write_scenario(s)
    scenario = load_scenario(path)
    assert any("4.1" in w for w in scenario.warnings)
