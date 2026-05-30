namespace GuideArch.ViewModels;

/// <summary>
/// Thrown by ScenarioVMFactory mutation methods when a mutation would violate a
/// domain invariant (spec/domain/invariants.md). Fatal violations block the
/// mutation; the View catches this and shows a dialog to the user.
/// </summary>
public sealed class ScenarioMutationException : Exception
{
    public ScenarioMutationException(string message) : base(message) { }
    public ScenarioMutationException(string message, Exception inner) : base(message, inner) { }
}
