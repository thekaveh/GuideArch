using System.Collections.Immutable;

namespace GuideArch.ViewModels;

/// <summary>
/// Immutable state carried by the root AppVM (a <see cref="VMx.Components.ComponentVM{AppState}"/>).
/// Holds app-shell concerns: active theme, runtime mode, and a warnings log.
/// Replaced (not mutated) on every state transition — same pattern as
/// <see cref="ScenarioState"/>.
/// </summary>
/// <param name="Theme">
/// Current theme name. Must be a member of <see cref="AppVMFactory.KnownThemes"/>.
/// "dark" and "light" are mandated cross-impl. Impls may extend by calling
/// <see cref="AppVMFactory.RegisterTheme"/> at startup.
/// </param>
/// <param name="Mode">
/// Runtime mode — one of "web", "native", "tauri". Set at construction and
/// immutable thereafter: there is no command on the public AppVM surface to
/// change it. The C# impl defaults to "native" (Avalonia desktop).
/// </param>
/// <param name="Warnings">
/// Append-only log of soft failures (e.g. setTheme called with an unknown
/// name). Persistence errors are NOT recorded here — they're silently
/// swallowed since the in-memory state is always authoritative.
/// </param>
public sealed record AppState(
    string Theme,
    string Mode,
    ImmutableArray<string> Warnings)
{
    /// <summary>
    /// Default empty state at construction. Mode defaults to "native";
    /// callers can supply a different mode through the factory.
    /// </summary>
    public static AppState Default(string mode) => new(
        Theme: AppVMFactory.DefaultTheme,
        Mode: mode,
        Warnings: ImmutableArray<string>.Empty);
}
