"""Unit tests for TriangularFuzzyM arithmetic — topsis.md §4.1."""

import pytest

from guidearch.models.triangular_fuzzy import TriangularFuzzyM


def tfm(lower: float, modal: float, upper: float) -> TriangularFuzzyM:
    return TriangularFuzzyM(lower, modal, upper)


class TestAddition:
    def test_basic(self) -> None:
        a = tfm(1.0, 2.0, 3.0)
        b = tfm(4.0, 5.0, 6.0)
        assert a + b == tfm(5.0, 7.0, 9.0)

    def test_with_zero(self) -> None:
        a = tfm(1.0, 2.0, 3.0)
        assert a + TriangularFuzzyM.zero() == a

    def test_commutativity(self) -> None:
        a = tfm(1.0, 2.0, 3.0)
        b = tfm(0.5, 1.5, 2.5)
        assert a + b == b + a


class TestSubtraction:
    def test_basic(self) -> None:
        a = tfm(5.0, 7.0, 9.0)
        b = tfm(1.0, 2.0, 3.0)
        assert a - b == tfm(4.0, 5.0, 6.0)

    def test_self_minus_self(self) -> None:
        a = tfm(1.0, 2.0, 3.0)
        result = a - a
        assert result == TriangularFuzzyM.zero()


class TestScalarMultiplication:
    def test_positive_scalar(self) -> None:
        a = tfm(1.0, 2.0, 3.0)
        assert a * 2.0 == tfm(2.0, 4.0, 6.0)

    def test_rmul(self) -> None:
        a = tfm(1.0, 2.0, 3.0)
        assert 2.0 * a == tfm(2.0, 4.0, 6.0)

    def test_negative_scalar_flips_sign(self) -> None:
        a = tfm(1.0, 2.0, 3.0)
        result = a * -1.0
        assert result == tfm(-1.0, -2.0, -3.0)

    def test_zero_scalar(self) -> None:
        a = tfm(1.0, 2.0, 3.0)
        assert a * 0.0 == TriangularFuzzyM.zero()


class TestNegation:
    def test_negate(self) -> None:
        a = tfm(1.0, 2.0, 3.0)
        assert -a == tfm(-1.0, -2.0, -3.0)

    def test_double_negate(self) -> None:
        a = tfm(1.0, 2.0, 3.0)
        neg_a = -a
        assert -neg_a == a


class TestDivision:
    def test_basic(self) -> None:
        a = tfm(2.0, 4.0, 6.0)
        assert a / 2.0 == tfm(1.0, 2.0, 3.0)

    def test_divide_by_zero_raises(self) -> None:
        a = tfm(1.0, 2.0, 3.0)
        with pytest.raises(ZeroDivisionError):
            _ = a / 0.0

    def test_precision(self) -> None:
        a = tfm(1.0, 2.0, 3.0)
        result = a / 3.0
        assert abs(result.lower - 1.0 / 3.0) < 1e-15
        assert abs(result.modal - 2.0 / 3.0) < 1e-15
        assert abs(result.upper - 1.0) < 1e-15


class TestZero:
    def test_zero_is_additive_identity(self) -> None:
        z = TriangularFuzzyM.zero()
        assert z == TriangularFuzzyM(0.0, 0.0, 0.0)

    def test_zero_plus_zero(self) -> None:
        assert TriangularFuzzyM.zero() + TriangularFuzzyM.zero() == TriangularFuzzyM.zero()


class TestCompositeOps:
    """Verify the formula: sign * weight * coeff / M  from topsis.md §3.5."""

    def test_weighted_contribution(self) -> None:
        coeff = tfm(10.0, 20.0, 30.0)
        weight = 2.0
        sign = 1.0  # min property
        M = 100.0
        result = coeff * (sign * weight) / M
        expected = tfm(0.2, 0.4, 0.6)
        assert abs(result.lower - expected.lower) < 1e-15
        assert abs(result.modal - expected.modal) < 1e-15
        assert abs(result.upper - expected.upper) < 1e-15
