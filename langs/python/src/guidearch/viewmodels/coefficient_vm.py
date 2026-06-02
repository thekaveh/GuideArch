"""CoefficientVM — wraps a CoefficientM (one cell in the alt x property grid).

Per spec/viewmodels.md §5.4:
  - Flat list CoefficientCellVM[] indexed by (alternativeId, propertyId).
  - Each cell exposes lower, modal, upper as read-write doubles.
  - Editing any cell triggers a solve.
"""

from __future__ import annotations

from dataclasses import replace

from vmx.components.component_vm import ComponentVMOf
from vmx.messages.protocols import Message
from vmx.services.dispatcher import Dispatcher
from vmx.services.message_hub import MessageHub

from guidearch.models.coefficient import CoefficientM
from guidearch.models.triangular_fuzzy import TriangularFuzzyM


class CoefficientVM(ComponentVMOf[CoefficientM]):
    """ViewModel for one (alternative, property) fuzzy coefficient cell.

    Exposes ``lower``, ``modal``, ``upper`` as read-write doubles.
    Any mutation emits PropertyChangedMessage("model") which ScenarioVM
    uses to trigger a re-solve.
    """

    def __init__(
        self,
        model: CoefficientM,
        hub: MessageHub[Message],
        dispatcher: Dispatcher,
    ) -> None:
        super().__init__(
            name=f"coefficient-vm-{model.alternative_id}-{model.property_id}",
            hint="",
            initial_model=model,
            modeled_hinter=lambda m: f"[{m.value.lower}, {m.value.modal}, {m.value.upper}]",
            on_model_changed=None,
            hub=hub,
            dispatcher=dispatcher,
        )

    # ── Observable properties ────────────────────────────────────────────────

    @property
    def alternative_id(self) -> str:
        return self.model.alternative_id

    @property
    def property_id(self) -> str:
        return self.model.property_id

    @property
    def lower(self) -> float:
        return self.model.value.lower

    @lower.setter
    def lower(self, value: float) -> None:
        old = self.model.value
        if old.lower != value:
            self.model = replace(
                self.model,
                value=TriangularFuzzyM(lower=value, modal=old.modal, upper=old.upper),
            )

    @property
    def modal(self) -> float:
        return self.model.value.modal

    @modal.setter
    def modal(self, value: float) -> None:
        old = self.model.value
        if old.modal != value:
            self.model = replace(
                self.model,
                value=TriangularFuzzyM(lower=old.lower, modal=value, upper=old.upper),
            )

    @property
    def upper(self) -> float:
        return self.model.value.upper

    @upper.setter
    def upper(self, value: float) -> None:
        old = self.model.value
        if old.upper != value:
            self.model = replace(
                self.model,
                value=TriangularFuzzyM(lower=old.lower, modal=old.modal, upper=value),
            )
