using System.Collections.Immutable;
using GuideArch.Models;

namespace GuideArch.ViewModels;

/// <summary>
/// Immutable state record carried by the root ScenarioVM.
/// Replaced (not mutated) on every state transition.
/// </summary>
public sealed record ScenarioState(
    ScenarioM? Scenario,
    string? FilePath,
    bool IsDirty,
    ImmutableArray<CandidateM> Candidates,
    ImmutableArray<CriticalDecisionM> CriticalDecisions,
    ImmutableArray<CriticalConstraintM> CriticalConstraints,
    string Status,
    ImmutableArray<string> Warnings
)
{
    /// <summary>Returns an empty initial state with no scenario loaded.</summary>
    public static ScenarioState Empty { get; } = new(
        Scenario: null,
        FilePath: null,
        IsDirty: false,
        Candidates: ImmutableArray<CandidateM>.Empty,
        CriticalDecisions: ImmutableArray<CriticalDecisionM>.Empty,
        CriticalConstraints: ImmutableArray<CriticalConstraintM>.Empty,
        Status: "No scenario loaded.",
        Warnings: ImmutableArray<string>.Empty
    );
}
