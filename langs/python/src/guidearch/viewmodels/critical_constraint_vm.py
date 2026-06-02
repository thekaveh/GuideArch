"""CriticalConstraintVM — read-only wrapper for a CriticalConstraintM result.

Per spec/viewmodels.md §5.6: result VMs are read-only, no commands.
"""

from __future__ import annotations

from guidearch.models.critical_constraint import CriticalConstraintM


class CriticalConstraintVM:
    """Read-only view-model wrapper for a ranked critical constraint."""

    def __init__(self, model: CriticalConstraintM) -> None:
        self._model = model

    @property
    def constraint_index(self) -> int:
        return self._model.constraint_index

    @property
    def kind(self) -> str:
        return self._model.kind

    @property
    def eliminated(self) -> int:
        return self._model.eliminated

    @property
    def total(self) -> int:
        return self._model.total

    @property
    def redundant(self) -> bool:
        return self._model.redundant

    @property
    def model(self) -> CriticalConstraintM:
        return self._model
