"""CriticalDecisionVM — read-only wrapper for a CriticalDecisionM result.

Per spec/viewmodels.md §4.6: result VMs are read-only, no commands.
"""

from __future__ import annotations

from guidearch.models.critical_decision import CriticalDecisionM


class CriticalDecisionVM:
    """Read-only view-model wrapper for a ranked critical decision."""

    def __init__(self, model: CriticalDecisionM) -> None:
        self._model = model

    @property
    def decision_id(self) -> str:
        return self._model.decision_id

    @property
    def score(self) -> float:
        return self._model.score

    @property
    def rank(self) -> int:
        return self._model.rank

    @property
    def model(self) -> CriticalDecisionM:
        return self._model
