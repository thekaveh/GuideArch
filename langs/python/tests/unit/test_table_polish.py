from pathlib import Path

_ROOT = Path(__file__).resolve().parents[2] / "src" / "guidearch"
_THEME = (_ROOT / "view" / "theme.py").read_text(encoding="utf-8")
_MAIN = (_ROOT / "main.py").read_text(encoding="utf-8")


def test_table_header_is_sticky():
    # The .q-table th block must pin on scroll (§5.3 sticky header).
    start = _THEME.index(".q-table th")
    block = _THEME[start : start + 240]
    assert "position: sticky" in block, "table header must be sticky (§5.3)"


def test_selected_row_has_accent_left_border():
    # The selected candidate row carries the accent-muted fill AND a 2px
    # accent left border (§5.3 selected-row idiom).
    idx = _MAIN.index("bg-[var(--accent-muted)]")
    region = _MAIN[idx - 40 : idx + 120]
    assert "border-l-2" in region and "border-[var(--accent)]" in region, (
        "selected row must add a 2px accent left border alongside accent-muted"
    )
