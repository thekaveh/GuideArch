from pathlib import Path
from guidearch.view import theme

_MAIN = Path(__file__).resolve().parents[2] / "src" / "guidearch" / "main.py"
_MAIN_SRC = _MAIN.read_text(encoding="utf-8")
_THEME_SRC = Path(theme.__file__).read_text(encoding="utf-8")


def test_theme_defines_solve_gradient_rule():
    assert ".guidearch-solve" in _THEME_SRC, "theme.py must define a .guidearch-solve rule"
    assert "linear-gradient" in _THEME_SRC, "dark Solve must use an accent gradient"


def test_theme_defines_light_solve_override():
    assert "body--light .guidearch-solve" in _THEME_SRC, (
        "theme.py must define a body.body--light .guidearch-solve override"
    )


def test_solve_button_has_the_class():
    # The Solve ui.button is constructed with label "Solve"; assert it
    # carries the guidearch-solve class somewhere in its chained call.
    idx = _MAIN_SRC.index('"Solve",')
    window = _MAIN_SRC[idx : idx + 400]
    assert "guidearch-solve" in window, "Solve button must add .classes('guidearch-solve')"
