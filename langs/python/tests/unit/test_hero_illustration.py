from pathlib import Path

_MAIN = Path(__file__).resolve().parents[2] / "src" / "guidearch" / "main.py"
_SRC = _MAIN.read_text(encoding="utf-8")


def _func_body(name: str) -> str:
    start = _SRC.index(f"def {name}(")
    nxt = _SRC.find("\ndef ", start + 1)
    return _SRC[start : nxt if nxt != -1 else len(_SRC)]


def test_hero_illustration_uses_the_accent_token_not_hardcoded_hex():
    body = _func_body("_hero_illustration_svg")
    assert "var(--accent)" in body, "hero motif must use var(--accent) so it retints"
    assert "#8b5cf6" not in body, "hero motif must not hardcode the dark accent hex"


def test_hero_illustration_keeps_the_three_triangle_opacities():
    body = _func_body("_hero_illustration_svg")
    for op in ("0.08", "0.16", "0.24"):
        assert op in body, f"hero motif must keep fill-opacity {op} (§6.2)"
