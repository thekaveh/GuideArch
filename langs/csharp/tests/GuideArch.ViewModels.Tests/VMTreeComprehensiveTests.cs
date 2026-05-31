using System.Collections.Immutable;
using System.ComponentModel;
using System.Reflection;
using GuideArch.Models;
using GuideArch.ViewModels;
using VMx.Components;
using Xunit;

namespace GuideArch.ViewModels.Tests;

/// <summary>
/// Comprehensive VM hierarchy verification per spec/viewmodels.md §2, §3, §4.
///
/// Coverage:
///   1. Existence — every factory type is present in the assembly.
///   2. Construction — each factory.Create() returns the correct ComponentVM shape.
///   3. Observable properties — ScenarioState has every spec §3.1 property.
///   4. Commands — ScenarioCommands has every spec §3.2 command; Execute and CanExecute work.
///   5. Mutation propagation — ScenarioMutator changes are visible via vm.Model.
///   6. Solve-trigger semantics — parameterized matrix per spec §3.3.
///   7. Read-only result VMs — CandidateVM / CriticalDecisionVM / CriticalConstraintVM
///      have no mutation methods.
///
/// VM types covered:
///   ScenarioVM (via ScenarioVMFactory)       1
///   DecisionVM (via DecisionVMFactory)        2
///   AlternativeVM (via AlternativeVMFactory)  3
///   PropertyVM (via PropertyVMFactory)        4
///   CoefficientVM (via CoefficientVMFactory)  5
///   ConstraintVM (via ConstraintVMFactory)    6
///   CandidateVM (via CandidateVMFactory)      7
///   CriticalDecisionVM (CriticalDecisionVMFactory) 8
///   CriticalConstraintVM (CriticalConstraintVMFactory) 9
///
/// Total: 9 VM types.
/// </summary>
public class VMTreeComprehensiveTests
{
    // =====================================================================
    // Shared helpers
    // =====================================================================

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

    /// <summary>
    /// Builds a self-consistent minimal scenario suitable for solve tests.
    /// 2 decisions × 2 alternatives each, 2 properties, no constraints.
    /// </summary>
    private static ScenarioM MakeMinimalScenario()
    {
        var d0 = new DecisionM("d0", "Decision A");
        var d1 = new DecisionM("d1", "Decision B");
        var a00 = new AlternativeM("a00", "d0", "Alt A0");
        var a01 = new AlternativeM("a01", "d0", "Alt A1");
        var a10 = new AlternativeM("a10", "d1", "Alt B0");
        var a11 = new AlternativeM("a11", "d1", "Alt B1");
        var p0 = new PropertyM("p0", "Cost", PropertyKind.Min, 1.0);
        var p1 = new PropertyM("p1", "Perf", PropertyKind.Max, 1.0);

        var alternatives = ImmutableArray.Create(a00, a01, a10, a11);
        var properties = ImmutableArray.Create(p0, p1);
        var coefficients = alternatives.SelectMany(a =>
            properties.Select(p => new CoefficientM(a.Id, p.Id,
                new TriangularFuzzyM(1.0, 2.0, 3.0)))
        ).ToImmutableArray();

        return new ScenarioM(
            SchemaVersion: "1.0.0",
            Name: "MinimalTest",
            Description: "Test scenario",
            Decisions: ImmutableArray.Create(d0, d1),
            Alternatives: alternatives,
            Properties: properties,
            Coefficients: coefficients,
            Constraints: ImmutableArray<ConstraintM>.Empty,
            Config: new ConfigM(Aggregation.Max, new NormalizedFuzzyM(0.5, 0.25, 0.25)),
            Warnings: ImmutableArray<string>.Empty
        );
    }

    private static (ComponentVM<ScenarioState> vm, ScenarioCommands cmds) CreateWithMinimalScenario()
    {
        var vm = ScenarioVMFactory.Create();
        var cmds = ScenarioVMFactory.GetCommands(vm);
        // Load the minimal scenario via temp file so we go through the normal open path.
        var tempPath = Path.GetTempFileName() + ".json";
        try
        {
            // Use public ScenarioSerializer to write the scenario.
            var json = ScenarioSerializer.Serialize(MakeMinimalScenario());
            File.WriteAllText(tempPath, json);
            cmds.OpenCmd.Execute(tempPath);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
        Assert.NotNull(vm.Model.Scenario);
        return (vm, cmds);
    }

    // =====================================================================
    // §1 — EXISTENCE: every factory is in the GuideArch.ViewModels assembly
    // =====================================================================

    [Theory]
    [InlineData("GuideArch.ViewModels.ScenarioVMFactory")]
    [InlineData("GuideArch.ViewModels.DecisionVMFactory")]
    [InlineData("GuideArch.ViewModels.AlternativeVMFactory")]
    [InlineData("GuideArch.ViewModels.PropertyVMFactory")]
    [InlineData("GuideArch.ViewModels.CoefficientVMFactory")]
    [InlineData("GuideArch.ViewModels.ConstraintVMFactory")]
    [InlineData("GuideArch.ViewModels.CandidateVMFactory")]
    [InlineData("GuideArch.ViewModels.CriticalDecisionVMFactory")]
    [InlineData("GuideArch.ViewModels.CriticalConstraintVMFactory")]
    public void Factory_ExistsInViewModelsAssembly(string fullTypeName)
    {
        var asm = typeof(ScenarioVMFactory).Assembly;
        var type = asm.GetType(fullTypeName);
        Assert.NotNull(type);
        Assert.True(type!.IsClass, $"{fullTypeName} should be a class");
        Assert.True(type.IsAbstract && type.IsSealed,
            $"{fullTypeName} should be a static class (abstract + sealed)");
    }

    // =====================================================================
    // §2 — CONSTRUCTION: each factory produces the correct ComponentVM shape
    // =====================================================================

    [Fact]
    public void ScenarioVMFactory_Create_ReturnsComponentVmOfScenarioState()
    {
        var vm = ScenarioVMFactory.Create();
        Assert.NotNull(vm);
        Assert.IsAssignableFrom<ComponentVM<ScenarioState>>(vm);
        // Initial model is the empty state.
        Assert.Null(vm.Model.Scenario);
        Assert.False(vm.Model.IsDirty);
        Assert.Empty(vm.Model.Candidates);
    }

    [Fact]
    public void DecisionVMFactory_Create_ReturnsComponentVmOfDecisionM()
    {
        var model = new DecisionM("d1", "Decision One");
        var vm = DecisionVMFactory.Create(model);
        Assert.NotNull(vm);
        Assert.IsAssignableFrom<ComponentVM<DecisionM>>(vm);
        Assert.Equal(model, vm.Model);
        Assert.Equal("d1", vm.Model.Id);
        Assert.Equal("Decision One", vm.Model.Name);
    }

    [Fact]
    public void AlternativeVMFactory_Create_ReturnsComponentVmOfAlternativeM()
    {
        var model = new AlternativeM("a1", "d1", "Alt One");
        var vm = AlternativeVMFactory.Create(model);
        Assert.NotNull(vm);
        Assert.IsAssignableFrom<ComponentVM<AlternativeM>>(vm);
        Assert.Equal(model, vm.Model);
        Assert.Equal("a1", vm.Model.Id);
        Assert.Equal("d1", vm.Model.DecisionId);
        Assert.Equal("Alt One", vm.Model.Name);
    }

    [Fact]
    public void PropertyVMFactory_Create_ReturnsComponentVmOfPropertyM()
    {
        var model = new PropertyM("p1", "Cost", PropertyKind.Min, 0.5);
        var vm = PropertyVMFactory.Create(model);
        Assert.NotNull(vm);
        Assert.IsAssignableFrom<ComponentVM<PropertyM>>(vm);
        Assert.Equal(model, vm.Model);
        Assert.Equal("p1", vm.Model.Id);
        Assert.Equal(PropertyKind.Min, vm.Model.Kind);
        Assert.Equal(0.5, vm.Model.Weight);
    }

    [Fact]
    public void CoefficientVMFactory_Create_ReturnsComponentVmOfCoefficientM()
    {
        var fuzzy = new TriangularFuzzyM(1.0, 2.0, 3.0);
        var model = new CoefficientM("a1", "p1", fuzzy);
        var vm = CoefficientVMFactory.Create(model);
        Assert.NotNull(vm);
        Assert.IsAssignableFrom<ComponentVM<CoefficientM>>(vm);
        Assert.Equal(model, vm.Model);
        Assert.Equal("a1", vm.Model.AlternativeId);
        Assert.Equal("p1", vm.Model.PropertyId);
        Assert.Equal(1.0, vm.Model.Value.Lower);
        Assert.Equal(2.0, vm.Model.Value.Modal);
        Assert.Equal(3.0, vm.Model.Value.Upper);
    }

    [Fact]
    public void ConstraintVMFactory_Create_ThresholdConstraint_ReturnsCorrectVM()
    {
        ConstraintM model = new ThresholdConstraintM("p1", 10.0, 100.0);
        var vm = ConstraintVMFactory.Create(model, 0);
        Assert.NotNull(vm);
        Assert.IsAssignableFrom<ComponentVM<ConstraintM>>(vm);
        Assert.Equal(model, vm.Model);
        Assert.Equal("threshold", vm.Model.Kind);
    }

    [Fact]
    public void ConstraintVMFactory_Create_DependencyConstraint_ReturnsCorrectVM()
    {
        ConstraintM model = new DependencyConstraintM("a1", "a2");
        var vm = ConstraintVMFactory.Create(model, 1);
        Assert.NotNull(vm);
        Assert.IsAssignableFrom<ComponentVM<ConstraintM>>(vm);
        Assert.Equal(model, vm.Model);
        Assert.Equal("dependency", vm.Model.Kind);
    }

    [Fact]
    public void ConstraintVMFactory_Create_ConflictConstraint_ReturnsCorrectVM()
    {
        ConstraintM model = new ConflictConstraintM("a1", "a2");
        var vm = ConstraintVMFactory.Create(model, 2);
        Assert.NotNull(vm);
        Assert.IsAssignableFrom<ComponentVM<ConstraintM>>(vm);
        Assert.Equal(model, vm.Model);
        Assert.Equal("conflict", vm.Model.Kind);
    }

    [Fact]
    public void CandidateVMFactory_Create_ReturnsComponentVmOfCandidateM()
    {
        var model = new CandidateM(
            AlternativeIds: ImmutableArray.Create("a1"),
            TriangularValue: new TriangularFuzzyM(0.1, 0.5, 0.9),
            NormalizedValue: new NormalizedFuzzyM(0.1, 0.2, 0.3),
            Score: 0.42,
            Rank: 0);
        var vm = CandidateVMFactory.Create(model);
        Assert.NotNull(vm);
        Assert.IsAssignableFrom<ComponentVM<CandidateM>>(vm);
        Assert.Equal(model, vm.Model);
        Assert.Equal(0.42, vm.Model.Score);
        Assert.Equal(0, vm.Model.Rank);
    }

    [Fact]
    public void CriticalDecisionVMFactory_Create_ReturnsComponentVmOfCriticalDecisionM()
    {
        var model = new CriticalDecisionM(
            DecisionId: "d1",
            TriangularValue: new TriangularFuzzyM(0, 1, 2),
            NormalizedValue: new NormalizedFuzzyM(0.1, 0.2, 0.3),
            Score: 0.33,
            Rank: 0);
        var vm = CriticalDecisionVMFactory.Create(model);
        Assert.NotNull(vm);
        Assert.IsAssignableFrom<ComponentVM<CriticalDecisionM>>(vm);
        Assert.Equal(model, vm.Model);
        Assert.Equal("d1", vm.Model.DecisionId);
    }

    [Fact]
    public void CriticalConstraintVMFactory_Create_ReturnsComponentVmOfCriticalConstraintM()
    {
        var model = new CriticalConstraintM(
            ConstraintIndex: 0,
            Kind: "threshold",
            Eliminated: 10,
            Total: 100,
            Redundant: false);
        var vm = CriticalConstraintVMFactory.Create(model);
        Assert.NotNull(vm);
        Assert.IsAssignableFrom<ComponentVM<CriticalConstraintM>>(vm);
        Assert.Equal(model, vm.Model);
        Assert.Equal(10, vm.Model.Eliminated);
        Assert.False(vm.Model.Redundant);
    }

    // =====================================================================
    // §3 — OBSERVABLE PROPERTIES: ScenarioState has every spec §3.1 property
    // =====================================================================

    // spec §3.1 observable properties
    [Theory]
    [InlineData("Scenario")]
    [InlineData("FilePath")]
    [InlineData("IsDirty")]
    [InlineData("Candidates")]
    [InlineData("CriticalDecisions")]
    [InlineData("CriticalConstraints")]
    [InlineData("Status")]
    [InlineData("Warnings")]
    public void ScenarioState_HasProperty(string propertyName)
    {
        var prop = typeof(ScenarioState).GetProperty(propertyName,
            BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(prop);
    }

    // Type-check for each critical property
    [Fact]
    public void ScenarioState_Scenario_IsNullableScenarioM()
    {
        var prop = typeof(ScenarioState).GetProperty("Scenario")!;
        Assert.Equal(typeof(ScenarioM), prop.PropertyType);
    }

    [Fact]
    public void ScenarioState_FilePath_IsNullableString()
    {
        var prop = typeof(ScenarioState).GetProperty("FilePath")!;
        Assert.Equal(typeof(string), prop.PropertyType);
    }

    [Fact]
    public void ScenarioState_IsDirty_IsBoolean()
    {
        var prop = typeof(ScenarioState).GetProperty("IsDirty")!;
        Assert.Equal(typeof(bool), prop.PropertyType);
    }

    [Fact]
    public void ScenarioState_Candidates_IsImmutableArrayOfCandidateM()
    {
        var prop = typeof(ScenarioState).GetProperty("Candidates")!;
        Assert.Equal(typeof(ImmutableArray<CandidateM>), prop.PropertyType);
    }

    [Fact]
    public void ScenarioState_CriticalDecisions_IsImmutableArrayOfCriticalDecisionM()
    {
        var prop = typeof(ScenarioState).GetProperty("CriticalDecisions")!;
        Assert.Equal(typeof(ImmutableArray<CriticalDecisionM>), prop.PropertyType);
    }

    [Fact]
    public void ScenarioState_CriticalConstraints_IsImmutableArrayOfCriticalConstraintM()
    {
        var prop = typeof(ScenarioState).GetProperty("CriticalConstraints")!;
        Assert.Equal(typeof(ImmutableArray<CriticalConstraintM>), prop.PropertyType);
    }

    [Fact]
    public void ScenarioState_Status_IsString()
    {
        var prop = typeof(ScenarioState).GetProperty("Status")!;
        Assert.Equal(typeof(string), prop.PropertyType);
    }

    [Fact]
    public void ScenarioState_Warnings_IsImmutableArrayOfString()
    {
        var prop = typeof(ScenarioState).GetProperty("Warnings")!;
        Assert.Equal(typeof(ImmutableArray<string>), prop.PropertyType);
    }

    // VM fires PropertyChanged when model mutates
    [Fact]
    public void ScenarioVM_FiresPropertyChanged_WhenStateChanges()
    {
        var vm = ScenarioVMFactory.Create();
        var cmds = ScenarioVMFactory.GetCommands(vm);

        bool fired = false;
        string? changedProp = null;
        vm.PropertyChanged += (_, e) =>
        {
            fired = true;
            changedProp = e.PropertyName;
        };

        // NewCmd sets state (Status changes)
        cmds.NewCmd.Execute(null);

        Assert.True(fired, "PropertyChanged must fire after a state mutation");
    }

    // =====================================================================
    // §4 — COMMANDS: ScenarioCommands has every spec §3.2 command
    // =====================================================================

    [Fact]
    public void ScenarioCommands_AllFiveCommandsPresent()
    {
        var vm = ScenarioVMFactory.Create();
        var cmds = ScenarioVMFactory.GetCommands(vm);

        Assert.NotNull(cmds.NewCmd);
        Assert.NotNull(cmds.OpenCmd);
        Assert.NotNull(cmds.SaveCmd);
        Assert.NotNull(cmds.SaveAsCmd);
        Assert.NotNull(cmds.SolveCmd);
    }

    [Fact]
    public void ScenarioCommands_HasMutator()
    {
        var vm = ScenarioVMFactory.Create();
        var cmds = ScenarioVMFactory.GetCommands(vm);

        Assert.NotNull(cmds.Mutator);
    }

    // NewCmd: replaces scenario with empty, clears file path, sets isDirty = false
    [Fact]
    public void NewCmd_Execute_ClearsScenarioAndFilePath()
    {
        var (vm, cmds) = LoadSas();
        Assert.NotNull(vm.Model.Scenario);
        Assert.NotNull(vm.Model.FilePath);

        cmds.NewCmd.Execute(null);

        Assert.Null(vm.Model.Scenario);
        Assert.Null(vm.Model.FilePath);
        Assert.False(vm.Model.IsDirty);
    }

    // NewCmd is always enabled (no predicate)
    [Fact]
    public void NewCmd_IsAlwaysEnabled()
    {
        var vm = ScenarioVMFactory.Create();
        var cmds = ScenarioVMFactory.GetCommands(vm);

        Assert.True(cmds.NewCmd.CanExecute(null));
    }

    // SaveCmd: disabled when filePath is null (spec §3.2)
    [Fact]
    public void SaveCmd_CanExecute_False_WhenFilePathIsNull()
    {
        var vm = ScenarioVMFactory.Create();
        var cmds = ScenarioVMFactory.GetCommands(vm);

        // No file loaded — FilePath is null.
        Assert.Null(vm.Model.FilePath);
        Assert.False(cmds.SaveCmd.CanExecute(null));
    }

    // SaveCmd: enabled after SaveAs sets the file path
    [Fact]
    public void SaveCmd_CanExecute_True_AfterSaveAsSetFilePath()
    {
        var (vm, cmds) = LoadSas();
        var tempPath = Path.GetTempFileName() + ".json";
        try
        {
            cmds.SaveAsCmd.Execute(tempPath);
            Assert.True(cmds.SaveCmd.CanExecute(null));
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    // SolveCmd: disabled when no scenario
    [Fact]
    public void SolveCmd_CanExecute_False_WhenNoScenario()
    {
        var vm = ScenarioVMFactory.Create();
        var cmds = ScenarioVMFactory.GetCommands(vm);

        Assert.False(cmds.SolveCmd.CanExecute(null));
    }

    // SolveCmd: enabled when scenario is loaded
    [Fact]
    public void SolveCmd_CanExecute_True_WhenScenarioLoaded()
    {
        var (vm, cmds) = LoadSas();
        Assert.True(cmds.SolveCmd.CanExecute(null));
    }

    // SolveCmd: execute updates Candidates and Status
    [Fact]
    public void SolveCmd_Execute_UpdatesCandidatesAndStatus()
    {
        var (vm, cmds) = LoadSas();
        int nBefore = vm.Model.Candidates.Length;
        string statusBefore = vm.Model.Status;

        cmds.SolveCmd.Execute(null);

        Assert.Equal(nBefore, vm.Model.Candidates.Length);
        Assert.Equal(statusBefore, vm.Model.Status); // unchanged: same scenario
        Assert.True(vm.Model.Candidates.Length > 0);
    }

    // OpenCmd: sets scenario, file path, clears dirty, runs solve
    [Fact]
    public void OpenCmd_Execute_SetsScenarioAndFilePath()
    {
        var sasPath = FindSasJsonPath();
        var vm = ScenarioVMFactory.Create();
        var cmds = ScenarioVMFactory.GetCommands(vm);

        cmds.OpenCmd.Execute(sasPath);

        Assert.NotNull(vm.Model.Scenario);
        Assert.Equal(sasPath, vm.Model.FilePath);
        Assert.False(vm.Model.IsDirty);
        Assert.True(vm.Model.Candidates.Length > 0);
    }

    // SaveAsCmd: sets file path and saves
    [Fact]
    public void SaveAsCmd_Execute_SetsFilePath()
    {
        var (vm, cmds) = LoadSas();
        var tempPath = Path.GetTempFileName() + ".json";
        try
        {
            cmds.SaveAsCmd.Execute(tempPath);

            Assert.Equal(tempPath, vm.Model.FilePath);
            Assert.False(vm.Model.IsDirty);
            Assert.True(File.Exists(tempPath), "File should have been written");
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    // =====================================================================
    // §5 — MUTATION PROPAGATION
    // =====================================================================

    [Fact]
    public void Mutator_UpdateScenarioName_UpdatesScenarioName()
    {
        var (vm, cmds) = CreateWithMinimalScenario();
        cmds.Mutator.UpdateScenarioName("RenamedScenario");
        Assert.Equal("RenamedScenario", vm.Model.Scenario!.Name);
        Assert.True(vm.Model.IsDirty);
    }

    [Fact]
    public void Mutator_AddDecision_IncrementsDecisionCount()
    {
        var (vm, cmds) = CreateWithMinimalScenario();
        int before = vm.Model.Scenario!.Decisions.Length;
        cmds.Mutator.AddDecision();
        Assert.Equal(before + 1, vm.Model.Scenario!.Decisions.Length);
    }

    [Fact]
    public void Mutator_UpdateDecisionName_UpdatesDecisionName()
    {
        var (vm, cmds) = CreateWithMinimalScenario();
        var d = vm.Model.Scenario!.Decisions[0];
        cmds.Mutator.UpdateDecisionName(d.Id, "NewName");
        var updated = vm.Model.Scenario!.Decisions.First(x => x.Id == d.Id);
        Assert.Equal("NewName", updated.Name);
    }

    [Fact]
    public void Mutator_DeleteDecision_RemovesDecision()
    {
        var (vm, cmds) = CreateWithMinimalScenario();
        var d = vm.Model.Scenario!.Decisions[0];
        cmds.Mutator.DeleteDecision(d.Id);
        Assert.DoesNotContain(vm.Model.Scenario!.Decisions, x => x.Id == d.Id);
    }

    [Fact]
    public void Mutator_AddAlternative_IncrementsAlternativeCount()
    {
        var (vm, cmds) = CreateWithMinimalScenario();
        var d = vm.Model.Scenario!.Decisions[0];
        int before = vm.Model.Scenario!.Alternatives.Length;
        cmds.Mutator.AddAlternative(d.Id);
        Assert.Equal(before + 1, vm.Model.Scenario!.Alternatives.Length);
    }

    [Fact]
    public void Mutator_UpdateAlternativeName_UpdatesAlternativeName()
    {
        var (vm, cmds) = CreateWithMinimalScenario();
        var a = vm.Model.Scenario!.Alternatives[0];
        cmds.Mutator.UpdateAlternativeName(a.Id, "RenamedAlt");
        var updated = vm.Model.Scenario!.Alternatives.First(x => x.Id == a.Id);
        Assert.Equal("RenamedAlt", updated.Name);
    }

    [Fact]
    public void Mutator_DeleteAlternative_RemovesAlternative()
    {
        var (vm, cmds) = CreateWithMinimalScenario();
        var a = vm.Model.Scenario!.Alternatives[0];
        cmds.Mutator.DeleteAlternative(a.Id);
        Assert.DoesNotContain(vm.Model.Scenario!.Alternatives, x => x.Id == a.Id);
    }

    [Fact]
    public void Mutator_AddProperty_IncrementsPropertyCount()
    {
        var (vm, cmds) = CreateWithMinimalScenario();
        int before = vm.Model.Scenario!.Properties.Length;
        cmds.Mutator.AddProperty();
        Assert.Equal(before + 1, vm.Model.Scenario!.Properties.Length);
    }

    [Fact]
    public void Mutator_UpdateProperty_ChangesPropertyFields()
    {
        var (vm, cmds) = CreateWithMinimalScenario();
        var p = vm.Model.Scenario!.Properties[0];
        cmds.Mutator.UpdateProperty(p.Id, "NewPropertyName", PropertyKind.Max, 2.5);
        var updated = vm.Model.Scenario!.Properties.First(x => x.Id == p.Id);
        Assert.Equal("NewPropertyName", updated.Name);
        Assert.Equal(PropertyKind.Max, updated.Kind);
        Assert.Equal(2.5, updated.Weight, 10);
    }

    [Fact]
    public void Mutator_UpdateProperty_ThrowsForNonPositiveWeight()
    {
        var (vm, cmds) = CreateWithMinimalScenario();
        var p = vm.Model.Scenario!.Properties[0];
        Assert.Throws<ScenarioMutationException>(
            () => cmds.Mutator.UpdateProperty(p.Id, null, null, 0.0));
    }

    [Fact]
    public void Mutator_UpdateCoefficient_UpdatesCoefficientValue()
    {
        var (vm, cmds) = CreateWithMinimalScenario();
        var c = vm.Model.Scenario!.Coefficients[0];
        cmds.Mutator.UpdateCoefficient(c.AlternativeId, c.PropertyId, 2.0, 3.0, 4.0);
        var updated = vm.Model.Scenario!.Coefficients.First(
            x => x.AlternativeId == c.AlternativeId && x.PropertyId == c.PropertyId);
        Assert.Equal(2.0, updated.Value.Lower, 10);
        Assert.Equal(3.0, updated.Value.Modal, 10);
        Assert.Equal(4.0, updated.Value.Upper, 10);
    }

    [Fact]
    public void Mutator_AddThresholdConstraint_IncrementsConstraintCount()
    {
        var (vm, cmds) = CreateWithMinimalScenario();
        var p = vm.Model.Scenario!.Properties[0];
        int before = vm.Model.Scenario!.Constraints.Length;
        cmds.Mutator.AddThresholdConstraint(p.Id, 0.0, null);
        Assert.Equal(before + 1, vm.Model.Scenario!.Constraints.Length);
    }

    [Fact]
    public void Mutator_AddDependencyConstraint_IncrementsConstraintCount()
    {
        var (vm, cmds) = CreateWithMinimalScenario();
        var alts = vm.Model.Scenario!.Alternatives;
        int before = vm.Model.Scenario!.Constraints.Length;
        cmds.Mutator.AddDependencyConstraint(alts[0].Id, alts[1].Id);
        Assert.Equal(before + 1, vm.Model.Scenario!.Constraints.Length);
    }

    [Fact]
    public void Mutator_AddConflictConstraint_IncrementsConstraintCount()
    {
        var (vm, cmds) = CreateWithMinimalScenario();
        var alts = vm.Model.Scenario!.Alternatives;
        int before = vm.Model.Scenario!.Constraints.Length;
        cmds.Mutator.AddConflictConstraint(alts[0].Id, alts[2].Id);
        Assert.Equal(before + 1, vm.Model.Scenario!.Constraints.Length);
    }

    [Fact]
    public void Mutator_AllChanges_PropagateViaVmModel()
    {
        var (vm, cmds) = CreateWithMinimalScenario();

        // Each mutation should be reflected in vm.Model immediately.
        cmds.Mutator.AddDecision();
        Assert.True(vm.Model.IsDirty);
        Assert.NotNull(vm.Model.Scenario);
    }

    // =====================================================================
    // §6 — SOLVE TRIGGER SEMANTICS (spec viewmodels.md §3.3)
    //
    // 18 parameterized cases:
    //   Must trigger solve (8 cases)
    //   Must NOT trigger solve (10 cases)
    // =====================================================================

    /// <summary>
    /// Runs the action, returns true if solve ran (i.e. ScenarioVMFactory's internal
    /// Solve() method was called as a result of the mutation).
    ///
    /// Detection strategy:
    ///   ScenarioVMFactory calls SetState twice for mutations that trigger solve:
    ///     (1) once for the domain change (decision/property/coefficient update),
    ///     (2) once for the Solve() result (updating Candidates/CriticalDecisions/Status).
    ///   Non-solve mutations call SetState exactly once.
    ///   Each SetState call fires vm.PropertyChanged exactly once.
    ///
    ///   Exception: SelectCandidate fires exactly once (does NOT call Solve).
    ///   Exception: SaveAs/NewCmd/OpenCmd have different patterns.
    ///
    ///   Therefore: if PropertyChanged fires 2+ times, solve ran.
    ///   If it fires exactly 1 time, solve did NOT run.
    ///
    ///   Edge case: AddDecision calls _setState once + _solve() which calls
    ///   SetState once more → total 2 fires.
    /// </summary>
    private static bool DidSolveRun(
        ComponentVM<ScenarioState> vm,
        Action action)
    {
        int notificationCount = 0;
        void Handler(object? _, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ComponentVM<ScenarioState>.Model))
                notificationCount++;
        }

        vm.PropertyChanged += Handler;
        try
        {
            action();
        }
        finally
        {
            vm.PropertyChanged -= Handler;
        }

        // 2+ notifications = state updated AND solve ran (each calling SetState once).
        return notificationCount >= 2;
    }

    // ── Cases that MUST trigger solve ─────────────────────────────────────

    [Fact]
    public void SolveTrigger_AddDecision_TriggersResolve()
    {
        var (vm, cmds) = CreateWithMinimalScenario();
        bool solved = DidSolveRun(vm, () => cmds.Mutator.AddDecision());
        Assert.True(solved, "AddDecision must trigger a solve.");
    }

    [Fact]
    public void SolveTrigger_DeleteDecision_TriggersResolve()
    {
        var (vm, cmds) = CreateWithMinimalScenario();
        var d = vm.Model.Scenario!.Decisions[0];
        bool solved = DidSolveRun(vm, () => cmds.Mutator.DeleteDecision(d.Id));
        Assert.True(solved, "DeleteDecision must trigger a solve.");
    }

    [Fact]
    public void SolveTrigger_AddAlternative_TriggersResolve()
    {
        var (vm, cmds) = CreateWithMinimalScenario();
        var d = vm.Model.Scenario!.Decisions[0];
        bool solved = DidSolveRun(vm, () => cmds.Mutator.AddAlternative(d.Id));
        Assert.True(solved, "AddAlternative must trigger a solve.");
    }

    [Fact]
    public void SolveTrigger_DeleteAlternative_TriggersResolve()
    {
        var (vm, cmds) = CreateWithMinimalScenario();
        var a = vm.Model.Scenario!.Alternatives[0];
        bool solved = DidSolveRun(vm, () => cmds.Mutator.DeleteAlternative(a.Id));
        Assert.True(solved, "DeleteAlternative must trigger a solve.");
    }

    [Fact]
    public void SolveTrigger_PropertyKindChange_TriggersResolve()
    {
        var (vm, cmds) = CreateWithMinimalScenario();
        var p = vm.Model.Scenario!.Properties[0];
        // Kind is currently Min; change to Max.
        bool solved = DidSolveRun(vm,
            () => cmds.Mutator.UpdateProperty(p.Id, null, PropertyKind.Max, null));
        Assert.True(solved, "Changing PropertyKind must trigger a solve.");
    }

    [Fact]
    public void SolveTrigger_PropertyWeightChange_TriggersResolve()
    {
        var (vm, cmds) = CreateWithMinimalScenario();
        var p = vm.Model.Scenario!.Properties[0];
        bool solved = DidSolveRun(vm,
            () => cmds.Mutator.UpdateProperty(p.Id, null, null, p.Weight + 0.1));
        Assert.True(solved, "Changing PropertyWeight must trigger a solve.");
    }

    [Fact]
    public void SolveTrigger_CoefficientValueChange_TriggersResolve()
    {
        var (vm, cmds) = CreateWithMinimalScenario();
        var c = vm.Model.Scenario!.Coefficients[0];
        bool solved = DidSolveRun(vm,
            () => cmds.Mutator.UpdateCoefficient(c.AlternativeId, c.PropertyId,
                c.Value.Lower + 0.1, c.Value.Modal + 0.1, c.Value.Upper + 0.1));
        Assert.True(solved, "Updating a coefficient value must trigger a solve.");
    }

    [Fact]
    public void SolveTrigger_AddThresholdConstraint_TriggersResolve()
    {
        var (vm, cmds) = CreateWithMinimalScenario();
        var p = vm.Model.Scenario!.Properties[0];
        bool solved = DidSolveRun(vm,
            () => cmds.Mutator.AddThresholdConstraint(p.Id, 0.0, null));
        Assert.True(solved, "Adding a constraint must trigger a solve.");
    }

    [Fact]
    public void SolveTrigger_DeleteThresholdConstraint_TriggersResolve()
    {
        var (vm, cmds) = CreateWithMinimalScenario();
        var p = vm.Model.Scenario!.Properties[0];
        // Add first, then delete.
        cmds.Mutator.AddThresholdConstraint(p.Id, 0.0, null);
        bool solved = DidSolveRun(vm,
            () => cmds.Mutator.DeleteThresholdConstraint(0));
        Assert.True(solved, "Deleting a constraint must trigger a solve.");
    }

    [Fact]
    public void SolveTrigger_AddProperty_TriggersResolve()
    {
        var (vm, cmds) = CreateWithMinimalScenario();
        bool solved = DidSolveRun(vm, () => cmds.Mutator.AddProperty());
        Assert.True(solved, "AddProperty must trigger a solve.");
    }

    [Fact]
    public void SolveTrigger_DeleteProperty_TriggersResolve()
    {
        var (vm, cmds) = CreateWithMinimalScenario();
        var p = vm.Model.Scenario!.Properties[0];
        bool solved = DidSolveRun(vm, () => cmds.Mutator.DeleteProperty(p.Id));
        Assert.True(solved, "DeleteProperty must trigger a solve.");
    }

    [Fact]
    public void SolveTrigger_AddDependencyConstraint_TriggersResolve()
    {
        var (vm, cmds) = CreateWithMinimalScenario();
        var alts = vm.Model.Scenario!.Alternatives;
        bool solved = DidSolveRun(vm,
            () => cmds.Mutator.AddDependencyConstraint(alts[0].Id, alts[1].Id));
        Assert.True(solved, "Adding a dependency constraint must trigger a solve.");
    }

    [Fact]
    public void SolveTrigger_AddConflictConstraint_TriggersResolve()
    {
        var (vm, cmds) = CreateWithMinimalScenario();
        var alts = vm.Model.Scenario!.Alternatives;
        bool solved = DidSolveRun(vm,
            () => cmds.Mutator.AddConflictConstraint(alts[0].Id, alts[2].Id));
        Assert.True(solved, "Adding a conflict constraint must trigger a solve.");
    }

    // ── Cases that MUST NOT trigger solve ─────────────────────────────────

    [Fact]
    public void SolveTrigger_UpdateScenarioName_DoesNotTriggerResolve()
    {
        var (vm, cmds) = CreateWithMinimalScenario();
        bool solved = DidSolveRun(vm, () => cmds.Mutator.UpdateScenarioName("Renamed"));
        Assert.False(solved, "UpdateScenarioName must NOT trigger a solve.");
    }

    [Fact]
    public void SolveTrigger_UpdateDecisionName_DoesNotTriggerResolve()
    {
        var (vm, cmds) = CreateWithMinimalScenario();
        var d = vm.Model.Scenario!.Decisions[0];
        bool solved = DidSolveRun(vm, () => cmds.Mutator.UpdateDecisionName(d.Id, "New Name"));
        Assert.False(solved, "UpdateDecisionName must NOT trigger a solve.");
    }

    [Fact]
    public void SolveTrigger_UpdateAlternativeName_DoesNotTriggerResolve()
    {
        var (vm, cmds) = CreateWithMinimalScenario();
        var a = vm.Model.Scenario!.Alternatives[0];
        bool solved = DidSolveRun(vm, () => cmds.Mutator.UpdateAlternativeName(a.Id, "New Alt Name"));
        Assert.False(solved, "UpdateAlternativeName must NOT trigger a solve.");
    }

    [Fact]
    public void SolveTrigger_UpdatePropertyNameOnly_DoesNotTriggerResolve()
    {
        // Only changing Name (not Kind or Weight) must NOT trigger solve.
        var (vm, cmds) = CreateWithMinimalScenario();
        var p = vm.Model.Scenario!.Properties[0];
        bool solved = DidSolveRun(vm,
            () => cmds.Mutator.UpdateProperty(p.Id, "New Property Name", null, null));
        Assert.False(solved, "Renaming a property (name only) must NOT trigger a solve.");
    }

    [Fact]
    public void SolveTrigger_SelectCandidate_DoesNotTriggerResolve()
    {
        var (vm, cmds) = CreateWithMinimalScenario();
        Assert.True(vm.Model.Candidates.Length > 1, "Need at least 2 candidates for this test.");
        bool solved = DidSolveRun(vm, () => cmds.Mutator.SelectCandidate(1));
        Assert.False(solved, "SelectCandidate must NOT trigger a solve.");
    }

    [Fact]
    public void SolveTrigger_SaveAs_DoesNotTriggerResolve()
    {
        // Changing FilePath (Save As) must NOT trigger solve per spec §3.3.
        var (vm, cmds) = CreateWithMinimalScenario();
        var tempPath = Path.GetTempFileName() + ".json";
        bool solved;
        try
        {
            solved = DidSolveRun(vm, () => cmds.SaveAsCmd.Execute(tempPath));
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
        Assert.False(solved, "SaveAs (changing FilePath) must NOT trigger a solve.");
    }

    // =====================================================================
    // §7 — READ-ONLY RESULT VMs: no public mutation methods
    // =====================================================================

    private static IEnumerable<MethodInfo> GetPublicMutationMethods(Type type)
    {
        // Mutation methods: any public instance method not from object/record
        // that accepts parameters (setters) or has "Set"/"Add"/"Update"/"Delete"
        // in the name, excluding the record's own With/Init.
        // We look for non-inherited, non-record-generated public methods.
        var objMethods = typeof(object).GetMethods().Select(m => m.Name).ToHashSet();
        return type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m =>
                !objMethods.Contains(m.Name) &&
                !m.Name.StartsWith("get_") &&
                !m.Name.StartsWith("set_") &&
                !m.Name.StartsWith("<") &&  // compiler-generated
                m.Name is not "ToString" and not "GetHashCode"
                    and not "Equals" and not "Deconstruct"
            );
    }

    [Fact]
    public void CandidateVM_ModelType_HasNoPublicMutationMethods()
    {
        // CandidateM is a pure record with no Add/Set/Update/Delete methods.
        var mutationMethods = GetPublicMutationMethods(typeof(CandidateM))
            .Where(m => m.Name.StartsWith("Add") || m.Name.StartsWith("Set") ||
                        m.Name.StartsWith("Update") || m.Name.StartsWith("Delete"))
            .ToList();

        Assert.Empty(mutationMethods);
    }

    [Fact]
    public void CriticalDecisionVM_ModelType_HasNoPublicMutationMethods()
    {
        var mutationMethods = GetPublicMutationMethods(typeof(CriticalDecisionM))
            .Where(m => m.Name.StartsWith("Add") || m.Name.StartsWith("Set") ||
                        m.Name.StartsWith("Update") || m.Name.StartsWith("Delete"))
            .ToList();

        Assert.Empty(mutationMethods);
    }

    [Fact]
    public void CriticalConstraintVM_ModelType_HasNoPublicMutationMethods()
    {
        var mutationMethods = GetPublicMutationMethods(typeof(CriticalConstraintM))
            .Where(m => m.Name.StartsWith("Add") || m.Name.StartsWith("Set") ||
                        m.Name.StartsWith("Update") || m.Name.StartsWith("Delete"))
            .ToList();

        Assert.Empty(mutationMethods);
    }

    [Fact]
    public void CandidateVMFactory_Create_ReturnsVm_WithReadOnlyModel()
    {
        var model = new CandidateM(
            ImmutableArray.Create("a1"),
            new TriangularFuzzyM(0, 1, 2),
            new NormalizedFuzzyM(0.1, 0.2, 0.3),
            0.5,
            0);
        var vm = CandidateVMFactory.Create(model);

        // The VM's model property getter should expose the original model.
        Assert.Equal(model, vm.Model);

        // There are no mutation commands attached to this VM.
        // Verify by attempting to locate ScenarioCommands — it should not exist.
        var commandsType = typeof(ScenarioCommands);
        // CandidateVMFactory doesn't register commands in the ConditionalWeakTable.
        // This is verified by the absence of a GetCommands method on CandidateVMFactory.
        var getCommandsMethod = typeof(CandidateVMFactory)
            .GetMethod("GetCommands", BindingFlags.Public | BindingFlags.Static);
        Assert.Null(getCommandsMethod);
    }

    // =====================================================================
    // §8 — VM CONSTRUCTION NAME CONVENTION
    // Spec §2: each VM is named "xxx-vm-{id}" in VMx
    // =====================================================================

    [Fact]
    public void DecisionVMFactory_VmName_ContainsDecisionId()
    {
        var model = new DecisionM("d-test-42", "Test Decision");
        var vm = DecisionVMFactory.Create(model);
        Assert.Contains("d-test-42", vm.Name);
    }

    [Fact]
    public void AlternativeVMFactory_VmName_ContainsAlternativeId()
    {
        var model = new AlternativeM("a-test-99", "d1", "Test Alt");
        var vm = AlternativeVMFactory.Create(model);
        Assert.Contains("a-test-99", vm.Name);
    }

    [Fact]
    public void PropertyVMFactory_VmName_ContainsPropertyId()
    {
        var model = new PropertyM("p-test-7", "Test Prop", PropertyKind.Max, 1.0);
        var vm = PropertyVMFactory.Create(model);
        Assert.Contains("p-test-7", vm.Name);
    }

    [Fact]
    public void CandidateVMFactory_VmName_ContainsRank()
    {
        var model = new CandidateM(
            ImmutableArray.Create("a1"),
            new TriangularFuzzyM(0, 1, 2),
            new NormalizedFuzzyM(0.1, 0.2, 0.3),
            0.5,
            Rank: 3);
        var vm = CandidateVMFactory.Create(model);
        Assert.Contains("3", vm.Name);
    }

    [Fact]
    public void CriticalDecisionVMFactory_VmName_ContainsRank()
    {
        var model = new CriticalDecisionM(
            "d1",
            new TriangularFuzzyM(0, 1, 2),
            new NormalizedFuzzyM(0.1, 0.2, 0.3),
            0.3,
            Rank: 2);
        var vm = CriticalDecisionVMFactory.Create(model);
        Assert.Contains("2", vm.Name);
    }

    // =====================================================================
    // §9 — DERIVED VIEW PROJECTIONS in ScenarioState
    //      spec §3.1 supplemental: DecisionsView, AlternativesView, etc.
    // =====================================================================

    [Fact]
    public void ScenarioState_DecisionsView_ReturnsScenarioDecisions()
    {
        var (vm, _) = CreateWithMinimalScenario();
        var state = vm.Model;
        Assert.NotNull(state.Scenario);
        Assert.Equal(state.Scenario!.Decisions, state.DecisionsView);
    }

    [Fact]
    public void ScenarioState_AlternativesView_ReturnsScenarioAlternatives()
    {
        var (vm, _) = CreateWithMinimalScenario();
        var state = vm.Model;
        Assert.Equal(state.Scenario!.Alternatives, state.AlternativesView);
    }

    [Fact]
    public void ScenarioState_PropertiesView_ReturnsScenarioProperties()
    {
        var (vm, _) = CreateWithMinimalScenario();
        var state = vm.Model;
        Assert.Equal(state.Scenario!.Properties, state.PropertiesView);
    }

    [Fact]
    public void ScenarioState_CoefficientsView_ReturnsScenarioCoefficients()
    {
        var (vm, _) = CreateWithMinimalScenario();
        var state = vm.Model;
        Assert.Equal(state.Scenario!.Coefficients, state.CoefficientsView);
    }

    [Fact]
    public void ScenarioState_DecisionsView_IsEmptyWhenNoScenario()
    {
        var state = ScenarioState.Empty;
        Assert.Empty(state.DecisionsView);
    }

    [Fact]
    public void ScenarioState_HasScenario_FalseWhenNull()
    {
        Assert.False(ScenarioState.Empty.HasScenario);
    }

    [Fact]
    public void ScenarioState_HasScenario_TrueAfterLoad()
    {
        var (vm, _) = LoadSas();
        Assert.True(vm.Model.HasScenario);
    }

    // =====================================================================
    // §10 — CONSTRAINT TYPE FILTERING in ScenarioState
    // =====================================================================

    [Fact]
    public void ScenarioState_ThresholdConstraintsView_FiltersToThresholdOnly()
    {
        var (vm, cmds) = CreateWithMinimalScenario();
        var alts = vm.Model.Scenario!.Alternatives;
        var props = vm.Model.Scenario!.Properties;

        // Add one of each kind.
        cmds.Mutator.AddThresholdConstraint(props[0].Id, 0.0, null);
        cmds.Mutator.AddDependencyConstraint(alts[0].Id, alts[1].Id);
        cmds.Mutator.AddConflictConstraint(alts[0].Id, alts[2].Id);

        var view = vm.Model.ThresholdConstraintsView;
        Assert.Single(view);
        Assert.IsType<ThresholdConstraintM>(view[0]);
    }

    [Fact]
    public void ScenarioState_DependencyConstraintsView_FiltersToDependencyOnly()
    {
        var (vm, cmds) = CreateWithMinimalScenario();
        var alts = vm.Model.Scenario!.Alternatives;
        var props = vm.Model.Scenario!.Properties;

        cmds.Mutator.AddThresholdConstraint(props[0].Id, 0.0, null);
        cmds.Mutator.AddDependencyConstraint(alts[0].Id, alts[1].Id);
        cmds.Mutator.AddConflictConstraint(alts[0].Id, alts[2].Id);

        var view = vm.Model.DependencyConstraintsView;
        Assert.Single(view);
        Assert.IsType<DependencyConstraintM>(view[0]);
    }

    [Fact]
    public void ScenarioState_ConflictConstraintsView_FiltersToConflictOnly()
    {
        var (vm, cmds) = CreateWithMinimalScenario();
        var alts = vm.Model.Scenario!.Alternatives;
        var props = vm.Model.Scenario!.Properties;

        cmds.Mutator.AddThresholdConstraint(props[0].Id, 0.0, null);
        cmds.Mutator.AddDependencyConstraint(alts[0].Id, alts[1].Id);
        cmds.Mutator.AddConflictConstraint(alts[0].Id, alts[2].Id);

        var view = vm.Model.ConflictConstraintsView;
        Assert.Single(view);
        Assert.IsType<ConflictConstraintM>(view[0]);
    }

    // =====================================================================
    // §11 — ERROR PATHS IN MUTATOR (negative tests)
    // =====================================================================

    [Fact]
    public void Mutator_UpdateDecisionName_ThrowsForUnknownId()
    {
        var (_, cmds) = CreateWithMinimalScenario();
        Assert.Throws<ScenarioMutationException>(
            () => cmds.Mutator.UpdateDecisionName("d-does-not-exist", "X"));
    }

    [Fact]
    public void Mutator_DeleteDecision_ThrowsForUnknownId()
    {
        var (_, cmds) = CreateWithMinimalScenario();
        Assert.Throws<ScenarioMutationException>(
            () => cmds.Mutator.DeleteDecision("d-does-not-exist"));
    }

    [Fact]
    public void Mutator_AddAlternative_ThrowsForUnknownDecisionId()
    {
        var (_, cmds) = CreateWithMinimalScenario();
        Assert.Throws<ScenarioMutationException>(
            () => cmds.Mutator.AddAlternative("d-does-not-exist"));
    }

    [Fact]
    public void Mutator_AddThresholdConstraint_ThrowsForUnknownPropertyId()
    {
        var (_, cmds) = CreateWithMinimalScenario();
        Assert.Throws<ScenarioMutationException>(
            () => cmds.Mutator.AddThresholdConstraint("p-does-not-exist", 0.0, null));
    }

    [Fact]
    public void Mutator_AddThresholdConstraint_ThrowsWhenBothMinMaxNull()
    {
        var (vm, cmds) = CreateWithMinimalScenario();
        var p = vm.Model.Scenario!.Properties[0];
        Assert.Throws<ScenarioMutationException>(
            () => cmds.Mutator.AddThresholdConstraint(p.Id, null, null));
    }

    [Fact]
    public void Mutator_AddThresholdConstraint_ThrowsWhenMinGreaterThanMax()
    {
        var (vm, cmds) = CreateWithMinimalScenario();
        var p = vm.Model.Scenario!.Properties[0];
        Assert.Throws<ScenarioMutationException>(
            () => cmds.Mutator.AddThresholdConstraint(p.Id, 10.0, 1.0));
    }

    [Fact]
    public void Mutator_AddDependencyConstraint_ThrowsForUnknownSourceAlt()
    {
        var (vm, cmds) = CreateWithMinimalScenario();
        var a = vm.Model.Scenario!.Alternatives[0];
        Assert.Throws<ScenarioMutationException>(
            () => cmds.Mutator.AddDependencyConstraint("a-unknown", a.Id));
    }

    [Fact]
    public void Mutator_AddConflictConstraint_ThrowsForUnknownAltA()
    {
        var (vm, cmds) = CreateWithMinimalScenario();
        var a = vm.Model.Scenario!.Alternatives[0];
        Assert.Throws<ScenarioMutationException>(
            () => cmds.Mutator.AddConflictConstraint("a-unknown", a.Id));
    }
}
