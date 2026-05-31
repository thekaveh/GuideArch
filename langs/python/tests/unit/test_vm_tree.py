"""Structural tests: verify the VM tree exists with the expected shape.

Per spec/viewmodels.md §7 M2 conformance gate:
  "Each impl ships unit tests proving the VM tree exists with the right
   names, properties, and commands."

Uses Python introspection to check for the required observable properties
and command attributes on each VM class.
"""

from __future__ import annotations

import inspect

from guidearch.viewmodels import (
    AlternativeVM,
    CandidateVM,
    CoefficientVM,
    ConflictConstraintVM,
    CriticalConstraintVM,
    CriticalDecisionVM,
    DecisionVM,
    DependencyConstraintVM,
    PropertyVM,
    ScenarioVM,
    ThresholdConstraintVM,
    make_scenario_vm,
)

# ---------------------------------------------------------------------------
# Helper to check property/attribute existence
# ---------------------------------------------------------------------------


def _has_property(cls_or_instance: object, name: str) -> bool:
    """Return True if the class (or its instance) exposes *name* as a readable attr."""
    # Check via the class for properties
    klass = cls_or_instance if inspect.isclass(cls_or_instance) else type(cls_or_instance)
    # Walk the MRO to find the attribute
    return any(name in base.__dict__ for base in klass.__mro__)


def _has_attr(instance: object, name: str) -> bool:
    return hasattr(instance, name)


# ---------------------------------------------------------------------------
# ScenarioVM tree
# ---------------------------------------------------------------------------


class TestScenarioVMShape:
    def test_exists_in_package(self) -> None:
        assert ScenarioVM is not None

    def test_factory_returns_scenario_vm(self) -> None:
        vm = make_scenario_vm()
        try:
            assert isinstance(vm, ScenarioVM)
        finally:
            vm.dispose()

    def test_observable_properties(self) -> None:
        vm = make_scenario_vm()
        try:
            assert _has_property(vm, "scenario")
            assert _has_property(vm, "file_path")
            assert _has_property(vm, "is_dirty")
            assert _has_property(vm, "candidates")
            assert _has_property(vm, "critical_decisions")
            assert _has_property(vm, "critical_constraints")
            assert _has_property(vm, "status")
            assert _has_property(vm, "warnings")
        finally:
            vm.dispose()

    def test_commands(self) -> None:
        vm = make_scenario_vm()
        try:
            assert _has_attr(vm, "new_cmd")
            assert _has_attr(vm, "open_cmd")
            assert _has_attr(vm, "save_cmd")
            assert _has_attr(vm, "save_as_cmd")
            assert _has_attr(vm, "solve_cmd")
        finally:
            vm.dispose()

    def test_commands_are_callable(self) -> None:
        vm = make_scenario_vm()
        try:
            assert callable(vm.new_cmd.execute)
            assert callable(vm.open_cmd.execute)
            assert callable(vm.save_cmd.execute)
            assert callable(vm.save_as_cmd.execute)
            assert callable(vm.solve_cmd.execute)
        finally:
            vm.dispose()

    def test_property_changed_observable_exists(self) -> None:
        vm = make_scenario_vm()
        try:
            pc = vm.property_changed
            assert pc is not None
            # Should be subscribable
            events: list[str] = []
            sub = pc.subscribe(on_next=events.append)
            sub.dispose()
        finally:
            vm.dispose()


# ---------------------------------------------------------------------------
# DecisionVM
# ---------------------------------------------------------------------------


class TestDecisionVMShape:
    def _make(self) -> DecisionVM:
        from vmx import NULL_DISPATCHER, NULL_MESSAGE_HUB
        from vmx.messages.protocols import Message
        from vmx.services.message_hub import MessageHub

        from guidearch.models.decision import DecisionM

        hub: MessageHub[Message] = NULL_MESSAGE_HUB  # type: ignore[assignment]
        return DecisionVM(DecisionM(id="d1", name="DB"), hub, NULL_DISPATCHER)

    def test_exists(self) -> None:
        assert DecisionVM is not None

    def test_observable_properties(self) -> None:
        vm = self._make()
        assert _has_property(vm, "id")
        assert _has_property(vm, "decision_name")
        # id is read-only
        assert vm.id == "d1"
        assert vm.decision_name == "DB"

    def test_name_is_read_write(self) -> None:
        vm = self._make()
        vm.decision_name = "Updated DB"
        assert vm.decision_name == "Updated DB"


# ---------------------------------------------------------------------------
# AlternativeVM
# ---------------------------------------------------------------------------


class TestAlternativeVMShape:
    def _make(self) -> AlternativeVM:
        from vmx import NULL_DISPATCHER, NULL_MESSAGE_HUB
        from vmx.messages.protocols import Message
        from vmx.services.message_hub import MessageHub

        from guidearch.models.alternative import AlternativeM

        hub: MessageHub[Message] = NULL_MESSAGE_HUB  # type: ignore[assignment]
        model = AlternativeM(id="a1", decision_id="d1", name="Postgres")
        return AlternativeVM(model, hub, NULL_DISPATCHER)

    def test_observable_properties(self) -> None:
        vm = self._make()
        assert _has_property(vm, "id")
        assert _has_property(vm, "decision_id")
        assert _has_property(vm, "alt_name")
        assert vm.id == "a1"
        assert vm.decision_id == "d1"
        assert vm.alt_name == "Postgres"

    def test_decision_id_is_read_write(self) -> None:
        vm = self._make()
        vm.decision_id = "d2"
        assert vm.decision_id == "d2"


# ---------------------------------------------------------------------------
# PropertyVM
# ---------------------------------------------------------------------------


class TestPropertyVMShape:
    def _make(self) -> PropertyVM:
        from vmx import NULL_DISPATCHER, NULL_MESSAGE_HUB
        from vmx.messages.protocols import Message
        from vmx.services.message_hub import MessageHub

        from guidearch.models.property import PropertyM

        hub: MessageHub[Message] = NULL_MESSAGE_HUB  # type: ignore[assignment]
        model = PropertyM(id="p1", name="Cost", kind="min", weight=1.0)
        return PropertyVM(model, hub, NULL_DISPATCHER)

    def test_observable_properties(self) -> None:
        vm = self._make()
        assert _has_property(vm, "id")
        assert _has_property(vm, "prop_name")
        assert _has_property(vm, "kind")
        assert _has_property(vm, "weight")
        assert vm.id == "p1"
        assert vm.kind == "min"
        assert vm.weight == 1.0

    def test_kind_is_read_write(self) -> None:
        vm = self._make()
        vm.kind = "max"
        assert vm.kind == "max"

    def test_weight_is_read_write(self) -> None:
        vm = self._make()
        vm.weight = 2.5
        assert vm.weight == 2.5


# ---------------------------------------------------------------------------
# CoefficientVM
# ---------------------------------------------------------------------------


class TestCoefficientVMShape:
    def _make(self) -> CoefficientVM:
        from vmx import NULL_DISPATCHER, NULL_MESSAGE_HUB
        from vmx.messages.protocols import Message
        from vmx.services.message_hub import MessageHub

        from guidearch.models.coefficient import CoefficientM
        from guidearch.models.triangular_fuzzy import TriangularFuzzyM

        hub: MessageHub[Message] = NULL_MESSAGE_HUB  # type: ignore[assignment]
        return CoefficientVM(
            CoefficientM(
                alternative_id="a1",
                property_id="p1",
                value=TriangularFuzzyM(lower=1.0, modal=2.0, upper=3.0),
            ),
            hub,
            NULL_DISPATCHER,
        )

    def test_observable_properties(self) -> None:
        vm = self._make()
        assert _has_property(vm, "lower")
        assert _has_property(vm, "modal")
        assert _has_property(vm, "upper")
        assert vm.lower == 1.0
        assert vm.modal == 2.0
        assert vm.upper == 3.0

    def test_lower_is_read_write(self) -> None:
        vm = self._make()
        vm.lower = 0.5
        assert vm.lower == 0.5


# ---------------------------------------------------------------------------
# Constraint VMs
# ---------------------------------------------------------------------------


class TestConstraintVMShapes:
    def test_threshold_constraint_vm_exists(self) -> None:
        assert ThresholdConstraintVM is not None

    def test_dependency_constraint_vm_exists(self) -> None:
        assert DependencyConstraintVM is not None

    def test_conflict_constraint_vm_exists(self) -> None:
        assert ConflictConstraintVM is not None

    def test_threshold_properties(self) -> None:
        from vmx import NULL_DISPATCHER, NULL_MESSAGE_HUB
        from vmx.messages.protocols import Message
        from vmx.services.message_hub import MessageHub

        from guidearch.models.constraint import ThresholdConstraint

        hub: MessageHub[Message] = NULL_MESSAGE_HUB  # type: ignore[assignment]
        vm = ThresholdConstraintVM(
            ThresholdConstraint(kind="threshold", property_id="p1", min=None, max=5.0),
            index=0,
            hub=hub,
            dispatcher=NULL_DISPATCHER,
        )
        assert _has_property(vm, "property_id")
        assert _has_property(vm, "min_value")
        assert _has_property(vm, "max_value")
        assert vm.max_value == 5.0


# ---------------------------------------------------------------------------
# Result VMs (read-only)
# ---------------------------------------------------------------------------


class TestResultVMShapes:
    def test_candidate_vm_shape(self) -> None:
        from guidearch.models.candidate import CandidateM
        from guidearch.models.normalized_fuzzy import NormalizedFuzzyM
        from guidearch.models.triangular_fuzzy import TriangularFuzzyM

        c = CandidateM(
            alternative_ids=("a1", "a2"),
            triangular_value=TriangularFuzzyM(0.0, 1.0, 2.0),
            normalized_value=NormalizedFuzzyM(0.1, 0.5, 0.4),
            score=0.3,
            rank=0,
        )
        vm = CandidateVM(c)
        assert _has_property(vm, "rank")
        assert _has_property(vm, "score")
        assert _has_property(vm, "alternative_ids")
        assert vm.rank == 0
        assert vm.score == 0.3
        assert vm.alternative_ids == ("a1", "a2")

    def test_critical_decision_vm_shape(self) -> None:
        from guidearch.models.critical_decision import CriticalDecisionM
        from guidearch.models.normalized_fuzzy import NormalizedFuzzyM
        from guidearch.models.triangular_fuzzy import TriangularFuzzyM

        m = CriticalDecisionM(
            decision_id="d1",
            triangular_value=TriangularFuzzyM(0.0, 1.0, 2.0),
            normalized_value=NormalizedFuzzyM(0.1, 0.5, 0.4),
            score=0.2,
            rank=0,
        )
        vm = CriticalDecisionVM(m)
        assert _has_property(vm, "decision_id")
        assert _has_property(vm, "score")
        assert _has_property(vm, "rank")

    def test_critical_constraint_vm_shape(self) -> None:
        from guidearch.models.critical_constraint import CriticalConstraintM

        m = CriticalConstraintM(
            constraint_index=0,
            kind="threshold",
            eliminated=5,
            total=100,
            redundant=False,
        )
        vm = CriticalConstraintVM(m)
        assert _has_property(vm, "constraint_index")
        assert _has_property(vm, "kind")
        assert _has_property(vm, "eliminated")
        assert _has_property(vm, "total")
        assert _has_property(vm, "redundant")
