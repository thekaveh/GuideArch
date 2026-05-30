using System.Collections.Immutable;

namespace GuideArch.Models;

/// <summary>
/// Complete decision-analysis scenario for fuzzy TOPSIS ranking.
/// Constructed exclusively via <see cref="ScenarioLoader"/>.
/// </summary>
public sealed record ScenarioM(
    string SchemaVersion,
    string Name,
    string Description,
    ImmutableArray<DecisionM> Decisions,
    ImmutableArray<AlternativeM> Alternatives,
    ImmutableArray<PropertyM> Properties,
    ImmutableArray<CoefficientM> Coefficients,
    ImmutableArray<ConstraintM> Constraints,
    ConfigM Config,
    ImmutableArray<string> Warnings
);
