using GuideArch.Models;
using GuideArch.ViewModels;
using Xunit;

namespace GuideArch.ViewModels.Tests;

/// <summary>
/// Headless MVVM integration tests — exercises only the VM layer, no Avalonia controls.
/// </summary>
public class VMMvvmIntegrationTests
{
    private const double AbsTol = 1e-9;

    // Rank-0 scores from spec/conformance/expected/*.candidates.json
    private const double SasRank0Score = 0.031180695179944085;
    private const double EdsRank0Score = 0.027987043955595994;

    private static string FindScenarioPath(string filename)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (int i = 0; i < 15; i++)
        {
            var candidate = Path.Combine(dir.FullName, "spec", "conformance", "scenarios", filename);
            if (File.Exists(candidate)) return candidate;
            if (dir.Parent is null) break;
            dir = dir.Parent;
        }
        throw new InvalidOperationException($"Cannot locate spec/conformance/scenarios/{filename}");
    }

    private static (ScenarioVMFactory_Wrapper vm, ScenarioCommands cmds) Load(string filename)
    {
        var vm = ScenarioVMFactory.Create();
        var cmds = ScenarioVMFactory.GetCommands(vm);
        cmds.OpenCmd.Execute(FindScenarioPath(filename));
        Assert.NotNull(vm.Model.Scenario);
        return (new ScenarioVMFactory_Wrapper(vm), cmds);
    }

    // Thin wrapper so tests can access vm.Model cleanly.
    private sealed class ScenarioVMFactory_Wrapper
    {
        private readonly VMx.Components.ComponentVM<ScenarioState> _inner;
        public ScenarioVMFactory_Wrapper(VMx.Components.ComponentVM<ScenarioState> inner) => _inner = inner;
        public ScenarioState Model => _inner.Model;
    }

    // -----------------------------------------------------------------------
    // 1. Load SAS — top score within 1e-9
    // -----------------------------------------------------------------------

    [Fact]
    public void LoadSampleSas_ProducesCorrectTopScore()
    {
        var vm = ScenarioVMFactory.Create();
        var cmds = ScenarioVMFactory.GetCommands(vm);

        cmds.OpenCmd.Execute(FindScenarioPath("sas.json"));

        Assert.True(vm.Model.Candidates.Length > 0);
        double actual = vm.Model.Candidates[0].Score;
        Assert.True(
            Math.Abs(actual - SasRank0Score) <= AbsTol,
            $"SAS rank-0 score {actual:R} != expected {SasRank0Score:R} (diff={Math.Abs(actual - SasRank0Score):E3})");
    }

    // -----------------------------------------------------------------------
    // 2. Edit property weight — triggers resolve and changes top candidate
    // -----------------------------------------------------------------------

    [Fact]
    public void EditPropertyWeight_TriggersResolve_AndChangesTopCandidate()
    {
        var vm = ScenarioVMFactory.Create();
        var cmds = ScenarioVMFactory.GetCommands(vm);
        cmds.OpenCmd.Execute(FindScenarioPath("sas.json"));

        var scoreBefore = vm.Model.Candidates[0].Score;
        var prop = vm.Model.Scenario!.Properties[0];

        // Change weight dramatically so solve output must change.
        cmds.Mutator.UpdateProperty(prop.Id, null, null, prop.Weight * 10.0 + 5.0);

        Assert.True(vm.Model.Candidates.Length > 0, "Candidates should still be present after weight change.");
        double scoreAfter = vm.Model.Candidates[0].Score;
        Assert.NotEqual(scoreBefore, scoreAfter);
    }

    // -----------------------------------------------------------------------
    // 3. Add decision + alternative + property → save → reload preserves state
    // -----------------------------------------------------------------------

    [Fact]
    public void AddDecisionAddAlternativeAddProperty_SaveReload_PreservesState()
    {
        var vm = ScenarioVMFactory.Create();
        var cmds = ScenarioVMFactory.GetCommands(vm);
        cmds.OpenCmd.Execute(FindScenarioPath("sas.json"));

        var decisionsBefore = vm.Model.Scenario!.Decisions.Length;
        var propsBefore = vm.Model.Scenario!.Properties.Length;

        // Add a decision.
        cmds.Mutator.AddDecision();
        Assert.Equal(decisionsBefore + 1, vm.Model.Scenario!.Decisions.Length);

        // Add an alternative to the new decision.
        var newDecision = vm.Model.Scenario!.Decisions[^1];
        cmds.Mutator.AddAlternative(newDecision.Id);

        // Add a property.
        cmds.Mutator.AddProperty();
        Assert.Equal(propsBefore + 1, vm.Model.Scenario!.Properties.Length);

        var decCount = vm.Model.Scenario!.Decisions.Length;
        var altCount = vm.Model.Scenario!.Alternatives.Length;
        var propCount = vm.Model.Scenario!.Properties.Length;

        // Save and reload.
        var tempPath = Path.GetTempFileName() + ".json";
        try
        {
            cmds.SaveAsCmd.Execute(tempPath);

            var vm2 = ScenarioVMFactory.Create();
            var cmds2 = ScenarioVMFactory.GetCommands(vm2);
            cmds2.OpenCmd.Execute(tempPath);

            Assert.NotNull(vm2.Model.Scenario);
            Assert.Equal(decCount, vm2.Model.Scenario!.Decisions.Length);
            Assert.Equal(altCount, vm2.Model.Scenario!.Alternatives.Length);
            Assert.Equal(propCount, vm2.Model.Scenario!.Properties.Length);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    // -----------------------------------------------------------------------
    // 4. Delete decision cascades to alternatives and coefficients
    // -----------------------------------------------------------------------

    [Fact]
    public void DeleteDecision_CascadesToAlternatives_AndCoefficients()
    {
        var vm = ScenarioVMFactory.Create();
        var cmds = ScenarioVMFactory.GetCommands(vm);
        cmds.OpenCmd.Execute(FindScenarioPath("sas.json"));

        var scenario = vm.Model.Scenario!;
        var target = scenario.Decisions[0];
        var altIds = scenario.Alternatives
            .Where(a => a.DecisionId == target.Id)
            .Select(a => a.Id)
            .ToHashSet();
        Assert.NotEmpty(altIds);

        cmds.Mutator.DeleteDecision(target.Id);

        var s = vm.Model.Scenario!;

        // Decision removed.
        Assert.DoesNotContain(s.Decisions, d => d.Id == target.Id);

        // Alternatives removed.
        Assert.DoesNotContain(s.Alternatives, a => a.DecisionId == target.Id);

        // Coefficients referencing those alternatives removed.
        Assert.DoesNotContain(s.Coefficients, c => altIds.Contains(c.AlternativeId));
    }

    // -----------------------------------------------------------------------
    // 5. Solve does NOT re-trigger on scenario name changes
    // -----------------------------------------------------------------------

    [Fact]
    public void SolveTrigger_SkipsScenarioNameChanges()
    {
        var vm = ScenarioVMFactory.Create();
        var cmds = ScenarioVMFactory.GetCommands(vm);
        cmds.OpenCmd.Execute(FindScenarioPath("sas.json"));

        var scoresBefore = vm.Model.Candidates.Select(c => c.Score).ToArray();

        // Change the name — should NOT re-solve (spec viewmodels.md §3.3).
        cmds.Mutator.UpdateScenarioName("Renamed scenario");

        Assert.Equal("Renamed scenario", vm.Model.Scenario!.Name);

        // Candidates should be unchanged (same scores in same order).
        var scoresAfter = vm.Model.Candidates.Select(c => c.Score).ToArray();
        Assert.Equal(scoresBefore.Length, scoresAfter.Length);
        for (int i = 0; i < scoresBefore.Length; i++)
            Assert.Equal(scoresBefore[i], scoresAfter[i]);
    }

    // -----------------------------------------------------------------------
    // 6. Add with no scenario throws at the VM layer (parity with TS/Python).
    //    The add-before-open auto-create convenience is View policy:
    //    MainWindow runs NewCmd first, then calls the mutator.
    // -----------------------------------------------------------------------

    [Fact]
    public void AddDecision_WithNoScenario_Throws()
    {
        var vm = ScenarioVMFactory.Create();
        var cmds = ScenarioVMFactory.GetCommands(vm);

        Assert.Null(vm.Model.Scenario);
        Assert.Throws<ScenarioMutationException>(() => cmds.Mutator.AddDecision());
        Assert.Null(vm.Model.Scenario);
        Assert.False(vm.Model.IsDirty);
    }

    [Fact]
    public void AddProperty_WithNoScenario_Throws()
    {
        var vm = ScenarioVMFactory.Create();
        var cmds = ScenarioVMFactory.GetCommands(vm);

        Assert.Null(vm.Model.Scenario);
        Assert.Throws<ScenarioMutationException>(() => cmds.Mutator.AddProperty());
        Assert.Null(vm.Model.Scenario);
        Assert.False(vm.Model.IsDirty);
    }

    // -----------------------------------------------------------------------
    // 7. View auto-create policy still works end-to-end: NewCmd then Add.
    // -----------------------------------------------------------------------

    [Fact]
    public void NewCmdThenAdd_MirrorsViewAutoCreatePolicy()
    {
        var vm = ScenarioVMFactory.Create();
        var cmds = ScenarioVMFactory.GetCommands(vm);

        cmds.NewCmd.Execute(null);
        cmds.Mutator.AddDecision();
        cmds.Mutator.AddProperty();

        Assert.NotNull(vm.Model.Scenario);
        Assert.Single(vm.Model.Scenario!.Decisions);
        Assert.Single(vm.Model.Scenario!.Properties);
        Assert.True(vm.Model.IsDirty);
    }

    // -----------------------------------------------------------------------
    // 8. EDS sample loads and produces correct top score
    // -----------------------------------------------------------------------

    [Fact]
    public void LoadSampleEds_ProducesCorrectTopScore()
    {
        var vm = ScenarioVMFactory.Create();
        var cmds = ScenarioVMFactory.GetCommands(vm);

        cmds.OpenCmd.Execute(FindScenarioPath("eds.json"));

        Assert.True(vm.Model.Candidates.Length > 0);
        double actual = vm.Model.Candidates[0].Score;
        Assert.True(
            Math.Abs(actual - EdsRank0Score) <= AbsTol,
            $"EDS rank-0 score {actual:R} != expected {EdsRank0Score:R} (diff={Math.Abs(actual - EdsRank0Score):E3})");
    }
}
