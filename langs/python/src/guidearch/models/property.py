"""Property domain model."""

from __future__ import annotations

from dataclasses import dataclass
from typing import Literal


@dataclass(frozen=True)
class PropertyM:
    """A quality attribute used to score candidates.

    kind: 'min' means lower is better; 'max' means higher is better.
    weight: positive priority scalar (called 'Priority' in legacy code).
    """

    id: str
    name: str
    kind: Literal["min", "max"]
    weight: float
