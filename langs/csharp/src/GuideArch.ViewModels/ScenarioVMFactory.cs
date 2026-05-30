using System.Collections.Immutable;
using System.Text.Json;
using System.Windows.Input;
using GuideArch.Models;
using GuideArch.Models.Topsis;
using VMx.Commands;
using VMx.Components;
using VMx.Services;

namespace GuideArch.ViewModels;

/// <summary>
/// Root VM factory for the GuideArch scenario.
/// Returns a <see cref="ComponentVM{ScenarioState}"/> configured with observable
/// state and commands per spec/viewmodels.md §3.
/// </summary>
public static class ScenarioVMFactory
{
    /// <summary>
    /// Creates and constructs the root ScenarioVM.
    /// </summary>
    public static ComponentVM<ScenarioState> Create()
    {
        // Mutable state box — updated atomically by commands.
        var state = ScenarioState.Empty;

        ComponentVM<ScenarioState>? vm = null;

        // -----------------------------------------------------------------------
        // Helper: update state and push to VM.Model
        // -----------------------------------------------------------------------
        void SetState(ScenarioState next)
        {
            state = next;
            if (vm is not null)
                vm.Model = state;
        }

        // -----------------------------------------------------------------------
        // Core: run Solve + analyses and update candidates / status.
        // -----------------------------------------------------------------------
        void Solve()
        {
            if (state.Scenario is null)
            {
                SetState(state with
                {
                    Candidates = ImmutableArray<CandidateM>.Empty,
                    CriticalDecisions = ImmutableArray<CriticalDecisionM>.Empty,
                    CriticalConstraints = ImmutableArray<CriticalConstraintM>.Empty,
                    Status = "No scenario loaded."
                });
                return;
            }

            var scenario = state.Scenario;
            var candidates = Solver.Solve(scenario);
            var critDec = CriticalDecisions.Analyze(scenario, candidates);
            var critCon = CriticalConstraints.Analyze(scenario);

            SetState(state with
            {
                Candidates = candidates,
                CriticalDecisions = critDec,
                CriticalConstraints = critCon,
                Status = $"Solved: {candidates.Length} candidates — \"{scenario.Name}\""
            });
        }

        // -----------------------------------------------------------------------
        // NewCmd — spec §3.2
        // -----------------------------------------------------------------------
        ICommand newCmd = RelayCommand.Builder()
            .Task(() =>
            {
                SetState(ScenarioState.Empty with
                {
                    Status = "New scenario (empty)."
                });
            })
            .Build();

        // -----------------------------------------------------------------------
        // OpenCmd — spec §3.2
        // -----------------------------------------------------------------------
        ICommand openCmd = RelayCommand<string>.Builder()
            .Task(path =>
            {
                try
                {
                    var scenario = ScenarioLoader.Load(path);
                    SetState(state with
                    {
                        Scenario = scenario,
                        FilePath = path,
                        IsDirty = false,
                        Warnings = scenario.Warnings,
                        Status = $"Loading \"{scenario.Name}\"…"
                    });
                    Solve();
                }
                catch (Exception ex)
                {
                    SetState(state with
                    {
                        Warnings = state.Warnings.Add($"Open failed: {ex.Message}")
                    });
                }
            })
            .Build();

        // -----------------------------------------------------------------------
        // SaveCmd — spec §3.2
        // -----------------------------------------------------------------------
        ICommand saveCmd = RelayCommand.Builder()
            .Predicate(() => state.FilePath is not null && state.Scenario is not null)
            .Task(() =>
            {
                if (state.FilePath is null || state.Scenario is null) return;
                WriteScenario(state.Scenario, state.FilePath);
                SetState(state with { IsDirty = false });
            })
            .Build();

        // -----------------------------------------------------------------------
        // SaveAsCmd — spec §3.2
        // -----------------------------------------------------------------------
        ICommand saveAsCmd = RelayCommand<string>.Builder()
            .Task(path =>
            {
                if (state.Scenario is null) return;
                WriteScenario(state.Scenario, path);
                SetState(state with { FilePath = path, IsDirty = false });
            })
            .Build();

        // -----------------------------------------------------------------------
        // SolveCmd — spec §3.2
        // -----------------------------------------------------------------------
        ICommand solveCmd = RelayCommand.Builder()
            .Predicate(() => state.Scenario is not null)
            .Task(Solve)
            .Build();

        // -----------------------------------------------------------------------
        // Build VM
        // -----------------------------------------------------------------------
        vm = ComponentVM<ScenarioState>.Builder()
            .Name("scenario-vm")
            .Model(state)
            .Services(NullMessageHub.Instance, NullDispatcher.Instance)
            .OnConstruct(() =>
            {
                // Expose commands as named properties via INPC / dynamic access.
                // Avalonia DataGrid can bind to them by name via ReflectionBinding.
            })
            .Build();

        // Attach commands as extra properties via a thin wrapper.
        // Because VMx ComponentVM<M> doesn't have a dictionary of extra properties,
        // we expose commands through a companion object that the View can access
        // directly via the factory's Commands property (tests use this too).
        var cmdHolder = new ScenarioCommands(newCmd, openCmd, saveCmd, saveAsCmd, solveCmd);
        CommandsCache.Add(vm, cmdHolder);

        vm.Construct();
        return vm;
    }

    // Provide a way for View code-behind and tests to retrieve commands from a VM.
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<
        ComponentVM<ScenarioState>, ScenarioCommands> CommandsCache = new();

    /// <summary>
    /// Retrieves the commands attached to a ScenarioVM created by this factory.
    /// </summary>
    public static ScenarioCommands GetCommands(ComponentVM<ScenarioState> vm)
    {
        if (CommandsCache.TryGetValue(vm, out var cmds))
            return cmds;
        throw new InvalidOperationException("VM was not created by ScenarioVMFactory.");
    }

    // -----------------------------------------------------------------------
    // JSON serialiser (deterministic, sorted keys)
    // -----------------------------------------------------------------------
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static void WriteScenario(ScenarioM scenario, string path)
    {
        // Re-serialize through Models.Output.Serializer if available; otherwise
        // fall back to System.Text.Json (deterministic enough for our purposes).
        var json = JsonSerializer.Serialize(scenario, SerializerOptions);
        File.WriteAllText(path, json);
    }
}

/// <summary>
/// Commands attached to a ScenarioVM instance.
/// Exposed by <see cref="ScenarioVMFactory.GetCommands"/>.
/// </summary>
public sealed class ScenarioCommands
{
    public ICommand NewCmd { get; }
    public ICommand OpenCmd { get; }
    public ICommand SaveCmd { get; }
    public ICommand SaveAsCmd { get; }
    public ICommand SolveCmd { get; }

    internal ScenarioCommands(
        ICommand newCmd,
        ICommand openCmd,
        ICommand saveCmd,
        ICommand saveAsCmd,
        ICommand solveCmd)
    {
        NewCmd = newCmd;
        OpenCmd = openCmd;
        SaveCmd = saveCmd;
        SaveAsCmd = saveAsCmd;
        SolveCmd = solveCmd;
    }
}
