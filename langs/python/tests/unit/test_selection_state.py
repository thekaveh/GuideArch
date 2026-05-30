"""Tests for ScenarioVM.selected_candidate_index observable (M4 §6).

Verifies:
- Default is None before any solve.
- Defaults to 0 after first solve with non-empty candidates.
- Setting the property notifies subscribers.
- Setting explicitly to None is allowed.
- Loading a new scenario resets to None, then re-solve sets it to 0.
"""

from __future__ import annotations

from collections.abc import Generator
from pathlib import Path

import pytest

from guidearch.viewmodels.scenario_vm import ScenarioVM, make_scenario_vm

_REPO_ROOT = Path(__file__).parents[4]
_SAS_JSON = _REPO_ROOT / "spec" / "conformance" / "scenarios" / "sas.json"


@pytest.fixture()
def vm() -> Generator[ScenarioVM, None, None]:
    v = make_scenario_vm()
    yield v
    v.dispose()


def test_initial_selected_candidate_index_is_none(vm: ScenarioVM) -> None:
    """Before any solve the index should be None."""
    assert vm.selected_candidate_index is None


def test_selected_candidate_defaults_to_zero_after_solve(vm: ScenarioVM) -> None:
    """After open+solve with non-empty candidates, index defaults to 0."""
    assert _SAS_JSON.exists(), f"sas.json not found at {_SAS_JSON}"
    vm.open_cmd.execute(str(_SAS_JSON))
    assert len(vm.candidates) > 0
    assert vm.selected_candidate_index == 0


def test_setter_notifies_subscriber(vm: ScenarioVM) -> None:
    """Setting selected_candidate_index emits 'selected_candidate_index' via property_changed."""
    vm.open_cmd.execute(str(_SAS_JSON))

    received: list[str] = []
    vm.property_changed.subscribe(on_next=received.append)

    vm.selected_candidate_index = 2
    assert "selected_candidate_index" in received


def test_setter_updates_value(vm: ScenarioVM) -> None:
    """Value is stored correctly."""
    vm.open_cmd.execute(str(_SAS_JSON))
    vm.selected_candidate_index = 5
    assert vm.selected_candidate_index == 5


def test_setter_accepts_none(vm: ScenarioVM) -> None:
    """Can set back to None explicitly."""
    vm.open_cmd.execute(str(_SAS_JSON))
    vm.selected_candidate_index = None
    assert vm.selected_candidate_index is None


def test_new_cmd_resets_selected_candidate_index(vm: ScenarioVM) -> None:
    """new_cmd clears candidates and resets selected index to None."""
    vm.open_cmd.execute(str(_SAS_JSON))
    assert vm.selected_candidate_index == 0

    vm.new_cmd.execute()
    assert vm.selected_candidate_index is None


def test_solve_keeps_existing_selection_if_valid(vm: ScenarioVM) -> None:
    """Re-solve does not reset selected_candidate_index if already set."""
    vm.open_cmd.execute(str(_SAS_JSON))
    # First solve sets to 0; change to 3
    vm.selected_candidate_index = 3
    # Re-solve: since _selected_candidate_index is not None it should stay
    vm.solve_cmd.execute()
    assert vm.selected_candidate_index == 3


def test_property_changed_fires_on_open(vm: ScenarioVM) -> None:
    """Open + auto-solve must emit 'selected_candidate_index' at some point."""
    received: list[str] = []
    vm.property_changed.subscribe(on_next=received.append)

    vm.open_cmd.execute(str(_SAS_JSON))
    assert "selected_candidate_index" in received
