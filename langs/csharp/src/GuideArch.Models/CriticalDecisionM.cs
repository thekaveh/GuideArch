namespace GuideArch.Models;

/// <summary>
/// How strongly a decision drives the top candidates' scores.
/// Lower score = more critical (topsis.md §5).
/// </summary>
public sealed record CriticalDecisionM(
    string DecisionId,
    TriangularFuzzyM TriangularValue,
    NormalizedFuzzyM NormalizedValue,
    double Score,
    int Rank
);
