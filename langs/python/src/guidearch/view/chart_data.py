"""chart_data — pure data-preparation functions for M4 charts.

These functions are decoupled from NiceGUI so they can be tested independently.
"""

from __future__ import annotations

from typing import Any

from guidearch.models.candidate import CandidateM

# ---------------------------------------------------------------------------
# Chart A — ranked candidates score bar chart
# ---------------------------------------------------------------------------


def candidates_bar_option(
    candidates: tuple[CandidateM, ...],
    alt_name_map: dict[str, str],
    selected_index: int | None,
    max_items: int = 30,
) -> dict[str, Any]:
    """Return an ECharts option dict for Chart A (horizontal bar, top N candidates).

    Parameters
    ----------
    candidates:
        Ranked candidates (already sorted rank-0 first).
    alt_name_map:
        Maps alternative_id → display name.
    selected_index:
        0-based index into *candidates* for the currently-selected bar
        (highlighted with full opacity; others are dimmed slightly).
    max_items:
        Maximum number of candidates to show (default 30).
    """
    top = candidates[:max_items]
    n = len(top)
    if n == 0:
        return {}

    max_score = max(c.score for c in top) if top else 1.0

    # Y-axis labels: rank number
    y_labels = [str(c.rank) for c in top]

    # Bar data: score value, with itemStyle for gradient + selection highlight
    # Design-system tokens (§2.3 accent, §2.5 fuzzy axes)
    _ACCENT = "#8b5cf6"  # accent
    _ACCENT_MUTED = "#3d2a6b"  # accent-muted (selected highlight)
    _ACCENT_HOVER = "#a78bfa"  # accent-hover
    _TEXT_SEC = "#9298a8"  # text-secondary
    _BORDER_SUB = "#262b36"  # border-subtle

    bar_data: list[dict[str, Any]] = []
    for i, cand in enumerate(top):
        # Gradient: full opacity at rank 0, diminishing toward the end
        frac = i / max(n - 1, 1)
        opacity = 1.0 - 0.45 * frac
        is_selected = selected_index is not None and i == selected_index
        # Selected: accent-hover highlight; others: accent at varying opacity
        if is_selected:
            color = _ACCENT_HOVER
        else:
            r, g, b = 0x8B, 0x5C, 0xF6
            color = f"rgba({r},{g},{b},{opacity:.3f})"
        alt_names = [alt_name_map.get(aid, aid[:8]) for aid in cand.alternative_ids]
        bar_data.append(
            {
                "value": round(cand.score, 8),
                "itemStyle": {"color": color},
                "tooltip_rank": cand.rank,
                "tooltip_alts": ", ".join(alt_names[:6])
                + (f", +{len(alt_names) - 6}" if len(alt_names) > 6 else ""),
            }
        )

    option: dict[str, Any] = {
        "backgroundColor": "#13161d",  # bg-surface per §5.7
        "tooltip": {
            "trigger": "item",
            "backgroundColor": "#252a36",
            "borderColor": "#363c4a",
            "textStyle": {"color": "#e6e7ed", "fontSize": 12},
        },
        "grid": {"left": "8%", "right": "5%", "top": "5%", "bottom": "10%", "containLabel": True},
        "xAxis": {
            "type": "value",
            "name": "Score",
            "max": round(max_score * 1.05, 6),
            "axisLabel": {"color": _TEXT_SEC, "fontSize": 10},
            "splitLine": {"lineStyle": {"color": _BORDER_SUB, "opacity": 0.5}},
            "nameTextStyle": {"color": _TEXT_SEC, "fontSize": 10},
            "axisLine": {"lineStyle": {"color": _BORDER_SUB}},
        },
        "yAxis": {
            "type": "category",
            "data": y_labels,
            "name": "Rank",
            "inverse": True,
            "axisLabel": {"color": _TEXT_SEC, "fontSize": 10},
            "nameTextStyle": {"color": _TEXT_SEC, "fontSize": 10},
            "axisLine": {"lineStyle": {"color": _BORDER_SUB}},
        },
        "series": [
            {
                "type": "bar",
                "data": bar_data,
                "barMaxWidth": 16,
                "emphasis": {"itemStyle": {"color": _ACCENT_HOVER}},
            }
        ],
    }
    return option


# ---------------------------------------------------------------------------
# Chart B — fuzzy-value triangle visualizer
# ---------------------------------------------------------------------------

# Design-system tokens used by charts (§2.3 accent, §2.5 fuzzy axes)
_DS_ACCENT = "#8b5cf6"
_DS_TEXT_SEC = "#9298a8"
_DS_BORDER_SUB = "#262b36"
_DS_FUZZY_POS = "#34d399"  # fuzzy-positive
_DS_FUZZY_AVG = "#fbbf24"  # fuzzy-average
_DS_FUZZY_NEG = "#fb7185"  # fuzzy-negative

# Distinguishable colours for chart series (accent-based palette)
_PALETTE = [
    "#8b5cf6",  # accent (violet)
    "#34d399",  # fuzzy-positive (emerald)
    "#a78bfa",  # accent-hover
    "#fbbf24",  # fuzzy-average (amber)
    "#fb7185",  # fuzzy-negative (rose)
    "#60a5fa",  # blue
    "#f59e0b",  # warning (orange-amber)
    "#22d3ee",  # cyan
    "#fb923c",  # orange
    "#e879f9",  # fuchsia
]


def triangle_option(
    candidate: CandidateM,
    property_names: list[str],
    alt_name_map: dict[str, str],
) -> dict[str, Any]:
    """Return an ECharts option dict for Chart B (triangle visualizer).

    One line series per property (triangular shape: lower→0, modal→1, upper→0).
    Title shows rank + score.

    Parameters
    ----------
    candidate:
        The selected candidate whose triangular_value to decompose.
        (We show the *candidate*-level triangular value, not per-property;
         but the signature accepts property_names for future per-property use
         and for the legend.)
    property_names:
        Names of all properties (for the legend entries).
    alt_name_map:
        Maps alternative_id → display name (for subtitle).
    """
    if not property_names:
        return {}

    alt_labels = [alt_name_map.get(aid, aid[:8]) for aid in candidate.alternative_ids]
    subtitle = ", ".join(alt_labels[:4]) + (
        f", +{len(alt_labels) - 4}" if len(alt_labels) > 4 else ""
    )

    tv = candidate.triangular_value
    # We render the *aggregate* triangular value as a single triangle series.
    # If there are multiple properties, we add one series per property whose
    # lower/modal/upper we need.  For M4 we only have the aggregate value on
    # CandidateM, so we show just that.  Property names are used as legend
    # placeholder.
    # Use design-system fuzzy-axis colors (§2.5)
    series = [
        {
            "type": "line",
            "name": "aggregate",
            "data": [
                [tv.lower, 0.0],
                [tv.modal, 1.0],
                [tv.upper, 0.0],
            ],
            "smooth": False,
            "lineStyle": {"color": _DS_ACCENT, "width": 2},
            "itemStyle": {"color": _DS_ACCENT},
            "areaStyle": {"color": _DS_ACCENT, "opacity": 0.15},
            "symbol": "circle",
            "symbolSize": 5,
        }
    ]

    option: dict[str, Any] = {
        "backgroundColor": "#13161d",  # bg-surface per §5.7
        "title": {
            "text": f"Rank {candidate.rank}   score {candidate.score:.6g}",
            "subtext": subtitle,
            "left": "center",
            "textStyle": {"color": "#e6e7ed", "fontSize": 11, "fontWeight": "500"},
            "subtextStyle": {"color": _DS_TEXT_SEC, "fontSize": 9},
        },
        "tooltip": {
            "trigger": "axis",
            "backgroundColor": "#252a36",
            "borderColor": "#363c4a",
            "textStyle": {"color": "#e6e7ed", "fontSize": 11},
        },
        "legend": {"show": False},
        "grid": {"left": "8%", "right": "5%", "top": "28%", "bottom": "10%", "containLabel": True},
        "xAxis": {
            "type": "value",
            "name": "Value",
            "axisLabel": {"color": _DS_TEXT_SEC, "fontSize": 10},
            "splitLine": {"lineStyle": {"color": _DS_BORDER_SUB, "opacity": 0.5}},
            "nameTextStyle": {"color": _DS_TEXT_SEC, "fontSize": 10},
            "axisLine": {"lineStyle": {"color": _DS_BORDER_SUB}},
        },
        "yAxis": {
            "type": "value",
            "name": "μ",
            "min": 0.0,
            "max": 1.1,
            "axisLabel": {"color": _DS_TEXT_SEC, "fontSize": 10},
            "splitLine": {"lineStyle": {"color": _DS_BORDER_SUB, "opacity": 0.5}},
            "nameTextStyle": {"color": _DS_TEXT_SEC, "fontSize": 10},
            "axisLine": {"lineStyle": {"color": _DS_BORDER_SUB}},
        },
        "series": series,
    }
    return option
