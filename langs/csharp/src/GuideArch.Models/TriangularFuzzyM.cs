namespace GuideArch.Models;

/// <summary>
/// Triangular fuzzy number (lower, modal, upper) — topsis.md §4.1.
/// Arithmetic is componentwise. Constructor does NOT enforce lower ≤ modal ≤ upper.
/// </summary>
public sealed record TriangularFuzzyM(double Lower, double Modal, double Upper)
{
    /// <summary>The additive identity (0, 0, 0).</summary>
    public static readonly TriangularFuzzyM Zero = new(0.0, 0.0, 0.0);

    // topsis.md §4.1: a ⊕ b = (aL+bL, aM+bM, aU+bU)
    public static TriangularFuzzyM operator +(TriangularFuzzyM a, TriangularFuzzyM b)
        => new(a.Lower + b.Lower, a.Modal + b.Modal, a.Upper + b.Upper);

    // topsis.md §4.1: a ⊖ b = (aL-bL, aM-bM, aU-bU)
    public static TriangularFuzzyM operator -(TriangularFuzzyM a, TriangularFuzzyM b)
        => new(a.Lower - b.Lower, a.Modal - b.Modal, a.Upper - b.Upper);

    // topsis.md §4.1: unary negation
    public static TriangularFuzzyM operator -(TriangularFuzzyM a)
        => new(-a.Lower, -a.Modal, -a.Upper);

    // topsis.md §4.1: s ⊙ a = (s·aL, s·aM, s·aU)
    public static TriangularFuzzyM operator *(double scalar, TriangularFuzzyM a)
        => new(scalar * a.Lower, scalar * a.Modal, scalar * a.Upper);

    public static TriangularFuzzyM operator *(TriangularFuzzyM a, double scalar)
        => new(scalar * a.Lower, scalar * a.Modal, scalar * a.Upper);

    // topsis.md §4.1: a ⊘ s = (aL/s, aM/s, aU/s)
    public static TriangularFuzzyM operator /(TriangularFuzzyM a, double scalar)
        => new(a.Lower / scalar, a.Modal / scalar, a.Upper / scalar);
}
