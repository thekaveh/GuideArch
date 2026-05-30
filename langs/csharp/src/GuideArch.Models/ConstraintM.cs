namespace GuideArch.Models;

/// <summary>Discriminated union base for the three constraint kinds.</summary>
public abstract record ConstraintM
{
    public abstract string Kind { get; }
}

/// <summary>
/// A property's aggregate contribution for a candidate must lie in [Min, Max].
/// kind == "threshold"
/// </summary>
public sealed record ThresholdConstraintM(
    string PropertyId,
    double? Min,
    double? Max
) : ConstraintM
{
    public override string Kind => "threshold";
}

/// <summary>
/// Biconditional: source and target must both be present or both absent.
/// kind == "dependency"
/// </summary>
public sealed record DependencyConstraintM(
    string SourceAlternativeId,
    string TargetAlternativeId
) : ConstraintM
{
    public override string Kind => "dependency";
}

/// <summary>
/// Exclusion: A and B cannot both appear in the same candidate.
/// kind == "conflict"
/// </summary>
public sealed record ConflictConstraintM(
    string AlternativeAId,
    string AlternativeBId
) : ConstraintM
{
    public override string Kind => "conflict";
}
