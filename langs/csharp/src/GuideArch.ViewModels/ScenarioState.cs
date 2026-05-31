using System.Collections.Immutable;
using GuideArch.Models;

namespace GuideArch.ViewModels;

// ---------------------------------------------------------------------------
// Coefficients 2-D matrix view model types (spec editors.md §2.4)
// ---------------------------------------------------------------------------

/// <summary>One fuzzy cell in the coefficients matrix.</summary>
public sealed record CoeffCellVM(
    string AlternativeId,
    string PropertyId,
    double Lower,
    double Modal,
    double Upper)
{
    /// <summary>Display text: L / M / U formatted to 4 dp.</summary>
    public string Display => $"{Lower:F4} / {Modal:F4} / {Upper:F4}";
}

/// <summary>One alternative row in the coefficients matrix.</summary>
public sealed record CoeffRowVM(
    string AlternativeId,
    string AlternativeName,
    ImmutableArray<CoeffCellVM> Cells);   // one per property, in property order

/// <summary>One decision group in the coefficients matrix.</summary>
public sealed record CoeffGroupVM(
    string DecisionId,
    string DecisionName,
    ImmutableArray<CoeffRowVM> Rows);

/// <summary>Full 2-D matrix ready for the Coefficients tab.</summary>
public sealed record CoeffMatrixVM(
    ImmutableArray<PropertyM> Properties,         // column headers
    ImmutableArray<CoeffGroupVM> Groups);          // row groups

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

    /// <summary>
    /// 2-D fuzzy coefficient matrix grouped by decision (spec editors.md §2.4).
    /// Rows = alternatives (grouped by decision); columns = properties.
    /// Empty when no scenario is loaded.
    /// </summary>
    public CoeffMatrixVM CoefficientsMatrix
    {
        get
        {
            if (Scenario is null)
                return new CoeffMatrixVM(
                    ImmutableArray<PropertyM>.Empty,
                    ImmutableArray<CoeffGroupVM>.Empty);

            var s = Scenario;

            // Build a lookup: (alternativeId, propertyId) → TriangularFuzzyM
            var cellLookup = new Dictionary<(string altId, string propId), TriangularFuzzyM>(
                s.Coefficients.Length);
            foreach (var c in s.Coefficients)
                cellLookup[(c.AlternativeId, c.PropertyId)] = c.Value;

            // Build groups: one per decision, preserving decision order.
            var groups = ImmutableArray.CreateBuilder<CoeffGroupVM>(s.Decisions.Length);
            foreach (var decision in s.Decisions)
            {
                var alts = s.Alternatives.Where(a => a.DecisionId == decision.Id).ToList();
                var rows = ImmutableArray.CreateBuilder<CoeffRowVM>(alts.Count);
                foreach (var alt in alts)
                {
                    var cells = ImmutableArray.CreateBuilder<CoeffCellVM>(s.Properties.Length);
                    foreach (var prop in s.Properties)
                    {
                        var val = cellLookup.TryGetValue((alt.Id, prop.Id), out var v)
                            ? v : TriangularFuzzyM.Zero;
                        cells.Add(new CoeffCellVM(alt.Id, prop.Id,
                            val.Lower, val.Modal, val.Upper));
                    }
                    rows.Add(new CoeffRowVM(alt.Id, alt.Name, cells.MoveToImmutable()));
                }
                groups.Add(new CoeffGroupVM(decision.Id, decision.Name, rows.MoveToImmutable()));
            }

            return new CoeffMatrixVM(s.Properties, groups.MoveToImmutable());
        }
    }

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
