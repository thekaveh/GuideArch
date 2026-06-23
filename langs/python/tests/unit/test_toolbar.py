from pathlib import Path

_MAIN = Path(__file__).resolve().parents[2] / "src" / "guidearch" / "main.py"
_SRC = _MAIN.read_text(encoding="utf-8")


def _toolbar_region() -> str:
    # The toolbar is built between the "── Toolbar (§6" comment and the
    # "── Tab strip" comment. Slice that window so the assertion only
    # covers toolbar buttons, not unrelated code.
    start = _SRC.index("── Toolbar (§6")
    end = _SRC.index("── Tab strip")
    return _SRC[start:end]


def test_toolbar_ghost_buttons_drop_color_white():
    region = _toolbar_region()
    assert "color=white" not in region, (
        "toolbar ghost buttons must use text-secondary, not color=white (§5.1)"
    )


def test_toolbar_ghost_buttons_use_text_secondary_token():
    region = _toolbar_region()
    assert "text-[var(--text-secondary)]" in region, (
        "ghost toolbar buttons must tint via the --text-secondary token"
    )
