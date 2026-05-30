"""Triangular fuzzy number — topsis.md §4.1."""

from __future__ import annotations

from dataclasses import dataclass


@dataclass(frozen=True)
class TriangularFuzzyM:
    """Triangular fuzzy number (lower, modal, upper).

    Arithmetic follows topsis.md §4.1 (componentwise).
    The constructor does NOT enforce lower ≤ modal ≤ upper — legacy semantics.
    """

    lower: float
    modal: float
    upper: float

    # ------------------------------------------------------------------
    # Arithmetic — topsis.md §4.1
    # ------------------------------------------------------------------

    def __add__(self, other: TriangularFuzzyM) -> TriangularFuzzyM:
        return TriangularFuzzyM(
            self.lower + other.lower,
            self.modal + other.modal,
            self.upper + other.upper,
        )

    def __sub__(self, other: TriangularFuzzyM) -> TriangularFuzzyM:
        return TriangularFuzzyM(
            self.lower - other.lower,
            self.modal - other.modal,
            self.upper - other.upper,
        )

    def __mul__(self, scalar: float) -> TriangularFuzzyM:
        return TriangularFuzzyM(
            scalar * self.lower,
            scalar * self.modal,
            scalar * self.upper,
        )

    def __rmul__(self, scalar: float) -> TriangularFuzzyM:
        return self.__mul__(scalar)

    def __neg__(self) -> TriangularFuzzyM:
        return TriangularFuzzyM(-self.lower, -self.modal, -self.upper)

    def __truediv__(self, scalar: float) -> TriangularFuzzyM:
        return TriangularFuzzyM(
            self.lower / scalar,
            self.modal / scalar,
            self.upper / scalar,
        )

    @classmethod
    def zero(cls) -> TriangularFuzzyM:
        """Return the additive identity (0, 0, 0)."""
        return cls(0.0, 0.0, 0.0)
