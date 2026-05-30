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
    bar_data: list[dict[str, Any]] = []
    for i, cand in enumerate(top):
        # Gradient: full opacity at rank 0, half at max_items
        # Fraction from 0 (first) to 1 (last)
        frac = i / max(n - 1, 1)
        opacity = 1.0 - 0.5 * frac
        # Selection: highlighted bar uses accent colour; others dimmer
        is_selected = selected_index is not None and i == selected_index
        color = f"rgba(99,102,241,{opacity:.3f})"  # indigo
        if is_selected:
            color = "rgba(250,204,21,0.95)"  # amber highlight
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
        "backgroundColor": "transparent",
        "tooltip": {
            "trigger": "item",
            "formatter": "{b}",  # overridden by per-item tooltip via axis info
        },
        "grid": {"left": "8%", "right": "5%", "top": "5%", "bottom": "8%"},
        "xAxis": {
            "type": "value",
            "name": "Score",
            "max": round(max_score * 1.05, 6),
            "axisLabel": {"color": "#9ca3af", "fontSize": 10},
            "splitLine": {"lineStyle": {"color": "#374151"}},
            "nameTextStyle": {"color": "#9ca3af", "fontSize": 10},
        },
        "yAxis": {
            "type": "category",
            "data": y_labels,
            "name": "Rank",
            "inverse": True,
            "axisLabel": {"color": "#9ca3af", "fontSize": 10},
            "nameTextStyle": {"color": "#9ca3af", "fontSize": 10},
        },
        "series": [
            {
                "type": "bar",
                "data": bar_data,
                "emphasis": {"itemStyle": {"color": "rgba(250,204,21,0.95)"}},
            }
        ],
    }
    return option


# ---------------------------------------------------------------------------
# Chart B — fuzzy-value triangle visualizer
# ---------------------------------------------------------------------------

# Distinguishable colours for up to 10 properties
_PALETTE = [
    "#6366f1",  # indigo
    "#22d3ee",  # cyan
    "#a78bfa",  # violet
    "#34d399",  # emerald
    "#f59e0b",  # amber
    "#f87171",  # red
    "#60a5fa",  # blue
    "#fb923c",  # orange
    "#a3e635",  # lime
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
            "lineStyle": {"color": _PALETTE[0], "width": 2},
            "itemStyle": {"color": _PALETTE[0]},
            "areaStyle": {"color": _PALETTE[0], "opacity": 0.15},
            "symbol": "circle",
            "symbolSize": 5,
        }
    ]

    option: dict[str, Any] = {
        "backgroundColor": "transparent",
        "title": {
            "text": f"Rank {candidate.rank}   score {candidate.score:.6g}",
            "subtext": subtitle,
            "left": "center",
            "textStyle": {"color": "#e5e7eb", "fontSize": 11},
            "subtextStyle": {"color": "#6b7280", "fontSize": 9},
        },
        "tooltip": {"trigger": "axis"},
        "legend": {"show": False},
        "grid": {"left": "8%", "right": "5%", "top": "30%", "bottom": "10%"},
        "xAxis": {
            "type": "value",
            "name": "Value",
            "axisLabel": {"color": "#9ca3af", "fontSize": 10},
            "splitLine": {"lineStyle": {"color": "#374151"}},
            "nameTextStyle": {"color": "#9ca3af", "fontSize": 10},
        },
        "yAxis": {
            "type": "value",
            "name": "μ",
            "min": 0.0,
            "max": 1.1,
            "axisLabel": {"color": "#9ca3af", "fontSize": 10},
            "splitLine": {"lineStyle": {"color": "#374151"}},
            "nameTextStyle": {"color": "#9ca3af", "fontSize": 10},
        },
        "series": series,
    }
    return option
