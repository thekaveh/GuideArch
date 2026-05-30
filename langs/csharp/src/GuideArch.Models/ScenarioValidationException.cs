namespace GuideArch.Models;

/// <summary>Raised for fatal invariant violations when loading a scenario.</summary>
public sealed class ScenarioValidationException : Exception
{
    public ScenarioValidationException(string message) : base(message) { }
}
