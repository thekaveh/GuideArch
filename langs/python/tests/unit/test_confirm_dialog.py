from pathlib import Path

_MAIN = Path(__file__).resolve().parents[2] / "src" / "guidearch" / "main.py"
_SRC = _MAIN.read_text(encoding="utf-8")


def _func_body(name: str) -> str:
    """Source slice from `def name(` to the next top-level `def `."""
    start = _SRC.index(f"def {name}(")
    nxt = _SRC.find("\ndef ", start + 1)
    return _SRC[start : nxt if nxt != -1 else len(_SRC)]


def test_branded_dialog_helper_exists_with_spec_styling():
    body = _func_body("_branded_confirm_dialog")
    # §5.10 card: surface-3 fill, border-strong, max-width 28rem.
    assert "bg-[var(--bg-surface-3)]" in body
    assert "border-[var(--border-strong)]" in body
    assert "max-w-[28rem]" in body
    # Right-aligned buttons.
    assert "justify-end" in body
    # A title-row icon (danger triangle / info circle).
    assert "ui.icon(" in body
    # Esc/Enter keyboard handling.
    assert "keydown.esc" in body or "Escape" in body


def test_delete_flows_route_through_the_branded_helper():
    for fn in ("_do_delete_decision", "_do_delete_alternative", "_do_delete_property"):
        body = _func_body(fn)
        assert "_branded_confirm_dialog(" in body, (
            f"{fn} must use _branded_confirm_dialog (§5.10), not a raw ui.dialog()"
        )
        # The old hand-rolled `with ui.dialog() as dlg, ui.card()` is gone.
        assert "ui.card().classes(" not in body, (
            f"{fn} still hand-rolls a ui.card() dialog"
        )
