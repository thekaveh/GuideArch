"""Source-text guards for C3: theme-aware chart tokens + fuzzy-token triangle.

These tests read the source text of theme.py and chart_data.py directly so
they catch structural regressions without requiring a running NiceGUI / ECharts
environment (test_chart_data.py covers the runtime behaviour).
"""

from pathlib import Path

_ROOT = Path(__file__).resolve().parents[2] / "src" / "guidearch"
_THEME = (_ROOT / "view" / "theme.py").read_text(encoding="utf-8")
_CHART = (_ROOT / "view" / "chart_data.py").read_text(encoding="utf-8")


def _func_body(src: str, name: str) -> str:
    start = src.index(f"def {name}(")
    nxt = src.find("\ndef ", start + 1)
    return src[start : nxt if nxt != -1 else len(src)]


def test_theme_exposes_an_active_chart_token_getter() -> None:
    assert "def active_chart_tokens(" in _THEME
    body = _func_body(_THEME, "active_chart_tokens")
    assert "LIGHT_TOKENS" in body and "TOKENS" in body


def test_option_builders_accept_theme_tokens() -> None:
    for fn in ("candidates_bar_option", "triangle_option", "comparison_option"):
        sig = _CHART[_CHART.index(f"def {fn}(") : _CHART.index(f"def {fn}(") + 400]
        assert "tokens" in sig, f"{fn} must accept a tokens dict for theme-aware colors"


def test_chart_b_uses_the_three_fuzzy_tokens() -> None:
    body = _func_body(_CHART, "triangle_option")
    assert "fuzzy-positive" in body
    assert "fuzzy-average" in body
    assert "fuzzy-negative" in body


def test_dark_only_background_literal_is_gone_from_builders() -> None:
    # The dark bg-surface literal must no longer be baked into the options;
    # background now comes from the tokens dict.
    for fn in ("candidates_bar_option", "triangle_option", "comparison_option"):
        assert '"#13161d"' not in _func_body(_CHART, fn), (
            f"{fn} still hardcodes the dark bg-surface; read it from tokens"
        )
