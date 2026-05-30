namespace GuideArch.Models;

/// <summary>
/// How the three φ components are aggregated into a final score.
/// Default: Max (topsis.md §8).
/// </summary>
public enum Aggregation { Sum, Max }

/// <summary>Scenario configuration for the TOPSIS pipeline.</summary>
public sealed record ConfigM(Aggregation Aggregation, NormalizedFuzzyM Weights);
