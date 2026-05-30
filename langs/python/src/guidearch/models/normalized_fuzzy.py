"""Normalized fuzzy triple — post-PIS/NIS-normalization output."""

from __future__ import annotations

from dataclasses import dataclass


@dataclass(frozen=True)
class NormalizedFuzzyM:
    """Normalized fuzzy triple (positive, average, negative) in [0, 1]^3.

    Computed by the TOPSIS pipeline (topsis.md §3.8).  Not present in
    scenario input.
    """

    positive: float
    average: float
    negative: float
