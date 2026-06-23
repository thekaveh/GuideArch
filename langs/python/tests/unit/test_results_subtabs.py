from pathlib import Path

_MAIN = Path(__file__).resolve().parents[2] / "src" / "guidearch" / "main.py"
_SRC = _MAIN.read_text(encoding="utf-8")


def _chart_tabs_line() -> str:
    # The right-rail chart sub-tab container is the ui.tabs(...) bound to
    # `chart_tabs`. Grab that statement's classes() argument.
    idx = _SRC.index("as chart_tabs:")
    start = _SRC.rindex("ui.tabs()", 0, idx)
    return _SRC[start:idx]


def test_chart_subtabs_not_a_filled_pill() -> None:
    line = _chart_tabs_line()
    assert "bg-[var(--bg-surface-2)]" not in line, (
        "right-rail sub-tabs must use the underline idiom, not a surface-2 pill (§6.4)"
    )


def test_chart_subtabs_carry_strip_hairline() -> None:
    line = _chart_tabs_line()
    assert "border-b" in line and "border-[var(--border-subtle)]" in line, (
        "right-rail sub-tab strip must carry the border-subtle underline"
    )
