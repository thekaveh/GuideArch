using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows.Input;
using VMx.Commands;
using VMx.Components;
using VMx.Services;

namespace GuideArch.ViewModels;

/// <summary>
/// Root VM factory: builds the AppVM (a <see cref="ComponentVM{AppState}"/>) that
/// owns the theme observable + a child ScenarioVM. Every Avalonia binding rebases
/// from ScenarioVM.* to AppVM.Scenario.* — only app-shell concerns live on the
/// AppVM itself.
///
/// <para>
/// Theme persistence: round-trips through
/// <c>{LocalApplicationData}/GuideArch/settings.json</c>, atomically (write to
/// <c>.tmp</c> then <see cref="File.Move(string, string, bool)"/>). A missing,
/// unreadable, or unknown value silently falls back to <see cref="DefaultTheme"/>;
/// the next successful <c>SetTheme</c> rewrites the file.
/// </para>
///
/// <para>
/// <c>SetTheme</c> validates against <see cref="KnownThemes"/>. An unknown name
/// appends a warning and leaves state unchanged — it never throws. A theme
/// picker should be able to feed it arbitrary user input without crashing
/// the app.
/// </para>
/// </summary>
public static class AppVMFactory
{
    /// <summary>Default theme when no persisted value is available.</summary>
    public const string DefaultTheme = "dark";

    /// <summary>
    /// Mutable theme registry. "dark" and "light" are mandated cross-impl; impls
    /// can add more via <see cref="RegisterTheme"/> at startup (before the first
    /// AppVM is constructed) to surface framework-supported themes such as
    /// high-contrast variants.
    /// </summary>
    private static readonly ConcurrentDictionary<string, byte> _knownThemes = new(
        new[]
        {
            new KeyValuePair<string, byte>("dark", 0),
            new KeyValuePair<string, byte>("light", 0),
        },
        StringComparer.Ordinal);

    /// <summary>Read-only view of the registered theme names.</summary>
    public static IReadOnlyCollection<string> KnownThemes => (IReadOnlyCollection<string>)_knownThemes.Keys;

    /// <summary>Returns true if the given name is registered.</summary>
    public static bool IsKnownTheme(string name) => _knownThemes.ContainsKey(name);

    /// <summary>
    /// Register an additional theme name (idempotent). Use at startup, before the
    /// first AppVM is constructed, to expose framework-supported themes.
    /// </summary>
    public static void RegisterTheme(string name) => _knownThemes.TryAdd(name, 0);

    private const string DefaultMode = "native";

    // ------------------------------------------------------------------
    // Persistence — JSON: { "theme": "<name>" }. Anything else is treated
    // as malformed and falls back to DefaultTheme.
    // ------------------------------------------------------------------

    private static string SettingsFilePath
    {
        get
        {
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(local, "GuideArch", "settings.json");
        }
    }

    private static string LoadPersistedTheme()
    {
        try
        {
            var path = SettingsFilePath;
            if (!File.Exists(path)) return DefaultTheme;
            using var fs = File.OpenRead(path);
            using var doc = JsonDocument.Parse(fs);
            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty("theme", out var t) &&
                t.ValueKind == JsonValueKind.String)
            {
                var name = t.GetString();
                if (name is not null && _knownThemes.ContainsKey(name)) return name;
            }
        }
        catch
        {
            // I/O, parse, permission errors are all non-fatal — the in-memory
            // default wins and the next successful write rewrites the file.
        }
        return DefaultTheme;
    }

    private static void PersistTheme(string theme)
    {
        try
        {
            var path = SettingsFilePath;
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(new SettingsPayload(theme));
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, json);
            File.Move(tmp, path, overwrite: true);
        }
        catch
        {
            // See LoadPersistedTheme — persistence is best-effort.
        }
    }

    private sealed record SettingsPayload(string theme);

    // ------------------------------------------------------------------
    // Factory
    // ------------------------------------------------------------------

    /// <summary>
    /// Constructs the AppVM. Most callers (the View) pass nothing; tests inject
    /// stubs for the persistence callbacks and the child ScenarioVM.
    /// </summary>
    /// <param name="mode">
    /// Runtime mode. C# defaults to "native" (Avalonia desktop). Must be one of
    /// "web", "native", "tauri" — this is contract-only; nothing enforces it
    /// at runtime since impls are free to add more later.
    /// </param>
    /// <param name="loadTheme">
    /// Override for the persistence reader. When <c>null</c>, the default
    /// LocalApplicationData reader is used.
    /// </param>
    /// <param name="persistTheme">
    /// Override for the persistence writer. When <c>null</c>, the default
    /// LocalApplicationData writer is used.
    /// </param>
    /// <param name="scenario">
    /// Inject an existing ScenarioVM (testing). When <c>null</c>, a fresh one
    /// is constructed via <see cref="ScenarioVMFactory.Create"/>.
    /// </param>
    public static ComponentVM<AppState> Create(
        string mode = DefaultMode,
        Func<string>? loadTheme = null,
        Action<string>? persistTheme = null,
        ComponentVM<ScenarioState>? scenario = null)
    {
        var load = loadTheme ?? LoadPersistedTheme;
        var save = persistTheme ?? PersistTheme;
        var scenarioVm = scenario ?? ScenarioVMFactory.Create();

        var startTheme = load();
        if (!_knownThemes.ContainsKey(startTheme)) startTheme = DefaultTheme;

        var state = new AppState(startTheme, mode, ImmutableArray<string>.Empty);
        ComponentVM<AppState>? vm = null;

        void SetState(AppState next)
        {
            state = next;
            if (vm is not null) vm.Model = state;
        }

        void SetThemeImpl(string name)
        {
            if (!_knownThemes.ContainsKey(name))
            {
                SetState(state with { Warnings = state.Warnings.Add($"Unknown theme: {name}") });
                return;
            }
            if (string.Equals(name, state.Theme, StringComparison.Ordinal)) return;
            SetState(state with { Theme = name });
            save(name);
        }

        ICommand setThemeCmd = RelayCommand<string>.Builder()
            .Task(name =>
            {
                if (name is not null) SetThemeImpl(name);
            })
            .Build();

        vm = ComponentVM<AppState>.Builder()
            .Name("app-vm")
            .Model(state)
            .Services(NullMessageHub.Instance, NullDispatcher.Instance)
            .Build();

        var holder = new AppCommands(setThemeCmd, scenarioVm, SetThemeImpl);
        CommandsCache.Add(vm, holder);
        vm.Construct();
        return vm;
    }

    // ------------------------------------------------------------------
    // Commands lookup — mirrors ScenarioVMFactory.GetCommands
    // ------------------------------------------------------------------

    private static readonly ConditionalWeakTable<ComponentVM<AppState>, AppCommands> CommandsCache = new();

    /// <summary>
    /// Retrieves the commands attached to an AppVM created by <see cref="Create"/>.
    /// </summary>
    public static AppCommands GetCommands(ComponentVM<AppState> vm)
    {
        if (CommandsCache.TryGetValue(vm, out var c)) return c;
        throw new InvalidOperationException(
            "AppVM was not created by AppVMFactory; commands are unavailable.");
    }
}

/// <summary>
/// Commands + child references attached to an AppVM instance. Exposed via
/// <see cref="AppVMFactory.GetCommands"/>.
/// </summary>
public sealed class AppCommands
{
    /// <summary>Set the active theme by name; unknown values append a warning.</summary>
    public ICommand SetThemeCmd { get; }

    /// <summary>
    /// Child ScenarioVM. Every existing Avalonia binding that used to target
    /// ScenarioVM directly rebases through this property.
    /// </summary>
    public ComponentVM<ScenarioState> Scenario { get; }

    /// <summary>Direct callable bypassing ICommand plumbing — handy for tests and code-behind.</summary>
    public Action<string> SetTheme { get; }

    internal AppCommands(ICommand setThemeCmd, ComponentVM<ScenarioState> scenario, Action<string> setTheme)
    {
        SetThemeCmd = setThemeCmd;
        Scenario = scenario;
        SetTheme = setTheme;
    }
}
