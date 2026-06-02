"""AlternativeVM — wraps an AlternativeM for the view layer.

Per spec/viewmodels.md §5.2:
  - Observable: id (read-only), decisionId (read-write), name (read-write).
  - Changing decisionId triggers a solve.
  - The solve trigger is handled by ScenarioVM subscribing to this VM's hub.
"""

from __future__ import annotations

from dataclasses import replace

from vmx.components.component_vm import ComponentVMOf
from vmx.messages.protocols import Message
from vmx.services.dispatcher import Dispatcher
from vmx.services.message_hub import MessageHub

from guidearch.models.alternative import AlternativeM


class AlternativeVM(ComponentVMOf[AlternativeM]):
    """ViewModel wrapping a single AlternativeM.

    Exposes ``id`` (read-only), ``decision_id`` (read-write — moves the
    alt between decisions, triggers re-solve), and ``alt_name`` (read-write).
    """

    def __init__(
        self,
        model: AlternativeM,
        hub: MessageHub[Message],
        dispatcher: Dispatcher,
    ) -> None:
        super().__init__(
            name=f"alternative-vm-{model.id}",
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
        """Alternative identifier (read-only)."""
        return self.model.id

    @property
    def decision_id(self) -> str:
        """Decision this alternative belongs to (read-write, triggers solve)."""
        return self.model.decision_id

    @decision_id.setter
    def decision_id(self, value: str) -> None:
        if self.model.decision_id != value:
            self.model = replace(self.model, decision_id=value)

    @property
    def alt_name(self) -> str:
        """Alternative display name (read-write)."""
        return self.model.name

    @alt_name.setter
    def alt_name(self, value: str) -> None:
        if self.model.name != value:
            self.model = replace(self.model, name=value)
