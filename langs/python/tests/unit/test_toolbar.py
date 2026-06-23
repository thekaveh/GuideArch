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


def test_toolbar_brand_svg_uses_accent_token_not_hardcoded_hex() -> None:
    region = _toolbar_region()
    assert "var(--accent)" in region, (
        "toolbar brand-mark polygons must fill via var(--accent) so they retint (§6.2)"
    )
    assert "#8b5cf6" not in region, (
        "toolbar brand-mark must not hardcode the dark accent hex #8b5cf6"
    )


def test_toolbar_brand_svg_keeps_the_three_triangle_opacities() -> None:
    region = _toolbar_region()
    for op in ("0.35", "0.6", "0.95"):
        assert op in region, f"toolbar brand-mark must keep fill-opacity {op} (§6.2)"


def test_toolbar_ghost_buttons_drop_color_white() -> None:
    region = _toolbar_region()
    assert "color=white" not in region, (
        "toolbar ghost buttons must use text-secondary, not color=white (§5.1)"
    )


def test_toolbar_ghost_buttons_use_text_secondary_token() -> None:
    region = _toolbar_region()
    assert "text-[var(--text-secondary)]" in region, (
        "ghost toolbar buttons must tint via the --text-secondary token"
    )
