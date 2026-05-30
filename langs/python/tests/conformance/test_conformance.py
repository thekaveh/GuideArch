"""Conformance test — runs the conformance runner and asserts empty divergence list."""

from guidearch.conformance.runner import run_conformance


def test_conformance_passes() -> None:
    """Run full conformance suite. Fails if any difference is detected."""
    diffs = run_conformance()
    assert diffs == [], f"{len(diffs)} conformance difference(s):\n" + "\n".join(
        f"  {d}" for d in diffs
    )
