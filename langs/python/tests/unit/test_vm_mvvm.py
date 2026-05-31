"""Headless MVVM integration tests for ScenarioVM.

These tests exercise ScenarioVM directly — no NiceGUI mounting required.
They validate MVVM separation: the ViewModel layer is fully testable without
any UI framework dependency.
"""

from __future__ import annotations

import json
import tempfile
from collections.abc import Generator
from pathlib import Path

import pytest

from guidearch.samples import SAMPLES
from guidearch.viewmodels.scenario_vm import ScenarioVM, make_scenario_vm

_SAS_PATH = str(SAMPLES[0]["path"])
_EDS_PATH = str(SAMPLES[1]["path"])

# Expected rank-0 score for SAS (from spec/conformance/expected/sas.candidates.json)
_SAS_RANK0_SCORE = 0.031180695179944085


@pytest.fixture()
def vm() -> Generator[ScenarioVM, None, None]:
    """Fresh ScenarioVM for each test."""
    v = make_scenario_vm()
    yield v
    v.dispose()


@pytest.fixture()
def vm_sas() -> Generator[ScenarioVM, None, None]:
    """ScenarioVM with SAS already loaded."""
    v = make_scenario_vm()
    v.open_cmd.execute(_SAS_PATH)
    yield v
    v.dispose()


# ---------------------------------------------------------------------------
# test_load_sample_sas_produces_correct_top_score
# ---------------------------------------------------------------------------


def test_load_sample_sas_produces_correct_top_score(vm: ScenarioVM) -> None:
    """Load the bundled SAS sample; candidates[0].score must match to 1e-9."""
    vm.open_cmd.execute(_SAS_PATH)

    assert vm.scenario is not None, "Scenario must be set after open"
    assert len(vm.candidates) > 0, "candidates must be non-empty after loading SAS"

    actual = vm.candidates[0].score
    assert abs(actual - _SAS_RANK0_SCORE) < 1e-9, (
        f"rank-0 score mismatch: got {actual!r}, expected {_SAS_RANK0_SCORE!r}"
    )


def test_load_sample_eds_produces_candidates(vm: ScenarioVM) -> None:
    """Load the bundled EDS sample; must produce at least one candidate."""
    vm.open_cmd.execute(_EDS_PATH)

    assert vm.scenario is not None, "Scenario must be set after open"
    assert len(vm.candidates) > 0, "EDS must produce at least one candidate"


# ---------------------------------------------------------------------------
# test_edit_property_weight_re_solves_and_changes_top
# ---------------------------------------------------------------------------


def test_edit_property_weight_re_solves_and_changes_top(vm_sas: ScenarioVM) -> None:
    """Mutate a property weight; the top candidate score must change."""
    original_score = vm_sas.candidates[0].score
    assert vm_sas.scenario is not None

    prop = vm_sas.scenario.properties[0]
    # Use a very different weight to guarantee a score change
    new_weight = prop.weight * 100.0 if prop.weight < 10 else 0.001

    vm_sas.update_property(prop.id, weight=new_weight)
    # update_property with weight triggers _apply_scenario_mutation → _do_solve
    new_score = vm_sas.candidates[0].score

    assert abs(new_score - original_score) > 1e-12, (
        "Changing property weight must change the top candidate score"
    )


# ---------------------------------------------------------------------------
# test_add_decision_add_alt_add_prop_save_reload_preserves_state
# ---------------------------------------------------------------------------


def test_add_decision_add_alt_add_prop_save_reload_preserves_state(
    vm: ScenarioVM,
) -> None:
    """Full round-trip: add entities, save to temp file, reload, verify counts."""
    # Start with a new scenario
    vm.new_cmd.execute()
    assert vm.scenario is not None

    # Add a decision
    dec_id = vm.add_decision("Arch Decision")
    # Add an alternative under that decision
    alt_id = vm.add_alternative(dec_id, "Option A")  # noqa: F841
    # Add a property
    prop_id = vm.add_property("Latency", kind="min", weight=2.0)  # noqa: F841

    scenario_before = vm.scenario
    n_decisions = len(scenario_before.decisions)
    n_alts = len(scenario_before.alternatives)
    n_props = len(scenario_before.properties)

    # Save to a temp file
    with tempfile.NamedTemporaryFile(suffix=".json", delete=False) as tmp:
        tmp_path = tmp.name

    vm.save_as_cmd.execute(tmp_path)
    assert Path(tmp_path).exists(), "Save must write the file"

    # Verify the file is valid JSON
    saved_data = json.loads(Path(tmp_path).read_text(encoding="utf-8"))
    assert saved_data["name"] == "New scenario"

    # Reload into a fresh VM
    vm2 = make_scenario_vm()
    try:
        vm2.open_cmd.execute(tmp_path)
        assert vm2.scenario is not None
        assert len(vm2.scenario.decisions) == n_decisions
        assert len(vm2.scenario.alternatives) == n_alts
        assert len(vm2.scenario.properties) == n_props
        # Verify specific entities survived
        assert any(d.name == "Arch Decision" for d in vm2.scenario.decisions)
        assert any(a.name == "Option A" for a in vm2.scenario.alternatives)
        assert any(p.name == "Latency" for p in vm2.scenario.properties)
        prop_reloaded = next(p for p in vm2.scenario.properties if p.name == "Latency")
        assert prop_reloaded.kind == "min"
        assert abs(prop_reloaded.weight - 2.0) < 1e-12
    finally:
        vm2.dispose()
        Path(tmp_path).unlink(missing_ok=True)


# ---------------------------------------------------------------------------
# test_delete_decision_cascades_correctly
# ---------------------------------------------------------------------------


def test_delete_decision_cascades_correctly(vm_sas: ScenarioVM) -> None:
    """Deleting a decision removes its alternatives, their coefficients,
    and any constraints that reference those alternatives."""
    assert vm_sas.scenario is not None
    scenario = vm_sas.scenario

    target_dec = scenario.decisions[0]
    affected_alt_ids = {a.id for a in scenario.alternatives if a.decision_id == target_dec.id}
    assert affected_alt_ids, "Target decision must have at least one alternative"

    # Count coefficients and constraints before
    coefs_before = len(scenario.coefficients)
    affected_coef_count = sum(
        1 for c in scenario.coefficients if c.alternative_id in affected_alt_ids
    )

    vm_sas.delete_decision(target_dec.id)

    assert vm_sas.scenario is not None
    s2 = vm_sas.scenario

    # Decision is gone
    assert all(d.id != target_dec.id for d in s2.decisions)

    # All affected alternatives are gone
    remaining_alt_ids = {a.id for a in s2.alternatives}
    for aid in affected_alt_ids:
        assert aid not in remaining_alt_ids

    # All affected coefficients are gone
    assert len(s2.coefficients) == coefs_before - affected_coef_count

    # No constraints reference the deleted alternatives
    from guidearch.models.constraint import ConflictConstraint, DependencyConstraint

    for c in s2.constraints:
        if isinstance(c, DependencyConstraint):
            assert c.source_alternative_id not in affected_alt_ids
            assert c.target_alternative_id not in affected_alt_ids
        elif isinstance(c, ConflictConstraint):
            assert c.alternative_a_id not in affected_alt_ids
            assert c.alternative_b_id not in affected_alt_ids

    # Cross-reference invariants hold
    decision_ids = {d.id for d in s2.decisions}
    for a in s2.alternatives:
        assert a.decision_id in decision_ids


# ---------------------------------------------------------------------------
# test_solve_trigger_skips_name_changes
# ---------------------------------------------------------------------------


def test_solve_trigger_skips_name_changes(vm_sas: ScenarioVM) -> None:
    """Changing scenario.name must NOT re-run solve (score stays identical)."""
    assert vm_sas.scenario is not None
    score_before = vm_sas.candidates[0].score

    # Track how many times solve runs by counting candidate tuple identity changes
    solve_count = 0
    original_candidates_id = id(vm_sas.candidates)

    vm_sas.update_scenario_name("Renamed Scenario")

    # Scenario name changed
    assert vm_sas.scenario is not None
    assert vm_sas.scenario.name == "Renamed Scenario"

    # candidates tuple identity must be unchanged — solve was NOT called
    assert id(vm_sas.candidates) == original_candidates_id, (
        "Name change must not replace the candidates tuple"
    )

    # Score unchanged confirms no re-solve
    assert abs(vm_sas.candidates[0].score - score_before) < 1e-15

    # Also verify decision name change skips solve
    dec = vm_sas.scenario.decisions[0]
    candidates_id_after_name = id(vm_sas.candidates)
    vm_sas.update_decision_name(dec.id, "Renamed Decision")
    assert id(vm_sas.candidates) == candidates_id_after_name, (
        "Decision name change must not replace the candidates tuple"
    )
    _ = solve_count  # suppress unused warning
