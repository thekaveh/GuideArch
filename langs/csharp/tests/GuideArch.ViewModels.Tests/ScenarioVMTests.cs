using GuideArch.ViewModels;
using Xunit;

namespace GuideArch.ViewModels.Tests;

/// <summary>
/// Integration test: load sas.json via ScenarioVMFactory, assert candidates
/// match M1 expected output (spec/viewmodels.md §7).
/// </summary>
public class ScenarioVMTests
{
    private const double AbsTol = 1e-9;

    // Expected score from spec/conformance/expected/sas.candidates.json rank-0 entry.
    private const double ExpectedRank0Score = 0.031180695179944085;

    private static string FindSasJsonPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (int i = 0; i < 15; i++)
        {
            var candidate = Path.Combine(dir.FullName, "spec", "conformance", "scenarios", "sas.json");
            if (File.Exists(candidate)) return candidate;
            if (dir.Parent is null) break;
            dir = dir.Parent;
        }
        throw new InvalidOperationException("Cannot locate spec/conformance/scenarios/sas.json");
    }

    [Fact]
    public void OpenCmd_LoadsSasJson_PopulatesCandidates()
    {
        string sasPath = FindSasJsonPath();

        var vm = ScenarioVMFactory.Create();
        var cmds = ScenarioVMFactory.GetCommands(vm);

        // Initially no candidates.
        Assert.Empty(vm.Model.Candidates);
        Assert.Null(vm.Model.Scenario);

        // Execute OpenCmd with the sas.json path.
        cmds.OpenCmd.Execute(sasPath);

        // After open: candidates must be non-empty.
        Assert.True(vm.Model.Candidates.Length > 0,
            "Candidates should be populated after OpenCmd");

        // Rank-0 score must match M1 baseline within tolerance.
        var rank0 = vm.Model.Candidates[0];
        double actualScore = rank0.Score;

        Assert.True(
            Math.Abs(actualScore - ExpectedRank0Score) <= AbsTol,
            $"Candidates[0].Score = {actualScore:R} expected {ExpectedRank0Score:R} " +
            $"(diff={Math.Abs(actualScore - ExpectedRank0Score):E3})");

        // FilePath must be set.
        Assert.Equal(sasPath, vm.Model.FilePath);

        // IsDirty must be false after load.
        Assert.False(vm.Model.IsDirty);

        // Status should mention the scenario name.
        Assert.NotEmpty(vm.Model.Status);

        // Print score for integration check (captured by xunit output).
        Console.WriteLine($"[integration check] Candidates[0].Score = {actualScore:R}");
    }

    [Fact]
    public void NewCmd_ClearsState()
    {
        string sasPath = FindSasJsonPath();

        var vm = ScenarioVMFactory.Create();
        var cmds = ScenarioVMFactory.GetCommands(vm);

        cmds.OpenCmd.Execute(sasPath);
        Assert.True(vm.Model.Candidates.Length > 0);

        cmds.NewCmd.Execute(null);

        // After New: scenario replaced with fresh empty per spec
        // viewmodels.md §3.2; candidates empty, file path cleared.
        Assert.NotNull(vm.Model.Scenario);
        Assert.Empty(vm.Model.Scenario!.Decisions);
        Assert.Empty(vm.Model.Candidates);
        Assert.Null(vm.Model.FilePath);
    }

    [Fact]
    public void SaveCmd_IsDisabledWithNoFilePath()
    {
        var vm = ScenarioVMFactory.Create();
        var cmds = ScenarioVMFactory.GetCommands(vm);

        // No file loaded — SaveCmd should report CanExecute = false.
        Assert.False(cmds.SaveCmd.CanExecute(null));
    }

    [Fact]
    public void SolveCmd_IsDisabledWithNoScenario()
    {
        var vm = ScenarioVMFactory.Create();
        var cmds = ScenarioVMFactory.GetCommands(vm);

        Assert.False(cmds.SolveCmd.CanExecute(null));
    }

    [Fact]
    public void SolveCmd_IsEnabledAfterOpen()
    {
        string sasPath = FindSasJsonPath();
        var vm = ScenarioVMFactory.Create();
        var cmds = ScenarioVMFactory.GetCommands(vm);

        cmds.OpenCmd.Execute(sasPath);
        Assert.True(cmds.SolveCmd.CanExecute(null));
    }
}
