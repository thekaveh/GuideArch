using GuideArch.Models;
using GuideArch.ViewModels;
using VMx.Components;
using Xunit;

namespace GuideArch.ViewModels.Tests;

/// <summary>
/// M3 save round-trip tests (spec/editors.md §6).
/// Load sas.json → mutate → save → reload → assert persistence.
/// </summary>
public class SaveRoundtripTests
{
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

    private static (ComponentVM<ScenarioState> vm, ScenarioCommands cmds) LoadSas()
    {
        var vm = ScenarioVMFactory.Create();
        var cmds = ScenarioVMFactory.GetCommands(vm);
        cmds.OpenCmd.Execute(FindSasJsonPath());
        Assert.NotNull(vm.Model.Scenario);
        return (vm, cmds);
    }

    // -----------------------------------------------------------------------
    // Round-trip: edit property weight → save → reload → assert weight persists
    // -----------------------------------------------------------------------

    [Fact]
    public void EditPropertyWeight_SaveAndReload_PersistsChange()
    {
        var (vm, cmds) = LoadSas();
        var scenario = vm.Model.Scenario!;
        var prop = scenario.Properties[0];
        var newWeight = prop.Weight * 2.0 + 0.1; // guaranteed different

        // Mutate the weight.
        cmds.Mutator.UpdateProperty(prop.Id, null, null, newWeight);
        Assert.Equal(newWeight, vm.Model.Scenario!.Properties[0].Weight, 12);

        // Candidates must have been recalculated after weight change.
        Assert.True(vm.Model.Candidates.Length > 0,
            "Solve should have run after property weight change.");

        // Save to temp file.
        var tempPath = Path.GetTempFileName() + ".json";
        try
        {
            cmds.SaveAsCmd.Execute(tempPath);
            Assert.False(vm.Model.IsDirty);

            // Reload into a fresh VM.
            var vm2 = ScenarioVMFactory.Create();
            var cmds2 = ScenarioVMFactory.GetCommands(vm2);
            cmds2.OpenCmd.Execute(tempPath);

            Assert.NotNull(vm2.Model.Scenario);
            var reloadedProp = vm2.Model.Scenario!.Properties
                .FirstOrDefault(p => p.Id == prop.Id);
            Assert.NotNull(reloadedProp);
            Assert.Equal(newWeight, reloadedProp!.Weight, 12);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    // -----------------------------------------------------------------------
    // Round-trip: pure load → save → reload is structurally identical
    // -----------------------------------------------------------------------

    [Fact]
    public void SaveWithoutMutation_ReloadIsStructurallyIdentical()
    {
        var (vm, cmds) = LoadSas();
        var scenario = vm.Model.Scenario!;

        var tempPath = Path.GetTempFileName() + ".json";
        try
        {
            cmds.SaveAsCmd.Execute(tempPath);

            var vm2 = ScenarioVMFactory.Create();
            var cmds2 = ScenarioVMFactory.GetCommands(vm2);
            cmds2.OpenCmd.Execute(tempPath);

            var s2 = vm2.Model.Scenario!;
            Assert.Equal(scenario.Decisions.Length, s2.Decisions.Length);
            Assert.Equal(scenario.Alternatives.Length, s2.Alternatives.Length);
            Assert.Equal(scenario.Properties.Length, s2.Properties.Length);
            Assert.Equal(scenario.Coefficients.Length, s2.Coefficients.Length);
            Assert.Equal(scenario.Constraints.Length, s2.Constraints.Length);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    // -----------------------------------------------------------------------
    // Round-trip: delete decision → save → reload → decision gone
    // -----------------------------------------------------------------------

    [Fact]
    public void DeleteDecision_SaveAndReload_DecisionAbsent()
    {
        var (vm, cmds) = LoadSas();
        var scenario = vm.Model.Scenario!;
        var decision = scenario.Decisions[0];

        cmds.Mutator.DeleteDecision(decision.Id);
        Assert.DoesNotContain(vm.Model.Scenario!.Decisions, d => d.Id == decision.Id);

        var tempPath = Path.GetTempFileName() + ".json";
        try
        {
            cmds.SaveAsCmd.Execute(tempPath);

            var vm2 = ScenarioVMFactory.Create();
            var cmds2 = ScenarioVMFactory.GetCommands(vm2);
            cmds2.OpenCmd.Execute(tempPath);

            Assert.DoesNotContain(vm2.Model.Scenario!.Decisions, d => d.Id == decision.Id);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    // -----------------------------------------------------------------------
    // Round-trip: mutating property weight actually changes solve output
    // -----------------------------------------------------------------------

    [Fact]
    public void EditPropertyWeight_ChangesCandidateRanking()
    {
        var (vm, cmds) = LoadSas();
        var scoreBefore = vm.Model.Candidates[0].Score;

        // Change the first property's weight dramatically.
        var prop = vm.Model.Scenario!.Properties[0];
        cmds.Mutator.UpdateProperty(prop.Id, null, null, prop.Weight * 10.0 + 5.0);

        var scoreAfter = vm.Model.Candidates[0].Score;

        // The solve must have re-run and produced a different score.
        // (It could theoretically be the same in a degenerate scenario, but
        //  in sas.json changing a weight by 10x MUST change the ranking.)
        Assert.NotEqual(scoreBefore, scoreAfter);
    }

    // -----------------------------------------------------------------------
    // SaveCmd disabled until file path set
    // -----------------------------------------------------------------------

    [Fact]
    public void SaveCmd_DisabledBeforeFilePathSet()
    {
        var vm = ScenarioVMFactory.Create();
        var cmds = ScenarioVMFactory.GetCommands(vm);
        Assert.False(cmds.SaveCmd.CanExecute(null));
    }

    [Fact]
    public void SaveAsCmd_EnablesSaveCmd()
    {
        var (vm, cmds) = LoadSas();
        var tempPath = Path.GetTempFileName() + ".json";
        try
        {
            cmds.SaveAsCmd.Execute(tempPath);
            Assert.True(cmds.SaveCmd.CanExecute(null));
            Assert.Equal(tempPath, vm.Model.FilePath);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    [Fact]
    public void SaveAsCmd_NewEmptyScenario_DoesNotWriteInvalidJson()
    {
        var vm = ScenarioVMFactory.Create();
        var cmds = ScenarioVMFactory.GetCommands(vm);
        var tempPath = Path.GetTempFileName() + ".json";

        File.Delete(tempPath);
        try
        {
            cmds.NewCmd.Execute(null);
            cmds.SaveAsCmd.Execute(tempPath);

            Assert.False(File.Exists(tempPath));
            Assert.Null(vm.Model.FilePath);
            Assert.Contains("Save failed:", vm.Model.Status);
            Assert.Contains("at least one decision", vm.Model.Status);
            Assert.Contains(vm.Model.Warnings, warning => warning == vm.Model.Status);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    [Fact]
    public void SaveAsCmd_NonEmptyInvalidScenario_DoesNotWriteInvalidJson()
    {
        var (vm, cmds) = LoadSas();
        var tempPath = Path.GetTempFileName() + ".json";

        File.Delete(tempPath);
        try
        {
            cmds.Mutator.AddDecision("Decision without alternatives");
            cmds.SaveAsCmd.Execute(tempPath);

            Assert.False(File.Exists(tempPath));
            Assert.True(vm.Model.IsDirty);
            Assert.NotEqual(tempPath, vm.Model.FilePath);
            Assert.Contains("Save failed:", vm.Model.Status);
            Assert.Contains("has no alternatives", vm.Model.Status);
            Assert.Contains(vm.Model.Warnings, warning => warning == vm.Model.Status);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }
}
