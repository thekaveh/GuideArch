"""Unit tests for ``comparison_option`` — Chart C (top-N candidate
comparison polylines) per spec/charts.md §4.

Mirrors langs/typescript/tests/unit/chart-comparison.test.ts and the
C# ChartComparisonTests so the three impls lock the same invariants.
"""

from __future__ import annotations

import pytest

from guidearch.models.candidate import CandidateM
from guidearch.models.coefficient import CoefficientM
from guidearch.models.normalized_fuzzy import NormalizedFuzzyM
from guidearch.models.property import PropertyM
from guidearch.models.triangular_fuzzy import TriangularFuzzyM
from guidearch.view.chart_data import (
    COMPARISON_PALETTE,
    DEFAULT_COMPARISON_TOP_N,
    comparison_option,
)


def _candidate(rank: int, alt_ids: tuple[str, ...], score: float) -> CandidateM:
    return CandidateM(
        alternative_ids=alt_ids,
        triangular_value=TriangularFuzzyM(0.0, score, 0.0),
        normalized_value=NormalizedFuzzyM(0.0, score, 0.0),
        score=score,
        rank=rank,
    )


def _property(id: str, name: str) -> PropertyM:
    return PropertyM(id=id, name=name, kind="max", weight=1.0)


def _coeff(alt_id: str, prop_id: str, modal: float) -> CoefficientM:
    return CoefficientM(
        alternative_id=alt_id,
        property_id=prop_id,
        value=TriangularFuzzyM(modal, modal, modal),
    )


def test_palette_matches_spec() -> None:
    """Palette has exactly the 10 Tableau hex values from spec/charts.md §4."""
    assert COMPARISON_PALETTE == [
        "#4e79a7",
        "#f28e2b",
        "#e15759",
        "#76b7b2",
        "#59a14f",
        "#edc948",
        "#b07aa1",
        "#ff9da7",
        "#9c755f",
        "#bab0ac",
    ]
    assert DEFAULT_COMPARISON_TOP_N == 10


def test_empty_candidates_returns_empty_dict() -> None:
    """No candidates → empty option dict (no chart to render)."""
    result = comparison_option(
        candidates=(),
        properties=(_property("p1", "P1"),),
        coefficients=(),
        selected_index=None,
    )
    assert result == {}


def test_empty_properties_returns_empty_dict() -> None:
    """No properties → empty option dict (no X axis)."""
    result = comparison_option(
        candidates=(_candidate(0, ("a",), 0.5),),
        properties=(),
        coefficients=(),
        selected_index=None,
    )
    assert result == {}


def test_caps_series_at_default_top_n() -> None:
    """Default top_n = 10; longer candidate lists are truncated."""
    props = (_property("p1", "P1"),)
    candidates = tuple(_candidate(i, (f"a{i}",), 1.0 - i * 0.01) for i in range(25))
    result = comparison_option(
        candidates=candidates,
        properties=props,
        coefficients=(),
        selected_index=None,
    )
    assert len(result["series"]) == 10


def test_caps_series_at_explicit_top_n() -> None:
    """Explicit top_n smaller than candidates + palette wins."""
    props = (_property("p1", "P1"),)
    candidates = tuple(_candidate(i, (f"a{i}",), 1.0 - i * 0.01) for i in range(15))
    result = comparison_option(
        candidates=candidates,
        properties=props,
        coefficients=(),
        selected_index=None,
        top_n=5,
    )
    assert len(result["series"]) == 5


def test_caps_series_at_candidates_length_when_smaller() -> None:
    """Fewer candidates than top_n → series length = candidates length."""
    props = (_property("p1", "P1"),)
    candidates = (
        _candidate(0, ("a0",), 0.9),
        _candidate(1, ("a1",), 0.8),
    )
    result = comparison_option(
        candidates=candidates,
        properties=props,
        coefficients=(),
        selected_index=None,
        top_n=10,
    )
    assert len(result["series"]) == 2


def test_palette_indexed_by_series_position() -> None:
    """Each series uses COMPARISON_PALETTE[index], not [rank]."""
    props = (_property("p1", "P1"),)
    candidates = (
        _candidate(0, ("a",), 0.5),
        _candidate(1, ("a",), 0.4),
        _candidate(2, ("a",), 0.3),
    )
    result = comparison_option(
        candidates=candidates,
        properties=props,
        coefficients=(),
        selected_index=None,
    )
    assert result["series"][0]["lineStyle"]["color"] == COMPARISON_PALETTE[0]
    assert result["series"][1]["lineStyle"]["color"] == COMPARISON_PALETTE[1]
    assert result["series"][2]["lineStyle"]["color"] == COMPARISON_PALETTE[2]


def test_label_format_is_rank_score_to_three_sig_figs() -> None:
    """Series name follows the spec format: "#<rank> (<score>)" with 3 sig figs."""
    props = (_property("p1", "P1"),)
    candidates = (
        _candidate(0, ("a",), 0.03118),
        _candidate(7, ("a",), 0.0271423),
    )
    result = comparison_option(
        candidates=candidates,
        properties=props,
        coefficients=(),
        selected_index=None,
    )
    assert result["series"][0]["name"] == "#0 (0.0312)"
    assert result["series"][1]["name"] == "#7 (0.0271)"


def test_modal_sum_per_property() -> None:
    """Y-value per property = sum of modal coefficients for the candidate's
    selected alternatives only — coefficients for other alternatives are
    ignored."""
    props = (_property("p1", "P1"), _property("p2", "P2"))
    coeffs = (
        _coeff("a1", "p1", 0.6),
        _coeff("a1", "p2", 0.1),
        _coeff("a2", "p1", 0.3),
        _coeff("a2", "p2", 0.2),
        # Noise — should NOT be summed (not in candidate's alternatives):
        _coeff("a3", "p1", 99.0),
        _coeff("a3", "p2", 99.0),
    )
    candidates = (_candidate(0, ("a1", "a2"), 0.5),)
    result = comparison_option(
        candidates=candidates,
        properties=props,
        coefficients=coeffs,
        selected_index=None,
    )
    series_data = result["series"][0]["data"]
    # data is list[(prop_name, y_value)] from zip()
    assert series_data[0][0] == "P1"
    assert series_data[0][1] == pytest.approx(0.9, abs=1e-12)
    assert series_data[1][0] == "P2"
    assert series_data[1][1] == pytest.approx(0.3, abs=1e-12)


def test_missing_coefficient_contributes_zero() -> None:
    """A candidate's (alternative, property) pairing without a coefficient
    contributes zero rather than raising."""
    props = (_property("p1", "P1"), _property("p2", "P2"))
    coeffs = (_coeff("a1", "p1", 0.5),)  # No (a1, p2) coefficient defined.
    candidates = (_candidate(0, ("a1",), 0.5),)
    result = comparison_option(
        candidates=candidates,
        properties=props,
        coefficients=coeffs,
        selected_index=None,
    )
    series_data = result["series"][0]["data"]
    assert series_data[0][1] == pytest.approx(0.5, abs=1e-12)
    assert series_data[1][1] == 0.0


def test_selection_highlight_dims_other_lines() -> None:
    """When selected_index is set, the selected series renders at full
    opacity / thicker stroke; non-selected lines fade to 0.25 opacity / 1.4
    stroke."""
    props = (_property("p1", "P1"),)
    candidates = (
        _candidate(0, ("a",), 0.5),
        _candidate(1, ("a",), 0.4),
        _candidate(2, ("a",), 0.3),
    )
    result = comparison_option(
        candidates=candidates,
        properties=props,
        coefficients=(),
        selected_index=1,
    )
    # selected series (index 1)
    assert result["series"][1]["lineStyle"]["opacity"] == 1.0
    assert result["series"][1]["lineStyle"]["width"] == 2.5
    # non-selected series
    assert result["series"][0]["lineStyle"]["opacity"] == 0.25
    assert result["series"][0]["lineStyle"]["width"] == 1.4
    assert result["series"][2]["lineStyle"]["opacity"] == 0.25
    assert result["series"][2]["lineStyle"]["width"] == 1.4


def test_no_selection_keeps_all_lines_full_opacity() -> None:
    """When selected_index is None, all polylines render at full opacity
    (so the comparison is unambiguous)."""
    props = (_property("p1", "P1"),)
    candidates = (
        _candidate(0, ("a",), 0.5),
        _candidate(1, ("a",), 0.4),
    )
    result = comparison_option(
        candidates=candidates,
        properties=props,
        coefficients=(),
        selected_index=None,
    )
    assert result["series"][0]["lineStyle"]["opacity"] == 1.0
    assert result["series"][1]["lineStyle"]["opacity"] == 1.0


def test_x_axis_uses_property_names_in_order() -> None:
    """X axis is categorical, populated with property names in declaration order."""
    props = (_property("p1", "Alpha"), _property("p2", "Beta"), _property("p3", "Gamma"))
    candidates = (_candidate(0, ("a",), 0.5),)
    result = comparison_option(
        candidates=candidates,
        properties=props,
        coefficients=(),
        selected_index=None,
    )
    assert result["xAxis"]["type"] == "category"
    assert result["xAxis"]["data"] == ["Alpha", "Beta", "Gamma"]
