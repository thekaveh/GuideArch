using System.Collections.Immutable;

namespace GuideArch.Models;

/// <summary>
/// A complete architecture (one alternative per decision) with its TOPSIS ranking.
/// Lower score = better (topsis.md §3.10).
/// </summary>
public sealed record CandidateM(
    ImmutableArray<string> AlternativeIds,
    TriangularFuzzyM TriangularValue,
    NormalizedFuzzyM NormalizedValue,
    double Score,
    int Rank
)
{
    /// <summary>
    /// Comma-separated list of alternative IDs — used for display in the Results DataGrid.
    /// </summary>
    public string AlternativeIdsDisplay => string.Join(", ", AlternativeIds);
}
