"""PropertyVM — wraps a PropertyM for the view layer.

Per spec/viewmodels.md §5.3:
  - Observable: id (read-only), name (read-write), kind (read-write), weight (read-write).
  - Changing kind or weight triggers a solve.
"""

from __future__ import annotations

from dataclasses import replace
from typing import Literal

from vmx.components.component_vm import ComponentVMOf
from vmx.messages.protocols import Message
from vmx.services.dispatcher import Dispatcher
from vmx.services.message_hub import MessageHub

from guidearch.models.property import PropertyM


class PropertyVM(ComponentVMOf[PropertyM]):
    """ViewModel wrapping a single PropertyM.

    Exposes ``id`` (read-only), ``prop_name`` (read-write), ``kind``
    (read-write, triggers solve), and ``weight`` (read-write, triggers solve).
    """

    def __init__(
        self,
        model: PropertyM,
        hub: MessageHub[Message],
        dispatcher: Dispatcher,
    ) -> None:
        super().__init__(
            name=f"property-vm-{model.id}",
            hint="",
            initial_model=model,
            modeled_hinter=lambda m: f"{m.name} ({m.kind}, w={m.weight})",
            on_model_changed=None,
            hub=hub,
            dispatcher=dispatcher,
        )

    # ── Observable properties ────────────────────────────────────────────────

    @property
    def id(self) -> str:
        """Property identifier (read-only)."""
        return self.model.id

    @property
    def prop_name(self) -> str:
        """Property display name (read-write)."""
        return self.model.name

    @prop_name.setter
    def prop_name(self, value: str) -> None:
        if self.model.name != value:
            self.model = replace(self.model, name=value)

    @property
    def kind(self) -> Literal["min", "max"]:
        """Optimization direction (read-write, triggers solve on change)."""
        return self.model.kind

    @kind.setter
    def kind(self, value: Literal["min", "max"]) -> None:
        if self.model.kind != value:
            self.model = replace(self.model, kind=value)

    @property
    def weight(self) -> float:
        """Priority weight > 0 (read-write, triggers solve on change)."""
        return self.model.weight

    @weight.setter
    def weight(self, value: float) -> None:
        if self.model.weight != value:
            self.model = replace(self.model, weight=value)
