"""M3 cascade tests — spec/editors.md §2 delete-cascade rules.

Each test:
  1. Loads sas.json into a fresh ScenarioVM.
  2. Calls a delete operation.
  3. Asserts that the cascade removed all required entities.
  4. Asserts the scenario still satisfies cross-reference invariants.
"""

from __future__ import annotations

import math
from collections.abc import Generator
from pathlib import Path

import pytest

from guidearch.models.constraint import (
    ConflictConstraint,
    DependencyConstraint,
    ThresholdConstraint,
)
from guidearch.viewmodels.scenario_vm import (
    ScenarioMutationError,
    ScenarioVM,
    make_scenario_vm,
)

_REPO_ROOT = Path(__file__).parents[4]
_SAS_JSON = _REPO_ROOT / "spec" / "conformance" / "scenarios" / "sas.json"


@pytest.fixture()
def vm() -> Generator[ScenarioVM, None, None]:
    v = make_scenario_vm()
    v.open_cmd.execute(str(_SAS_JSON))
    yield v
    v.dispose()


# ── helpers ──────────────────────────────────────────────────────────────────


def _assert_cross_refs_valid(vm: ScenarioVM) -> None:
    """Assert all cross-reference invariants hold after a cascade."""
    s = vm.scenario
    assert s is not None
    decision_ids = {d.id for d in s.decisions}
    alt_ids = {a.id for a in s.alternatives}
    prop_ids = {p.id for p in s.properties}

    # 2.1 every alternative.decision_id must exist
    for a in s.alternatives:
        assert a.decision_id in decision_ids, (
            f"Alternative {a.id} references missing decision {a.decision_id}"
        )

    # 2.2 coefficient.alternative_id
    for c in s.coefficients:
        assert c.alternative_id in alt_ids, (
            f"Coefficient references missing alternative {c.alternative_id}"
        )

    # 2.3 coefficient.property_id
    for c in s.coefficients:
        assert c.property_id in prop_ids, f"Coefficient references missing property {c.property_id}"

    # 2.4/2.5 constraints
    for con in s.constraints:
        if isinstance(con, ThresholdConstraint):
            assert con.property_id in prop_ids, (
                f"ThresholdConstraint references missing property {con.property_id}"
            )
        elif isinstance(con, DependencyConstraint):
            assert con.source_alternative_id in alt_ids, (
                f"DependencyConstraint references missing alt {con.source_alternative_id}"
            )
            assert con.target_alternative_id in alt_ids, (
                f"DependencyConstraint references missing alt {con.target_alternative_id}"
            )
        elif isinstance(con, ConflictConstraint):
            assert con.alternative_a_id in alt_ids, (
                f"ConflictConstraint references missing alt {con.alternative_a_id}"
            )
            assert con.alternative_b_id in alt_ids, (
                f"ConflictConstraint references missing alt {con.alternative_b_id}"
            )


# ── test_delete_decision ──────────────────────────────────────────────────────


def test_delete_decision_removes_decision(vm: ScenarioVM) -> None:
    s = vm.scenario
    assert s is not None
    target = s.decisions[0]
    vm.delete_decision(target.id)
    assert vm.scenario is not None
    assert all(d.id != target.id for d in vm.scenario.decisions)


def test_delete_decision_removes_alternatives(vm: ScenarioVM) -> None:
    s = vm.scenario
    assert s is not None
    target = s.decisions[0]
    alt_ids_before = {a.id for a in s.alternatives if a.decision_id == target.id}
    assert alt_ids_before, "Test requires decision with alternatives"

    vm.delete_decision(target.id)
    assert vm.scenario is not None
    remaining_alt_ids = {a.id for a in vm.scenario.alternatives}
    for aid in alt_ids_before:
        assert aid not in remaining_alt_ids, f"Alternative {aid} should have been removed"


def test_delete_decision_removes_coefficients(vm: ScenarioVM) -> None:
    s = vm.scenario
    assert s is not None
    target = s.decisions[0]
    alt_ids = {a.id for a in s.alternatives if a.decision_id == target.id}

    vm.delete_decision(target.id)
    assert vm.scenario is not None
    for coef in vm.scenario.coefficients:
        assert coef.alternative_id not in alt_ids, (
            f"Coefficient for removed alternative {coef.alternative_id} still present"
        )


def test_delete_decision_removes_referencing_constraints(vm: ScenarioVM) -> None:
    """Add a dependency constraint referencing an alternative of the deleted
    decision, then delete the decision and assert the constraint is gone.
    """
    s = vm.scenario
    assert s is not None
    target = s.decisions[0]
    target_alts = [a for a in s.alternatives if a.decision_id == target.id]
    other_alts = [a for a in s.alternatives if a.decision_id != target.id]
    assert target_alts and other_alts, "Need at least one alt in each group"

    # Add a dependency constraint referencing the soon-to-be-deleted alternative
    vm.add_dependency_constraint(target_alts[0].id, other_alts[0].id)
    assert vm.scenario is not None
    count_before = len(vm.scenario.constraints)

    vm.delete_decision(target.id)
    assert vm.scenario is not None
    # Constraint should be gone
    assert len(vm.scenario.constraints) < count_before

    _assert_cross_refs_valid(vm)


def test_delete_decision_leaves_valid_cross_refs(vm: ScenarioVM) -> None:
    s = vm.scenario
    assert s is not None
    vm.delete_decision(s.decisions[0].id)
    _assert_cross_refs_valid(vm)


def test_delete_decision_not_found_raises(vm: ScenarioVM) -> None:
    with pytest.raises(ValueError, match="not found"):
        vm.delete_decision("d-nonexistent")


# ── test_delete_alternative ───────────────────────────────────────────────────


def test_delete_alternative_removes_alternative(vm: ScenarioVM) -> None:
    s = vm.scenario
    assert s is not None
    target = s.alternatives[0]
    vm.delete_alternative(target.id)
    assert vm.scenario is not None
    assert all(a.id != target.id for a in vm.scenario.alternatives)


def test_delete_alternative_removes_coefficients(vm: ScenarioVM) -> None:
    s = vm.scenario
    assert s is not None
    target = s.alternatives[0]
    vm.delete_alternative(target.id)
    assert vm.scenario is not None
    for coef in vm.scenario.coefficients:
        assert coef.alternative_id != target.id


def test_delete_alternative_removes_referencing_constraints(vm: ScenarioVM) -> None:
    s = vm.scenario
    assert s is not None
    alt_a = s.alternatives[0]
    if s.alternatives[0].decision_id != s.alternatives[1].decision_id:
        alt_b = s.alternatives[1]
    else:
        alt_b = s.alternatives[2]
    vm.add_conflict_constraint(alt_a.id, alt_b.id)
    assert vm.scenario is not None
    count_before = len(vm.scenario.constraints)

    vm.delete_alternative(alt_a.id)
    assert vm.scenario is not None
    assert len(vm.scenario.constraints) < count_before
    _assert_cross_refs_valid(vm)


def test_delete_alternative_not_found_raises(vm: ScenarioVM) -> None:
    with pytest.raises(ValueError, match="not found"):
        vm.delete_alternative("a-nonexistent")


# ── test_delete_property ──────────────────────────────────────────────────────


def test_delete_property_removes_property(vm: ScenarioVM) -> None:
    s = vm.scenario
    assert s is not None
    target = s.properties[0]
    vm.delete_property(target.id)
    assert vm.scenario is not None
    assert all(p.id != target.id for p in vm.scenario.properties)


def test_delete_property_removes_coefficients(vm: ScenarioVM) -> None:
    s = vm.scenario
    assert s is not None
    target = s.properties[0]
    vm.delete_property(target.id)
    assert vm.scenario is not None
    for coef in vm.scenario.coefficients:
        assert coef.property_id != target.id


def test_delete_property_removes_threshold_constraints(vm: ScenarioVM) -> None:
    s = vm.scenario
    assert s is not None
    target = s.properties[0]
    vm.add_threshold_constraint(target.id, min_val=0.0)
    assert vm.scenario is not None
    count_before = sum(1 for c in vm.scenario.constraints if isinstance(c, ThresholdConstraint))

    vm.delete_property(target.id)
    assert vm.scenario is not None
    count_after = sum(1 for c in vm.scenario.constraints if isinstance(c, ThresholdConstraint))
    assert count_after < count_before
    _assert_cross_refs_valid(vm)


def test_delete_property_not_found_raises(vm: ScenarioVM) -> None:
    with pytest.raises(ValueError, match="not found"):
        vm.delete_property("p-nonexistent")


# ── test coefficient completeness after add ───────────────────────────────────


def test_add_alternative_creates_zero_coefficients(vm: ScenarioVM) -> None:
    s = vm.scenario
    assert s is not None
    n_props = len(s.properties)
    d_id = s.decisions[0].id
    new_id = vm.add_alternative(d_id)
    assert vm.scenario is not None
    new_coefs = [c for c in vm.scenario.coefficients if c.alternative_id == new_id]
    assert len(new_coefs) == n_props
    for c in new_coefs:
        assert c.value.lower == 0.0
        assert c.value.modal == 0.0
        assert c.value.upper == 0.0


def test_add_property_creates_zero_coefficients(vm: ScenarioVM) -> None:
    s = vm.scenario
    assert s is not None
    n_alts = len(s.alternatives)
    new_id = vm.add_property()
    assert vm.scenario is not None
    new_coefs = [c for c in vm.scenario.coefficients if c.property_id == new_id]
    assert len(new_coefs) == n_alts


# ── update_property weight validation (parity with TS+C#) ───────────────────


def test_update_property_raises_on_non_positive_weight(vm: ScenarioVM) -> None:
    """spec/viewmodels.md §5.3 mandates weight > 0; ScenarioVM.update_property
    enforces it at the mutation boundary. TS `updatePropertyWeight` and C#
    `UpdateProperty` both have regression guards for this; Python lacked one.
    """
    s = vm.scenario
    assert s is not None
    prop_id = s.properties[0].id
    with pytest.raises(ScenarioMutationError):
        vm.update_property(prop_id, weight=0.0)
    with pytest.raises(ScenarioMutationError):
        vm.update_property(prop_id, weight=-1.0)


def test_add_property_raises_on_non_positive_weight(vm: ScenarioVM) -> None:
    """Same weight > 0 invariant enforced on the Add boundary, mirroring
    C# AddProperty's weight>0 check (ScenarioVMFactory.cs:574). Prior
    behavior accepted any float at Add-time and failed only at save-time
    schema validation.
    """
    with pytest.raises(ScenarioMutationError):
        vm.add_property(weight=0.0)
    with pytest.raises(ScenarioMutationError):
        vm.add_property(weight=-1.0)


def test_weight_and_coefficient_reject_non_finite_values(vm: ScenarioVM) -> None:
    """NaN <= 0 is False in Python (all NaN comparisons are), so the >0 guard
    alone would let NaN through and poison every downstream score with
    "Solved" status. Parity with C# ScenarioMutator and TS scenario-vm.
    """
    s = vm.scenario
    assert s is not None
    prop_id = s.properties[0].id
    coeff = s.coefficients[0]

    for bad in (math.nan, math.inf, -math.inf):
        with pytest.raises(ScenarioMutationError):
            vm.add_property(weight=bad)
        with pytest.raises(ScenarioMutationError):
            vm.update_property(prop_id, weight=bad)
        # Each non-finite component is rejected independently.
        with pytest.raises(ScenarioMutationError):
            vm.update_coefficient(coeff.alternative_id, coeff.property_id, bad, 1.0, 2.0)
        with pytest.raises(ScenarioMutationError):
            vm.update_coefficient(coeff.alternative_id, coeff.property_id, 0.0, bad, 2.0)
        with pytest.raises(ScenarioMutationError):
            vm.update_coefficient(coeff.alternative_id, coeff.property_id, 0.0, 1.0, bad)


# ── test dirty flag ───────────────────────────────────────────────────────────


def test_delete_decision_marks_dirty(vm: ScenarioVM) -> None:
    assert not vm.is_dirty
    s = vm.scenario
    assert s is not None
    vm.delete_decision(s.decisions[0].id)
    assert vm.is_dirty
