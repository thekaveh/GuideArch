"""CandidateVM — read-only wrapper for a CandidateM result.

Per spec/viewmodels.md §5.6: result VMs are read-only, no commands, no
mutation paths.
"""

from __future__ import annotations

from guidearch.models.candidate import CandidateM


class CandidateVM:
    """Read-only view-model wrapper for a ranked architecture candidate."""

    def __init__(self, model: CandidateM) -> None:
        self._model = model

    @property
    def rank(self) -> int:
        return self._model.rank

    @property
    def score(self) -> float:
        return self._model.score

    @property
    def alternative_ids(self) -> tuple[str, ...]:
        return self._model.alternative_ids

    @property
    def model(self) -> CandidateM:
        return self._model
