"""Decision domain model."""

from __future__ import annotations

from dataclasses import dataclass


@dataclass(frozen=True)
class DecisionM:
    """A choice point the architect needs to resolve."""

    id: str
    name: str
