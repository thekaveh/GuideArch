"""Constraint VMs — wraps ThresholdConstraint, DependencyConstraint, ConflictConstraint.

Per spec/viewmodels.md §5.5:
  - Three flavors, each ComponentVM<XxxConstraint>.
  - Edits trigger a solve.
"""

from __future__ import annotations

from dataclasses import replace

from vmx.components.component_vm import ComponentVMOf
from vmx.messages.protocols import Message
from vmx.services.dispatcher import Dispatcher
from vmx.services.message_hub import MessageHub

from guidearch.models.constraint import (
    ConflictConstraint,
    DependencyConstraint,
    ThresholdConstraint,
)


class ThresholdConstraintVM(ComponentVMOf[ThresholdConstraint]):
    """ViewModel for a threshold constraint."""

    def __init__(
        self,
        model: ThresholdConstraint,
        index: int,
        hub: MessageHub[Message],
        dispatcher: Dispatcher,
    ) -> None:
        self._index = index
        super().__init__(
            name=f"threshold-constraint-vm-{index}",
            hint="",
            initial_model=model,
            modeled_hinter=lambda m: f"threshold({m.property_id})",
            on_model_changed=None,
            hub=hub,
            dispatcher=dispatcher,
        )

    @property
    def constraint_index(self) -> int:
        return self._index

    @property
    def property_id(self) -> str:
        return self.model.property_id

    @property
    def min_value(self) -> float | None:
        return self.model.min

    @min_value.setter
    def min_value(self, value: float | None) -> None:
        if self.model.min != value:
            self.model = replace(self.model, min=value)

    @property
    def max_value(self) -> float | None:
        return self.model.max

    @max_value.setter
    def max_value(self, value: float | None) -> None:
        if self.model.max != value:
            self.model = replace(self.model, max=value)


class DependencyConstraintVM(ComponentVMOf[DependencyConstraint]):
    """ViewModel for a dependency (biconditional) constraint."""

    def __init__(
        self,
        model: DependencyConstraint,
        index: int,
        hub: MessageHub[Message],
        dispatcher: Dispatcher,
    ) -> None:
        self._index = index
        super().__init__(
            name=f"dependency-constraint-vm-{index}",
            hint="",
            initial_model=model,
            modeled_hinter=lambda m: (
                f"dependency({m.source_alternative_id} ↔ {m.target_alternative_id})"
            ),
            on_model_changed=None,
            hub=hub,
            dispatcher=dispatcher,
        )

    @property
    def constraint_index(self) -> int:
        return self._index

    @property
    def source_alternative_id(self) -> str:
        return self.model.source_alternative_id

    @source_alternative_id.setter
    def source_alternative_id(self, value: str) -> None:
        if self.model.source_alternative_id != value:
            self.model = replace(self.model, source_alternative_id=value)

    @property
    def target_alternative_id(self) -> str:
        return self.model.target_alternative_id

    @target_alternative_id.setter
    def target_alternative_id(self, value: str) -> None:
        if self.model.target_alternative_id != value:
            self.model = replace(self.model, target_alternative_id=value)


class ConflictConstraintVM(ComponentVMOf[ConflictConstraint]):
    """ViewModel for a conflict (mutual exclusion) constraint."""

    def __init__(
        self,
        model: ConflictConstraint,
        index: int,
        hub: MessageHub[Message],
        dispatcher: Dispatcher,
    ) -> None:
        self._index = index
        super().__init__(
            name=f"conflict-constraint-vm-{index}",
            hint="",
            initial_model=model,
            modeled_hinter=lambda m: f"conflict({m.alternative_a_id} ✗ {m.alternative_b_id})",
            on_model_changed=None,
            hub=hub,
            dispatcher=dispatcher,
        )

    @property
    def constraint_index(self) -> int:
        return self._index

    @property
    def alternative_a_id(self) -> str:
        return self.model.alternative_a_id

    @alternative_a_id.setter
    def alternative_a_id(self, value: str) -> None:
        if self.model.alternative_a_id != value:
            self.model = replace(self.model, alternative_a_id=value)

    @property
    def alternative_b_id(self) -> str:
        return self.model.alternative_b_id

    @alternative_b_id.setter
    def alternative_b_id(self, value: str) -> None:
        if self.model.alternative_b_id != value:
            self.model = replace(self.model, alternative_b_id=value)
