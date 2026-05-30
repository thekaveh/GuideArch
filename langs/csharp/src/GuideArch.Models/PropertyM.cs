namespace GuideArch.Models;

/// <summary>Whether lower or higher values are preferred for this property.</summary>
public enum PropertyKind { Min, Max }

/// <summary>A quality attribute used to score candidates.</summary>
public sealed record PropertyM(string Id, string Name, PropertyKind Kind, double Weight);
