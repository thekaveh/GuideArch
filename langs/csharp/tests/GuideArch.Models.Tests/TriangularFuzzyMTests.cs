using GuideArch.Models;
using Xunit;

namespace GuideArch.Models.Tests;

public class TriangularFuzzyMTests
{
    private static void AssertClose(double expected, double actual, double tol = 1e-12)
        => Assert.True(Math.Abs(expected - actual) <= tol,
            $"Expected {expected:R} but got {actual:R} (diff={Math.Abs(expected - actual):E3})");

    [Fact]
    public void Zero_IsAdditiveIdentity()
    {
        var a = new TriangularFuzzyM(1.0, 2.0, 3.0);
        var r = a + TriangularFuzzyM.Zero;
        Assert.Equal(a, r);
    }

    [Fact]
    public void Addition_IsComponentwise()
    {
        var a = new TriangularFuzzyM(1.0, 2.0, 3.0);
        var b = new TriangularFuzzyM(4.0, 5.0, 6.0);
        var r = a + b;
        Assert.Equal(new TriangularFuzzyM(5.0, 7.0, 9.0), r);
    }

    [Fact]
    public void Subtraction_IsComponentwise()
    {
        var a = new TriangularFuzzyM(5.0, 7.0, 9.0);
        var b = new TriangularFuzzyM(1.0, 2.0, 3.0);
        var r = a - b;
        Assert.Equal(new TriangularFuzzyM(4.0, 5.0, 6.0), r);
    }

    [Fact]
    public void ScalarMultiplication_IsComponentwise()
    {
        var a = new TriangularFuzzyM(1.0, 2.0, 3.0);
        var r = 2.0 * a;
        Assert.Equal(new TriangularFuzzyM(2.0, 4.0, 6.0), r);
    }

    [Fact]
    public void ScalarMultiplication_RightSide()
    {
        var a = new TriangularFuzzyM(1.0, 2.0, 3.0);
        var r = a * 3.0;
        Assert.Equal(new TriangularFuzzyM(3.0, 6.0, 9.0), r);
    }

    [Fact]
    public void NegativeScalar_FlipsSign()
    {
        var a = new TriangularFuzzyM(1.0, 2.0, 3.0);
        var r = -1.0 * a;
        Assert.Equal(new TriangularFuzzyM(-1.0, -2.0, -3.0), r);
    }

    [Fact]
    public void UnaryNegation_FlipsAllComponents()
    {
        var a = new TriangularFuzzyM(1.0, 2.0, 3.0);
        var r = -a;
        Assert.Equal(new TriangularFuzzyM(-1.0, -2.0, -3.0), r);
    }

    [Fact]
    public void ScalarDivision_IsComponentwise()
    {
        var a = new TriangularFuzzyM(2.0, 4.0, 6.0);
        var r = a / 2.0;
        Assert.Equal(new TriangularFuzzyM(1.0, 2.0, 3.0), r);
    }

    [Fact]
    public void ZeroStatic_IsAllZeros()
    {
        Assert.Equal(new TriangularFuzzyM(0.0, 0.0, 0.0), TriangularFuzzyM.Zero);
    }

    [Fact]
    public void Multiplication_ThenDivision_RoundTrips()
    {
        var a = new TriangularFuzzyM(1.5, 2.5, 3.5);
        var r = (a * 5.0) / 5.0;
        AssertClose(a.Lower, r.Lower);
        AssertClose(a.Modal, r.Modal);
        AssertClose(a.Upper, r.Upper);
    }

    [Fact]
    public void DivisionByZero_Throws()
    {
        // topsis.md §4.1 defines a ⊘ s only for s ≠ 0. TS throws and Python
        // raises ZeroDivisionError; silent ±Infinity here was the C# outlier.
        var a = new TriangularFuzzyM(2.0, 4.0, 6.0);
        Assert.Throws<DivideByZeroException>(() => a / 0.0);
    }
}
