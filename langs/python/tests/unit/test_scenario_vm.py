"""Integration test: ScenarioVM loads sas.json and produces correct candidates.

Spec M2 conformance gate (spec/viewmodels.md §7):
- Build a VM, load sas.json via open_cmd.
- Assert len(candidates) > 0.
- Assert candidates[0].score matches sas.candidates.json rank-0 score to 1e-9.
"""

from __future__ import annotations

import json
from collections.abc import Generator
from pathlib import Path

import pytest

from guidearch.viewmodels.scenario_vm import ScenarioVM, make_scenario_vm

# Paths relative to repo root
# parents: [0]=unit, [1]=tests, [2]=python, [3]=langs, [4]=GuideArch(root)
_REPO_ROOT = Path(__file__).parents[4]
_SAS_JSON = _REPO_ROOT / "spec" / "conformance" / "scenarios" / "sas.json"
_EXPECTED_JSON = _REPO_ROOT / "spec" / "conformance" / "expected" / "sas.candidates.json"


@pytest.fixture()
def vm() -> Generator[ScenarioVM, None, None]:
    """Create a fresh ScenarioVM for each test."""
    v = make_scenario_vm()
    yield v
    v.dispose()


def test_initial_state(vm: ScenarioVM) -> None:
    """VM starts with no scenario and empty candidates list."""
    assert vm.scenario is None
    assert vm.candidates == ()
    assert vm.critical_decisions_result == ()
    assert vm.critical_constraints_result == ()
    assert vm.file_path is None
    assert vm.is_dirty is False


def test_open_cmd_loads_scenario(vm: ScenarioVM) -> None:
    """open_cmd sets scenario and runs solve."""
    assert _SAS_JSON.exists(), f"sas.json not found at {_SAS_JSON}"
    vm.open_cmd.execute(str(_SAS_JSON))

    assert vm.scenario is not None
    assert vm.scenario.name == "SAS"
    assert vm.file_path == str(_SAS_JSON)
    assert vm.is_dirty is False


def test_candidates_populated_after_open(vm: ScenarioVM) -> None:
    """After open_cmd, candidates must be non-empty."""
    vm.open_cmd.execute(str(_SAS_JSON))
    assert len(vm.candidates) > 0


def test_candidates_rank0_score_matches_expected(vm: ScenarioVM) -> None:
    """candidates[0].score must match sas.candidates.json rank-0 score to 1e-9."""
    assert _EXPECTED_JSON.exists(), f"expected output not found at {_EXPECTED_JSON}"
    vm.open_cmd.execute(str(_SAS_JSON))

    assert len(vm.candidates) > 0, "candidates must be non-empty after open"

    expected_data = json.loads(_EXPECTED_JSON.read_text(encoding="utf-8"))
    # Find the rank-0 entry
    rank0_entry = next((e for e in expected_data if e["rank"] == 0), None)
    assert rank0_entry is not None, "No rank-0 entry in expected output"
    expected_score = rank0_entry["score"]

    actual_score = vm.candidates[0].score
    assert abs(actual_score - expected_score) < 1e-9, (
        f"candidates[0].score = {actual_score!r}, "
        f"expected {expected_score!r}, "
        f"diff = {abs(actual_score - expected_score)}"
    )


def test_open_invalid_path_emits_warning(vm: ScenarioVM) -> None:
    """open_cmd with a bad path emits a warning and does not mutate scenario."""
    vm.open_cmd.execute("/nonexistent/path/to/scenario.json")
    assert vm.scenario is None
    assert len(vm.warnings) > 0


def test_solve_cmd_reruns(vm: ScenarioVM) -> None:
    """After open, calling solve_cmd again produces the same result."""
    vm.open_cmd.execute(str(_SAS_JSON))
    first_candidates = vm.candidates

    vm.solve_cmd.execute()
    second_candidates = vm.candidates

    assert len(first_candidates) == len(second_candidates)
    if first_candidates:
        assert abs(first_candidates[0].score - second_candidates[0].score) < 1e-12


def test_status_reflects_scenario(vm: ScenarioVM) -> None:
    """Status string is updated after load."""
    initial_status = vm.status
    vm.open_cmd.execute(str(_SAS_JSON))
    assert vm.status != initial_status
    assert "SAS" in vm.status or len(vm.candidates) > 0


def test_new_cmd_clears_scenario(vm: ScenarioVM) -> None:
    """new_cmd creates an empty scenario without candidates."""
    vm.open_cmd.execute(str(_SAS_JSON))
    assert vm.candidates  # has candidates from sas

    vm.new_cmd.execute()
    assert vm.scenario is not None
    assert vm.scenario.name == "New scenario"
    assert vm.candidates == ()
    assert vm.file_path is None
    assert vm.is_dirty is False
