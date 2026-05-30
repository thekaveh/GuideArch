using System.Reflection;
using GuideArch.Models;
using GuideArch.ViewModels;
using VMx.Components;
using Xunit;

namespace GuideArch.ViewModels.Tests;

/// <summary>
/// Structural conformance tests: each factory exists and produces a VM with
/// the expected property names / commands (spec/viewmodels.md §7).
/// </summary>
public class VMTreeTests
{
    // -----------------------------------------------------------------------
    // ScenarioVMFactory
    // -----------------------------------------------------------------------

    [Fact]
    public void ScenarioVMFactory_Exists_And_Creates_VM()
    {
        var vm = ScenarioVMFactory.Create();
        Assert.NotNull(vm);
        Assert.IsAssignableFrom<ComponentVM<ScenarioState>>(vm);
    }

    [Fact]
    public void ScenarioVMFactory_Commands_AllPresent()
    {
        var vm = ScenarioVMFactory.Create();
        var cmds = ScenarioVMFactory.GetCommands(vm);

        Assert.NotNull(cmds.NewCmd);
        Assert.NotNull(cmds.OpenCmd);
        Assert.NotNull(cmds.SaveCmd);
        Assert.NotNull(cmds.SaveAsCmd);
        Assert.NotNull(cmds.SolveCmd);
    }

    [Theory]
    [InlineData("Scenario")]
    [InlineData("FilePath")]
    [InlineData("IsDirty")]
    [InlineData("Candidates")]
    [InlineData("CriticalDecisions")]
    [InlineData("CriticalConstraints")]
    [InlineData("Status")]
    [InlineData("Warnings")]
    public void ScenarioState_Has_ExpectedProperty(string propertyName)
    {
        var prop = typeof(ScenarioState).GetProperty(propertyName,
            BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(prop);
    }

    // -----------------------------------------------------------------------
    // DecisionVMFactory
    // -----------------------------------------------------------------------

    [Fact]
    public void DecisionVMFactory_Exists_And_Creates_VM()
    {
        var model = new DecisionM("d1", "Decision One");
        var vm = DecisionVMFactory.Create(model);
        Assert.NotNull(vm);
        Assert.IsAssignableFrom<ComponentVM<DecisionM>>(vm);
        Assert.Equal(model, vm.Model);
    }

    // -----------------------------------------------------------------------
    // AlternativeVMFactory
    // -----------------------------------------------------------------------

    [Fact]
    public void AlternativeVMFactory_Exists_And_Creates_VM()
    {
        var model = new AlternativeM("a1", "d1", "Alt One");
        var vm = AlternativeVMFactory.Create(model);
        Assert.NotNull(vm);
        Assert.IsAssignableFrom<ComponentVM<AlternativeM>>(vm);
        Assert.Equal(model, vm.Model);
    }

    // -----------------------------------------------------------------------
    // PropertyVMFactory
    // -----------------------------------------------------------------------

    [Fact]
    public void PropertyVMFactory_Exists_And_Creates_VM()
    {
        var model = new PropertyM("p1", "Cost", PropertyKind.Min, 0.5);
        var vm = PropertyVMFactory.Create(model);
        Assert.NotNull(vm);
        Assert.IsAssignableFrom<ComponentVM<PropertyM>>(vm);
        Assert.Equal(model, vm.Model);
    }

    // -----------------------------------------------------------------------
    // ConstraintVMFactory
    // -----------------------------------------------------------------------

    [Fact]
    public void ConstraintVMFactory_Exists_And_Creates_VM()
    {
        ConstraintM model = new ThresholdConstraintM("p1", null, 100.0);
        var vm = ConstraintVMFactory.Create(model, 0);
        Assert.NotNull(vm);
        Assert.IsAssignableFrom<ComponentVM<ConstraintM>>(vm);
        Assert.Equal(model, vm.Model);
    }

    // -----------------------------------------------------------------------
    // CoefficientVMFactory
    // -----------------------------------------------------------------------

    [Fact]
    public void CoefficientVMFactory_Exists_And_Creates_VM()
    {
        var model = new CoefficientM("a1", "p1", new TriangularFuzzyM(1.0, 2.0, 3.0));
        var vm = CoefficientVMFactory.Create(model);
        Assert.NotNull(vm);
        Assert.IsAssignableFrom<ComponentVM<CoefficientM>>(vm);
        Assert.Equal(model, vm.Model);
    }

    // -----------------------------------------------------------------------
    // CandidateVMFactory
    // -----------------------------------------------------------------------

    [Fact]
    public void CandidateVMFactory_Exists_And_Creates_VM()
    {
        var model = new CandidateM(
            AlternativeIds: System.Collections.Immutable.ImmutableArray.Create("a1"),
            TriangularValue: new TriangularFuzzyM(0, 1, 2),
            NormalizedValue: new NormalizedFuzzyM(0.1, 0.2, 0.3),
            Score: 0.5,
            Rank: 0);
        var vm = CandidateVMFactory.Create(model);
        Assert.NotNull(vm);
        Assert.IsAssignableFrom<ComponentVM<CandidateM>>(vm);
        Assert.Equal(model, vm.Model);
    }

    // -----------------------------------------------------------------------
    // CriticalDecisionVMFactory
    // -----------------------------------------------------------------------

    [Fact]
    public void CriticalDecisionVMFactory_Exists_And_Creates_VM()
    {
        var model = new CriticalDecisionM(
            DecisionId: "d1",
            TriangularValue: new TriangularFuzzyM(0, 1, 2),
            NormalizedValue: new NormalizedFuzzyM(0.1, 0.2, 0.3),
            Score: 0.3,
            Rank: 0);
        var vm = CriticalDecisionVMFactory.Create(model);
        Assert.NotNull(vm);
        Assert.IsAssignableFrom<ComponentVM<CriticalDecisionM>>(vm);
        Assert.Equal(model, vm.Model);
    }

    // -----------------------------------------------------------------------
    // CriticalConstraintVMFactory
    // -----------------------------------------------------------------------

    [Fact]
    public void CriticalConstraintVMFactory_Exists_And_Creates_VM()
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
    }
}
