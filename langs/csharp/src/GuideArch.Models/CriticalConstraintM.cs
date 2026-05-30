namespace GuideArch.Models;

/// <summary>
/// How many candidates a constraint eliminates from the unconstrained Cartesian product.
/// Most-binding first (eliminated descending) — topsis.md §6.
/// </summary>
public sealed record CriticalConstraintM(
    int ConstraintIndex,
    string Kind,
    int Eliminated,
    int Total,
    bool Redundant
);
