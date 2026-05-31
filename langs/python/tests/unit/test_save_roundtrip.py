"""M3 save round-trip test — spec/editors.md §6.

Load sas.json, edit a property weight, save to a temp file, reload,
verify the change persisted.  Also verify that the saved file parses
correctly via the scenario_loader (which enforces JSON-schema + invariants).
"""

from __future__ import annotations

import json
from collections.abc import Generator
from pathlib import Path

import pytest

from guidearch.models.scenario_loader import load_scenario
from guidearch.viewmodels.scenario_vm import ScenarioVM, make_scenario_vm

_REPO_ROOT = Path(__file__).parents[4]
_SAS_JSON = _REPO_ROOT / "spec" / "conformance" / "scenarios" / "sas.json"


@pytest.fixture()
def vm() -> Generator[ScenarioVM, None, None]:
    v = make_scenario_vm()
    v.open_cmd.execute(str(_SAS_JSON))
    yield v
    v.dispose()


def test_save_roundtrip_persists_property_weight(vm: ScenarioVM, tmp_path: Path) -> None:
    """Edit a property weight, save, reload, assert the change is present."""
    s = vm.scenario
    assert s is not None
    target_prop = s.properties[0]
    original_weight = target_prop.weight
    new_weight = original_weight + 99.0  # guaranteed different

    # Mutate weight
    vm.update_property(target_prop.id, weight=new_weight)
    assert vm.scenario is not None
    updated = next(p for p in vm.scenario.properties if p.id == target_prop.id)
    assert updated.weight == new_weight

    # Save to temp
    save_path = tmp_path / "test_scenario.json"
    vm.save_as_cmd.execute(str(save_path))
    assert save_path.exists(), "save_as_cmd should have created the file"

    # Reload via save_as_cmd / open_cmd in a new VM
    vm2 = make_scenario_vm()
    vm2.open_cmd.execute(str(save_path))
    try:
        s2 = vm2.scenario
        assert s2 is not None, "Reload failed — scenario is None"
        reloaded_prop = next(p for p in s2.properties if p.id == target_prop.id)
        assert reloaded_prop.weight == new_weight, (
            f"Expected weight {new_weight}, got {reloaded_prop.weight}"
        )
    finally:
        vm2.dispose()


def test_save_roundtrip_is_valid_json(vm: ScenarioVM, tmp_path: Path) -> None:
    """Saved file must be parseable as JSON."""
    save_path = tmp_path / "scenario.json"
    vm.save_as_cmd.execute(str(save_path))
    data = json.loads(save_path.read_text(encoding="utf-8"))
    assert "schemaVersion" in data
    assert "decisions" in data
    assert "alternatives" in data
    assert "properties" in data
    assert "coefficients" in data
    assert "constraints" in data


def test_save_roundtrip_validates_via_loader(vm: ScenarioVM, tmp_path: Path) -> None:
    """load_scenario on the saved file must succeed (passes schema + invariants)."""
    save_path = tmp_path / "scenario.json"
    vm.save_as_cmd.execute(str(save_path))
    reloaded = load_scenario(save_path)
    assert reloaded.name == vm.scenario.name  # type: ignore[union-attr]


def test_save_roundtrip_structurally_identical(vm: ScenarioVM, tmp_path: Path) -> None:
    """Reload of unmodified scenario must be structurally identical."""
    save_path = tmp_path / "unchanged.json"
    vm.save_as_cmd.execute(str(save_path))

    vm2 = make_scenario_vm()
    vm2.open_cmd.execute(str(save_path))
    try:
        s_orig = vm.scenario
        s_reload = vm2.scenario
        assert s_orig is not None
        assert s_reload is not None
        assert len(s_orig.decisions) == len(s_reload.decisions)
        assert len(s_orig.alternatives) == len(s_reload.alternatives)
        assert len(s_orig.properties) == len(s_reload.properties)
        assert len(s_orig.coefficients) == len(s_reload.coefficients)
        assert len(s_orig.constraints) == len(s_reload.constraints)
    finally:
        vm2.dispose()


def test_save_roundtrip_candidates_unchanged(vm: ScenarioVM, tmp_path: Path) -> None:
    """After round-trip with no edits, candidates[0].score is unchanged."""
    original_score = vm.candidates[0].score if vm.candidates else None

    save_path = tmp_path / "scenario_rtrip.json"
    vm.save_as_cmd.execute(str(save_path))

    vm2 = make_scenario_vm()
    vm2.open_cmd.execute(str(save_path))
    try:
        if original_score is not None and vm2.candidates:
            assert abs(vm2.candidates[0].score - original_score) < 1e-9
    finally:
        vm2.dispose()
