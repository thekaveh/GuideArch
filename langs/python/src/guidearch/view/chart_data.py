"""chart_data — pure data-preparation functions for M4 charts.

These functions are decoupled from NiceGUI so they can be tested independently.
"""

from __future__ import annotations

from typing import Any

from guidearch.models.candidate import CandidateM
from guidearch.models.coefficient import CoefficientM
from guidearch.models.property import PropertyM
from guidearch.view.theme import TOKENS

# ---------------------------------------------------------------------------
# Chart A — ranked candidates score bar chart
# ---------------------------------------------------------------------------


def candidates_bar_option(
    candidates: tuple[CandidateM, ...],
    alt_name_map: dict[str, str],
    selected_index: int | None,
    max_items: int = 30,
    tokens: dict[str, str] = TOKENS,
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

    # Resolve per-theme colors from the token dict (§5.7).
    accent = tokens["accent"]
    accent_hover = tokens["accent-hover"]
    text_sec = tokens["text-secondary"]
    border_sub = tokens["border-subtle"]
    bg_surface = tokens["bg-surface"]
    bg_surface_3 = tokens["bg-surface-3"]
    border_strong = tokens["border-strong"]
    text_primary = tokens["text-primary"]

    bar_data: list[dict[str, Any]] = []
    for i, cand in enumerate(top):
        # Gradient: full opacity at rank 0, half opacity at rank n-1 —
        # spec/charts.md §2 fixes the floor at 0.5. C# ChartData.cs matches
        # (`1.0 - 0.5 * (i/(n-1))`); the previous 0.45 here put the floor at
        # 0.55 and the Python Chart A bars read noticeably crisper than C#'s
        # at the bottom of the list. Aligned 2026-06-05.
        frac = i / max(n - 1, 1)
        opacity = 1.0 - 0.5 * frac
        is_selected = selected_index is not None and i == selected_index
        # Selected: accent-hover highlight; others: accent at varying opacity
        if is_selected:
            color = accent_hover
        else:
            r, g, b = int(accent[1:3], 16), int(accent[3:5], 16), int(accent[5:7], 16)
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
        "backgroundColor": bg_surface,  # bg-surface per §5.7
        "tooltip": {
            "trigger": "item",
            "backgroundColor": bg_surface_3,
            "borderColor": border_strong,
            "textStyle": {"color": text_primary, "fontSize": 12},
        },
        "grid": {"left": "8%", "right": "5%", "top": "5%", "bottom": "10%", "containLabel": True},
        "xAxis": {
            "type": "value",
            "name": "Score",
            "max": round(max_score * 1.05, 6),
            "axisLabel": {"color": text_sec, "fontSize": 10},
            "splitLine": {"lineStyle": {"color": border_sub, "opacity": 0.5}},
            "nameTextStyle": {"color": text_sec, "fontSize": 10},
            "axisLine": {"lineStyle": {"color": border_sub}},
        },
        "yAxis": {
            "type": "category",
            "data": y_labels,
            "name": "Rank",
            "inverse": True,
            "axisLabel": {"color": text_sec, "fontSize": 10},
            "nameTextStyle": {"color": text_sec, "fontSize": 10},
            "axisLine": {"lineStyle": {"color": border_sub}},
        },
        "series": [
            {
                "type": "bar",
                "data": bar_data,
                "barMaxWidth": 16,
                "emphasis": {"itemStyle": {"color": accent_hover}},
            }
        ],
    }
    return option


# ---------------------------------------------------------------------------
# Chart B — fuzzy-value triangle visualizer
# ---------------------------------------------------------------------------


def triangle_option(
    candidate: CandidateM,
    properties: tuple[PropertyM, ...],
    coefficients: tuple[CoefficientM, ...],
    alt_name_map: dict[str, str],
    tokens: dict[str, str] = TOKENS,
) -> dict[str, Any]:
    """Return an ECharts option dict for Chart B (triangle visualizer).

    One line series per property (spec/charts.md §3): each series is a triangle
    with vertices ``(sumLower, 0) → (sumModal, 1) → (sumUpper, 0)`` where the
    sums are taken across the selected candidate's alternatives for that
    property's coefficient.

    Mirrors the TypeScript ``buildTriangleSeriesData`` and the C#
    ``PrepTriangleSeries`` helpers byte-for-byte on the input → triangle math.

    Parameters
    ----------
    candidate:
        The selected candidate whose per-property fuzzy decomposition to render.
    properties:
        All scenario properties, used for series names + legend ordering.
    coefficients:
        All scenario coefficients — looked up by ``(alternative_id, property_id)``.
    alt_name_map:
        Maps alternative_id → display name (for subtitle).
    """
    if not properties:
        return {}

    alt_labels = [alt_name_map.get(aid, aid[:8]) for aid in candidate.alternative_ids]
    subtitle = ", ".join(alt_labels[:4]) + (
        f", +{len(alt_labels) - 4}" if len(alt_labels) > 4 else ""
    )

    # Resolve per-theme colors from the token dict (§5.7).
    text_primary = tokens["text-primary"]
    text_sec = tokens["text-secondary"]
    border_sub = tokens["border-subtle"]
    border_strong = tokens["border-strong"]
    bg_surface = tokens["bg-surface"]
    bg_surface_3 = tokens["bg-surface-3"]

    # §5.7 — first three properties take the three fuzzy-axis tokens; rest cycle.
    palette = [
        tokens["fuzzy-positive"],
        tokens["fuzzy-average"],
        tokens["fuzzy-negative"],
        tokens["accent"],
        tokens["accent-hover"],
        tokens["info"],
        tokens["success"],
        tokens["warning"],
    ]

    # (alt_id, prop_id) → CoefficientM for O(1) lookup.
    coeff_index: dict[tuple[str, str], CoefficientM] = {
        (c.alternative_id, c.property_id): c for c in coefficients
    }
    alt_ids = candidate.alternative_ids

    series: list[dict[str, Any]] = []
    for idx, prop in enumerate(properties):
        sum_lower = 0.0
        sum_modal = 0.0
        sum_upper = 0.0
        for aid in alt_ids:
            coeff = coeff_index.get((aid, prop.id))
            if coeff is not None:
                sum_lower += coeff.value.lower
                sum_modal += coeff.value.modal
                sum_upper += coeff.value.upper
        color = palette[idx % len(palette)]
        series.append(
            {
                "type": "line",
                "name": prop.name,
                "data": [
                    [sum_lower, 0.0],
                    [sum_modal, 1.0],
                    [sum_upper, 0.0],
                ],
                "smooth": False,
                "lineStyle": {"color": color, "width": 2},
                "itemStyle": {"color": color},
                "areaStyle": {"color": color, "opacity": 0.12},
                "symbol": "circle",
                "symbolSize": 5,
            }
        )

    option: dict[str, Any] = {
        "backgroundColor": bg_surface,  # bg-surface per §5.7
        "title": {
            "text": f"Rank {candidate.rank}   score {candidate.score:.6g}",
            "subtext": subtitle,
            "left": "center",
            "textStyle": {"color": text_primary, "fontSize": 11, "fontWeight": "500"},
            "subtextStyle": {"color": text_sec, "fontSize": 9},
        },
        "tooltip": {
            "trigger": "axis",
            "backgroundColor": bg_surface_3,
            "borderColor": border_strong,
            "textStyle": {"color": text_primary, "fontSize": 11},
        },
        "legend": {
            "show": True,
            "type": "scroll",
            "right": "2%",
            "top": "middle",
            "orient": "vertical",
            "textStyle": {"color": text_sec, "fontSize": 10},
        },
        "grid": {"left": "8%", "right": "22%", "top": "20%", "bottom": "10%", "containLabel": True},
        "xAxis": {
            "type": "value",
            "name": "Value",
            "axisLabel": {"color": text_sec, "fontSize": 10},
            "splitLine": {"lineStyle": {"color": border_sub, "opacity": 0.5}},
            "nameTextStyle": {"color": text_sec, "fontSize": 10},
            "axisLine": {"lineStyle": {"color": border_sub}},
        },
        "yAxis": {
            "type": "value",
            "name": "μ",
            "min": 0.0,
            "max": 1.1,
            "axisLabel": {"color": text_sec, "fontSize": 10},
            "splitLine": {"lineStyle": {"color": border_sub, "opacity": 0.5}},
            "nameTextStyle": {"color": text_sec, "fontSize": 10},
            "axisLine": {"lineStyle": {"color": border_sub}},
        },
        "series": series,
    }
    return option


# ---------------------------------------------------------------------------
# Chart C — top-N candidate comparison polylines (legacy "color-coded
# comparison chart"). One polyline per top-N candidate; x = property index,
# y = sum of modal coefficients across the candidate's alternatives for
# that property. Same modal value Chart B uses for its peak — keeps the
# two charts numerically consistent.
# ---------------------------------------------------------------------------

# Stable 10-color qualitative palette (Tableau 10). Order matches TS and C#
# COMPARISON_PALETTE so cross-impl screenshots line up exactly.
COMPARISON_PALETTE: list[str] = [
    "#4e79a7",  # blue
    "#f28e2b",  # orange
    "#e15759",  # red
    "#76b7b2",  # teal
    "#59a14f",  # green
    "#edc948",  # yellow
    "#b07aa1",  # purple
    "#ff9da7",  # pink
    "#9c755f",  # brown
    "#bab0ac",  # grey
]

DEFAULT_COMPARISON_TOP_N = len(COMPARISON_PALETTE)


def comparison_option(
    candidates: tuple[CandidateM, ...],
    properties: tuple[PropertyM, ...],
    coefficients: tuple[CoefficientM, ...],
    selected_index: int | None,
    top_n: int = DEFAULT_COMPARISON_TOP_N,
    tokens: dict[str, str] = TOKENS,
) -> dict[str, Any]:
    """Return an ECharts option dict for Chart C (top-N candidate comparison).

    Parameters
    ----------
    candidates:
        Ranked candidates (already sorted rank-0 first).
    properties:
        Scenario properties (X axis).
    coefficients:
        All scenario coefficients (lookup source for per-property modal sums).
    selected_index:
        0-based rank of the highlighted candidate, or ``None``. Selected line
        is drawn full opacity / thicker; others fade out.
    top_n:
        Cap on the number of candidates shown. Defaults to palette size (10).
    """
    n = min(top_n, len(candidates), len(COMPARISON_PALETTE))
    if n == 0 or not properties:
        return {}

    # Resolve per-theme colors from the token dict (§5.7).
    # Series line colors stay COMPARISON_PALETTE[i] (Tableau-10 — do not theme).
    text_primary = tokens["text-primary"]
    text_sec = tokens["text-secondary"]
    border_sub = tokens["border-subtle"]
    border_strong = tokens["border-strong"]
    bg_surface = tokens["bg-surface"]
    bg_surface_3 = tokens["bg-surface-3"]

    # Coefficient lookup: (alt_id, prop_id) -> modal value.
    coeff_modal: dict[tuple[str, str], float] = {
        (c.alternative_id, c.property_id): c.value.modal for c in coefficients
    }

    prop_ids = [p.id for p in properties]
    prop_names = [p.name for p in properties]

    series: list[dict[str, Any]] = []
    for i in range(n):
        c = candidates[i]
        alt_ids = set(c.alternative_ids)

        # One y value per property: sum of modal coefficients for the
        # candidate's selected alternatives.
        ys = [sum(coeff_modal.get((aid, pid), 0.0) for aid in alt_ids) for pid in prop_ids]
        is_selected = selected_index is not None and i == selected_index
        # De-emphasise non-selected lines when a selection exists; full
        # opacity when none.
        opacity = 1.0 if selected_index is None else (1.0 if is_selected else 0.25)
        series.append(
            {
                "type": "line",
                "name": f"#{c.rank} ({c.score:.3g})",
                "data": list(zip(prop_names, ys, strict=True)),
                "smooth": False,
                "lineStyle": {
                    "color": COMPARISON_PALETTE[i],
                    "width": 2.5 if is_selected else 1.4,
                    "opacity": opacity,
                },
                "itemStyle": {"color": COMPARISON_PALETTE[i], "opacity": opacity},
                "symbol": "circle",
                "symbolSize": 5,
            }
        )

    option: dict[str, Any] = {
        "backgroundColor": bg_surface,  # bg-surface per §5.7
        "title": {
            "text": "Top 10 candidates — modal per property",
            "left": "center",
            "textStyle": {"color": text_primary, "fontSize": 11, "fontWeight": "500"},
        },
        "tooltip": {
            "trigger": "axis",
            "backgroundColor": bg_surface_3,
            "borderColor": border_strong,
            "textStyle": {"color": text_primary, "fontSize": 11},
        },
        "legend": {
            "show": True,
            "type": "scroll",
            "bottom": 0,
            "textStyle": {"color": text_sec, "fontSize": 10},
        },
        "grid": {
            "left": "6%",
            "right": "4%",
            "top": "18%",
            "bottom": "22%",
            "containLabel": True,
        },
        "xAxis": {
            "type": "category",
            "data": prop_names,
            "axisLabel": {"color": text_sec, "fontSize": 10},
            "splitLine": {"show": False},
            "axisLine": {"lineStyle": {"color": border_sub}},
        },
        "yAxis": {
            "type": "value",
            "name": "Modal sum",
            "axisLabel": {"color": text_sec, "fontSize": 10},
            "splitLine": {"lineStyle": {"color": border_sub, "opacity": 0.5}},
            "nameTextStyle": {"color": text_sec, "fontSize": 10},
            "axisLine": {"lineStyle": {"color": border_sub}},
        },
        "series": series,
    }
    return option
