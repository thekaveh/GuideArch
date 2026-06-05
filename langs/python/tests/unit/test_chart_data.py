"""Tests for chart_data module — pure data-prep functions (M4 §7).

Verifies:
- candidates_bar_option: rank order, score values, alternative name lookup,
  correct ECharts structure.
- triangle_option: per-property triangle series (spec/charts.md §3) — one
  triangle per scenario property, vertices derived from summing coefficients
  across the candidate's alternatives.
"""

from __future__ import annotations

from pathlib import Path

import pytest

from guidearch.models.candidate import CandidateM
from guidearch.models.coefficient import CoefficientM
from guidearch.models.normalized_fuzzy import NormalizedFuzzyM
from guidearch.models.property import PropertyM
from guidearch.models.triangular_fuzzy import TriangularFuzzyM
from guidearch.view.chart_data import candidates_bar_option, triangle_option

_REPO_ROOT = Path(__file__).parents[4]
_SAS_JSON = _REPO_ROOT / "spec" / "conformance" / "scenarios" / "sas.json"


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------


def _make_candidate(rank: int, score: float, alt_ids: tuple[str, ...]) -> CandidateM:
    tv = TriangularFuzzyM(score * 0.8, score, score * 1.2)
    nv = NormalizedFuzzyM(score, 0.0, 1.0 - score)
    return CandidateM(
        alternative_ids=alt_ids,
        triangular_value=tv,
        normalized_value=nv,
        score=score,
        rank=rank,
    )


def _make_coeff(
    alt_id: str, prop_id: str, lower: float, modal: float, upper: float
) -> CoefficientM:
    return CoefficientM(
        alternative_id=alt_id,
        property_id=prop_id,
        value=TriangularFuzzyM(lower, modal, upper),
    )


_CANDIDATES = (
    _make_candidate(0, 0.10, ("a1", "b1")),
    _make_candidate(1, 0.20, ("a1", "b2")),
    _make_candidate(2, 0.30, ("a2", "b1")),
    _make_candidate(3, 0.40, ("a2", "b2")),
    _make_candidate(4, 0.50, ("a3", "b1")),
)

_ALT_MAP = {
    "a1": "Alpha-1",
    "a2": "Alpha-2",
    "a3": "Alpha-3",
    "b1": "Beta-1",
    "b2": "Beta-2",
}

_PROPS = (
    PropertyM(id="prop_a", name="Property A", kind="max", weight=1.0),
    PropertyM(id="prop_b", name="Property B", kind="max", weight=1.0),
)

# Coefficients: candidate (a1, b1) → prop_a sums to (0.1+0.3, 0.2+0.4, 0.3+0.5) = (0.4, 0.6, 0.8)
# and prop_b sums to (0.5+0.7, 0.6+0.8, 0.7+0.9) = (1.2, 1.4, 1.6).
_COEFFS = (
    _make_coeff("a1", "prop_a", 0.1, 0.2, 0.3),
    _make_coeff("a1", "prop_b", 0.5, 0.6, 0.7),
    _make_coeff("a2", "prop_a", 0.2, 0.3, 0.4),
    _make_coeff("a2", "prop_b", 0.4, 0.5, 0.6),
    _make_coeff("a3", "prop_a", 0.3, 0.4, 0.5),
    _make_coeff("a3", "prop_b", 0.3, 0.4, 0.5),
    _make_coeff("b1", "prop_a", 0.3, 0.4, 0.5),
    _make_coeff("b1", "prop_b", 0.7, 0.8, 0.9),
    _make_coeff("b2", "prop_a", 0.4, 0.5, 0.6),
    _make_coeff("b2", "prop_b", 0.6, 0.7, 0.8),
)


# ---------------------------------------------------------------------------
# candidates_bar_option
# ---------------------------------------------------------------------------


def test_bar_option_returns_dict() -> None:
    opt = candidates_bar_option(_CANDIDATES, _ALT_MAP, selected_index=0)
    assert isinstance(opt, dict)


def test_bar_option_empty_candidates_returns_empty() -> None:
    opt = candidates_bar_option((), _ALT_MAP, selected_index=None)
    assert opt == {}


def test_bar_option_has_series() -> None:
    opt = candidates_bar_option(_CANDIDATES, _ALT_MAP, selected_index=0)
    assert "series" in opt
    assert len(opt["series"]) == 1
    series = opt["series"][0]
    assert series["type"] == "bar"


def test_bar_option_data_count_respects_max_items() -> None:
    """max_items limits the number of bars."""
    opt = candidates_bar_option(_CANDIDATES, _ALT_MAP, selected_index=None, max_items=3)
    data = opt["series"][0]["data"]
    assert len(data) == 3


def test_bar_option_data_count_all_candidates() -> None:
    opt = candidates_bar_option(_CANDIDATES, _ALT_MAP, selected_index=None)
    data = opt["series"][0]["data"]
    assert len(data) == len(_CANDIDATES)


def test_bar_option_scores_preserve_order() -> None:
    """Bar values must be in the same rank order as candidates."""
    opt = candidates_bar_option(_CANDIDATES, _ALT_MAP, selected_index=None)
    data = opt["series"][0]["data"]
    values = [entry["value"] for entry in data]
    expected = [c.score for c in _CANDIDATES]
    for v, exp in zip(values, expected, strict=False):
        assert abs(v - exp) < 1e-9


def test_bar_option_y_labels_are_rank_strings() -> None:
    opt = candidates_bar_option(_CANDIDATES, _ALT_MAP, selected_index=None)
    y_labels = opt["yAxis"]["data"]
    expected_labels = [str(c.rank) for c in _CANDIDATES]
    assert y_labels == expected_labels


def test_bar_option_selected_bar_highlighted() -> None:
    """The selected bar should have a different colour from unselected bars.

    The design system uses accent-hover (#a78bfa) for the selected bar.
    """
    opt_sel = candidates_bar_option(_CANDIDATES, _ALT_MAP, selected_index=0)
    opt_none = candidates_bar_option(_CANDIDATES, _ALT_MAP, selected_index=None)

    data_sel = opt_sel["series"][0]["data"]
    data_none = opt_none["series"][0]["data"]

    # Selected bar colour differs from unselected default
    sel_color = data_sel[0]["itemStyle"]["color"]
    unsel_color = data_none[0]["itemStyle"]["color"]
    assert sel_color != unsel_color
    # Design system accent-hover (#a78bfa) is used for the selected bar
    assert "#a78bfa" in sel_color.lower() or "a78bfa" in sel_color.lower()


def test_bar_option_alt_names_in_tooltip_data() -> None:
    """tooltip_alts field must resolve alternative IDs to names."""
    opt = candidates_bar_option(_CANDIDATES, _ALT_MAP, selected_index=None)
    first_bar = opt["series"][0]["data"][0]
    assert "Alpha-1" in first_bar["tooltip_alts"]
    assert "Beta-1" in first_bar["tooltip_alts"]


def test_bar_option_axes_present() -> None:
    opt = candidates_bar_option(_CANDIDATES, _ALT_MAP, selected_index=0)
    assert "xAxis" in opt
    assert "yAxis" in opt
    assert opt["xAxis"]["type"] == "value"
    assert opt["yAxis"]["type"] == "category"


# ---------------------------------------------------------------------------
# triangle_option (per-property — spec/charts.md §3)
# ---------------------------------------------------------------------------


def test_triangle_option_returns_dict() -> None:
    cand = _CANDIDATES[0]
    opt = triangle_option(cand, _PROPS, _COEFFS, _ALT_MAP)
    assert isinstance(opt, dict)


def test_triangle_option_one_series_per_property() -> None:
    """spec/charts.md §3: one triangle (one series) per property."""
    cand = _CANDIDATES[0]
    opt = triangle_option(cand, _PROPS, _COEFFS, _ALT_MAP)
    assert "series" in opt
    assert len(opt["series"]) == len(_PROPS)


def test_triangle_option_series_type_line() -> None:
    cand = _CANDIDATES[0]
    opt = triangle_option(cand, _PROPS, _COEFFS, _ALT_MAP)
    for s in opt["series"]:
        assert s["type"] == "line"


def test_triangle_option_series_named_after_properties() -> None:
    """Legend entries (series names) come from PropertyM.name."""
    cand = _CANDIDATES[0]
    opt = triangle_option(cand, _PROPS, _COEFFS, _ALT_MAP)
    names = [s["name"] for s in opt["series"]]
    assert names == [p.name for p in _PROPS]


def test_triangle_option_triangle_shape_uses_per_property_sums() -> None:
    """Each series is a triangle: (sumLower,0), (sumModal,1), (sumUpper,0)
    where the sums are taken across the candidate's alternatives for that
    property — matches TS buildTriangleSeriesData + C# PrepTriangleSeries.
    """
    cand = _CANDIDATES[0]  # alternatives ("a1", "b1")
    opt = triangle_option(cand, _PROPS, _COEFFS, _ALT_MAP)

    # prop_a: (a1 + b1) = (0.1+0.3, 0.2+0.4, 0.3+0.5) = (0.4, 0.6, 0.8)
    prop_a_series = opt["series"][0]
    data = prop_a_series["data"]
    assert len(data) == 3
    assert data[0] == [pytest.approx(0.4), 0.0]
    assert data[1] == [pytest.approx(0.6), 1.0]
    assert data[2] == [pytest.approx(0.8), 0.0]

    # prop_b: (a1 + b1) = (0.5+0.7, 0.6+0.8, 0.7+0.9) = (1.2, 1.4, 1.6)
    prop_b_series = opt["series"][1]
    data = prop_b_series["data"]
    assert data[0] == [pytest.approx(1.2), 0.0]
    assert data[1] == [pytest.approx(1.4), 1.0]
    assert data[2] == [pytest.approx(1.6), 0.0]


def test_triangle_option_empty_properties_returns_empty() -> None:
    cand = _CANDIDATES[0]
    opt = triangle_option(cand, (), (), _ALT_MAP)
    assert opt == {}


def test_triangle_option_missing_coefficients_emit_zero_triangle() -> None:
    """A property with no matching coefficients still gets a series — a
    degenerate (0,0,0) triangle. Cross-impl: TS does the same in
    buildTriangleSeriesData when count==0."""
    cand = _CANDIDATES[0]
    opt = triangle_option(cand, _PROPS, (), _ALT_MAP)
    assert len(opt["series"]) == len(_PROPS)
    for s in opt["series"]:
        assert s["data"] == [[0.0, 0.0], [0.0, 1.0], [0.0, 0.0]]


def test_triangle_option_title_shows_rank_and_score() -> None:
    cand = _CANDIDATES[2]
    opt = triangle_option(cand, _PROPS, _COEFFS, _ALT_MAP)
    title_text = opt["title"]["text"]
    assert "2" in title_text  # rank 2
    assert "0.3" in title_text  # score 0.30


def test_triangle_option_subtitle_shows_alt_names() -> None:
    cand = _CANDIDATES[0]
    opt = triangle_option(cand, _PROPS, _COEFFS, _ALT_MAP)
    subtitle = opt["title"]["subtext"]
    assert "Alpha-1" in subtitle or "Beta-1" in subtitle


def test_triangle_option_legend_is_visible() -> None:
    """spec/charts.md §3: 'legend on the right' — must be visible so users
    can identify which series is which property."""
    cand = _CANDIDATES[0]
    opt = triangle_option(cand, _PROPS, _COEFFS, _ALT_MAP)
    assert opt["legend"]["show"] is True


def test_bar_option_with_real_sas() -> None:
    """Integration: load sas.json, run solve, check chart data shape."""
    from guidearch.viewmodels.scenario_vm import make_scenario_vm

    if not _SAS_JSON.exists():
        pytest.skip(f"sas.json not found at {_SAS_JSON}")

    vm = make_scenario_vm()
    try:
        vm.open_cmd.execute(str(_SAS_JSON))
        assert vm.candidates

        scenario = vm.scenario
        assert scenario is not None
        alt_map = {a.id: a.name for a in scenario.alternatives}

        opt = candidates_bar_option(vm.candidates, alt_map, selected_index=0, max_items=30)
        assert opt
        data = opt["series"][0]["data"]
        assert len(data) <= 30
        # Rank order preserved
        scores = [d["value"] for d in data]
        assert scores == sorted(scores)
    finally:
        vm.dispose()
