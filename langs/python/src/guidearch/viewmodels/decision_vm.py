"""DecisionVM — wraps a DecisionM for the view layer.

Per spec/viewmodels.md §4.1:
  - Observable: id (read-only), name (read-write).
  - Mutating name updates the underlying ScenarioM.decisions[i].name.
  - Does NOT trigger a solve.
"""

from __future__ import annotations

from dataclasses import replace

from vmx.components.component_vm import ComponentVMOf
from vmx.messages.protocols import Message
from vmx.services.dispatcher import Dispatcher
from vmx.services.message_hub import MessageHub

from guidearch.models.decision import DecisionM


class DecisionVM(ComponentVMOf[DecisionM]):
    """ViewModel wrapping a single DecisionM.

    Exposes ``id`` (read-only) and ``name`` (read-write) as observable
    properties.  A name change emits PropertyChangedMessage("model") via
    the VMx base class but does NOT trigger a re-solve.
    """

    def __init__(
        self,
        model: DecisionM,
        hub: MessageHub[Message],
        dispatcher: Dispatcher,
    ) -> None:
        super().__init__(
            name=f"decision-vm-{model.id}",
            hint="",
            initial_model=model,
            modeled_hinter=lambda m: m.name,
            on_model_changed=None,
            hub=hub,
            dispatcher=dispatcher,
        )

    # ── Observable properties ────────────────────────────────────────────────

    @property
    def id(self) -> str:
        """Decision identifier (read-only)."""
        return self.model.id

    @property
    def decision_name(self) -> str:
        """Decision display name (read-write)."""
        return self.model.name

    @decision_name.setter
    def decision_name(self, value: str) -> None:
        if self.model.name != value:
            self.model = replace(self.model, name=value)
