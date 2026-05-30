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
    ImmutableArray<string> Warnings,
    int? SelectedCandidateIndex = null
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
        Warnings: ImmutableArray<string>.Empty,
        SelectedCandidateIndex: null
    );

    // ------------------------------------------------------------------
    // Derived view-ready projections of the scenario (M3 editors).
    // Computed lazily from Scenario for use by XAML bindings.
    // ReflectionBinding is used in AXAML since DataContext is
    // ComponentVM<ScenarioState> — an open generic type that Avalonia's
    // compiled binding engine cannot resolve without a type literal.
    // ------------------------------------------------------------------

    /// <summary>Decisions in the loaded scenario, or empty.</summary>
    public ImmutableArray<DecisionM> DecisionsView =>
        Scenario?.Decisions ?? ImmutableArray<DecisionM>.Empty;

    /// <summary>All alternatives, or empty.</summary>
    public ImmutableArray<AlternativeM> AlternativesView =>
        Scenario?.Alternatives ?? ImmutableArray<AlternativeM>.Empty;

    /// <summary>Properties in the loaded scenario, or empty.</summary>
    public ImmutableArray<PropertyM> PropertiesView =>
        Scenario?.Properties ?? ImmutableArray<PropertyM>.Empty;

    /// <summary>Coefficients in the loaded scenario, or empty.</summary>
    public ImmutableArray<CoefficientM> CoefficientsView =>
        Scenario?.Coefficients ?? ImmutableArray<CoefficientM>.Empty;

    /// <summary>Constraints in the loaded scenario, or empty.</summary>
    public ImmutableArray<ConstraintM> ConstraintsView =>
        Scenario?.Constraints ?? ImmutableArray<ConstraintM>.Empty;

    /// <summary>Threshold constraints only.</summary>
    public ImmutableArray<ThresholdConstraintM> ThresholdConstraintsView =>
        ConstraintsView.OfType<ThresholdConstraintM>().ToImmutableArray();

    /// <summary>Dependency constraints only.</summary>
    public ImmutableArray<DependencyConstraintM> DependencyConstraintsView =>
        ConstraintsView.OfType<DependencyConstraintM>().ToImmutableArray();

    /// <summary>Conflict constraints only.</summary>
    public ImmutableArray<ConflictConstraintM> ConflictConstraintsView =>
        ConstraintsView.OfType<ConflictConstraintM>().ToImmutableArray();

    /// <summary>True when a scenario is loaded.</summary>
    public bool HasScenario => Scenario is not null;

    // ------------------------------------------------------------------
    // M4 chart-ready projections (spec charts.md §4, §5)
    // ------------------------------------------------------------------

    /// <summary>
    /// Critical decisions display rows, sorted ascending by rank.
    /// Used by the Critical decisions tab DataGrid.
    /// </summary>
    public ImmutableArray<ChartData.CriticalDecisionRow> CriticalDecisionsView =>
        Scenario is null
            ? ImmutableArray<ChartData.CriticalDecisionRow>.Empty
            : ChartData.PrepCriticalDecisions(CriticalDecisions, Scenario);

    /// <summary>
    /// Critical constraints display rows, sorted descending by eliminated.
    /// Used by the Critical constraints tab DataGrid.
    /// </summary>
    public ImmutableArray<ChartData.CriticalConstraintRow> CriticalConstraintsView =>
        ChartData.PrepCriticalConstraints(CriticalConstraints);
}
