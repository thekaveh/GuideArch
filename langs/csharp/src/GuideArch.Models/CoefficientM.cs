namespace GuideArch.Models;

/// <summary>Maps (alternativeId, propertyId) → TriangularFuzzy score.</summary>
public sealed record CoefficientM(string AlternativeId, string PropertyId, TriangularFuzzyM Value);
