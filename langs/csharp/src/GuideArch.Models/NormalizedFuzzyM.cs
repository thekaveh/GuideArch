namespace GuideArch.Models;

/// <summary>
/// Post-PIS/NIS-normalization triple (positive, average, negative) in [0,1]³.
/// Also used for config weights (positive/average/negative summing to 1).
/// </summary>
public sealed record NormalizedFuzzyM(double Positive, double Average, double Negative);
