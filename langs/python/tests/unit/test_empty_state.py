from pathlib import Path

_MAIN = Path(__file__).resolve().parents[2] / "src" / "guidearch" / "main.py"
_SRC = _MAIN.read_text(encoding="utf-8")


def _empty_state_body() -> str:
    start = _SRC.index("def _render_empty_state(")
    nxt = _SRC.find("\ndef ", start + 1)
    return _SRC[start : nxt if nxt != -1 else len(_SRC)]


def test_kicker_uses_0_08em_letter_spacing() -> None:
    body = _empty_state_body()
    assert "tracking-[0.08em]" in body, "kicker must use 0.08em letter-spacing (§5.8)"
    assert "tracking-widest" not in body, "kicker must not use tracking-widest"


def test_body_max_width_is_36rem() -> None:
    body = _empty_state_body()
    assert "max-w-[36rem]" in body, "empty-state body max-width must be 36rem (§5.8)"
    assert "max-w-2xl" not in body, "empty-state body must not use max-w-2xl"
