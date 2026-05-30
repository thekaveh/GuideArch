"""Tests for chart_data module — pure data-prep functions (M4 §7).

Verifies:
- candidates_bar_option: rank order, score values, alternative name lookup,
  correct ECharts structure.
- triangle_option: correct series shape for selected candidate.
"""

from __future__ import annotations

from pathlib import Path

import pytest

from guidearch.models.candidate import CandidateM
from guidearch.models.normalized_fuzzy import NormalizedFuzzyM
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
    """The selected bar should have a different (amber) colour."""
    opt_sel = candidates_bar_option(_CANDIDATES, _ALT_MAP, selected_index=0)
    opt_none = candidates_bar_option(_CANDIDATES, _ALT_MAP, selected_index=None)

    data_sel = opt_sel["series"][0]["data"]
    data_none = opt_none["series"][0]["data"]

    # Selected bar colour differs from unselected default
    sel_color = data_sel[0]["itemStyle"]["color"]
    unsel_color = data_none[0]["itemStyle"]["color"]
    assert sel_color != unsel_color
    assert "250,204,21" in sel_color  # amber in rgba


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
# triangle_option
# ---------------------------------------------------------------------------


def test_triangle_option_returns_dict() -> None:
    cand = _CANDIDATES[0]
    opt = triangle_option(cand, ["prop_a", "prop_b"], _ALT_MAP)
    assert isinstance(opt, dict)


def test_triangle_option_has_series() -> None:
    cand = _CANDIDATES[0]
    opt = triangle_option(cand, ["prop_a"], _ALT_MAP)
    assert "series" in opt
    assert len(opt["series"]) >= 1


def test_triangle_option_series_type_line() -> None:
    cand = _CANDIDATES[0]
    opt = triangle_option(cand, ["prop_a"], _ALT_MAP)
    for s in opt["series"]:
        assert s["type"] == "line"


def test_triangle_option_triangle_shape() -> None:
    """Each series data must form a triangle: (lower,0), (modal,1), (upper,0)."""
    cand = _CANDIDATES[0]
    opt = triangle_option(cand, ["prop_a"], _ALT_MAP)
    tv = cand.triangular_value
    first_series = opt["series"][0]
    data = first_series["data"]
    assert len(data) == 3
    # Vertices
    assert data[0] == [tv.lower, 0.0]
    assert data[1] == [tv.modal, 1.0]
    assert data[2] == [tv.upper, 0.0]


def test_triangle_option_empty_properties_returns_empty() -> None:
    cand = _CANDIDATES[0]
    opt = triangle_option(cand, [], _ALT_MAP)
    assert opt == {}


def test_triangle_option_title_shows_rank_and_score() -> None:
    cand = _CANDIDATES[2]
    opt = triangle_option(cand, ["p"], _ALT_MAP)
    title_text = opt["title"]["text"]
    assert "2" in title_text  # rank 2
    assert "0.3" in title_text  # score 0.30


def test_triangle_option_subtitle_shows_alt_names() -> None:
    cand = _CANDIDATES[0]
    opt = triangle_option(cand, ["p"], _ALT_MAP)
    subtitle = opt["title"]["subtext"]
    assert "Alpha-1" in subtitle or "Beta-1" in subtitle


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
