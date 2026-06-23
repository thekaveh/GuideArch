from pathlib import Path

_MAIN = Path(__file__).resolve().parents[2] / "src" / "guidearch" / "main.py"
_SRC = _MAIN.read_text(encoding="utf-8")


def _func_body(name: str) -> str:
    """Source slice from `def name(` to the next top-level `def `."""
    start = _SRC.index(f"def {name}(")
    nxt = _SRC.find("\ndef ", start + 1)
    return _SRC[start : nxt if nxt != -1 else len(_SRC)]


def test_section_header_applies_spec_padding() -> None:
    body = _func_body("_render_section_header")
    for cls in ("pt-4", "pr-6", "pb-3", "pl-6"):
        assert cls in body, f"_render_section_header missing §5.9 padding class {cls}"


def test_analysis_and_results_tabs_use_section_header() -> None:
    for fn in (
        "_render_critical_decisions_tab",
        "_render_critical_constraints_tab",
        "_render_results_tab",
    ):
        assert "_render_section_header(" in _func_body(fn), (
            f"{fn} must render its header via _render_section_header (§5.9)"
        )
