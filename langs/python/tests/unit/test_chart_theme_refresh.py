from pathlib import Path

_MAIN = Path(__file__).resolve().parents[2] / "src" / "guidearch" / "main.py"
_SRC = _MAIN.read_text(encoding="utf-8")


def test_no_plan5_todo_marker_remains():
    assert "TODO(plan5)" not in _SRC, (
        "the plan5 chart-refresh deferral must be wired, not left as a marker"
    )


def test_theme_toggle_re_renders_the_results_container():
    # On a theme change, the results tab is cleared + re-rendered so the
    # mounted echarts rebuild from active_chart_tokens(theme). We assert the
    # index() body wires an app_vm theme subscription that re-renders
    # res_container (the same clear+re-render pattern used for candidates).
    idx = _SRC.index("def index(")
    body = _SRC[idx:]
    # A theme-change branch that touches res_container.
    assert "res_container.clear()" in body
    # There must be a theme-keyed re-render path (not only the candidates one).
    # Locate a handler that both checks the theme prop and re-renders results.
    assert '"theme"' in body or "== 'theme'" in body
    # The active-theme token getter is still the per-render color source.
    assert "active_chart_tokens" in body


def test_theme_subscription_is_disposed():
    # Any new app_vm subscription must be appended to _subs for disconnect cleanup.
    idx = _SRC.index("def index(")
    body = _SRC[idx:]
    assert body.count("_subs.append(") >= 3, (
        "the new theme→results subscription must be appended to _subs alongside "
        "the existing dark-mode and toggle-icon subscriptions"
    )
