using GuideArch.Models;
using GuideArch.ViewModels;
using VMx.Components;
using Xunit;

namespace GuideArch.ViewModels.Tests;

/// <summary>
/// Tests for M3 delete-cascade rules (spec/editors.md §2).
/// Each test loads sas.json, performs a delete mutation and verifies that all
/// dependent entities are removed and the scenario remains internally consistent.
/// </summary>
public class EditorCascadesTests
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

    private static (ComponentVM<ScenarioState> vm, ScenarioMutator mutator) LoadSas()
    {
        var vm = ScenarioVMFactory.Create();
        var cmds = ScenarioVMFactory.GetCommands(vm);
        cmds.OpenCmd.Execute(FindSasJsonPath());
        Assert.NotNull(vm.Model.Scenario);
        return (vm, cmds.Mutator);
    }

    // -----------------------------------------------------------------------
    // Decision cascade (spec editors.md §2.1)
    // -----------------------------------------------------------------------

    [Fact]
    public void DeleteDecision_RemovesDecision_And_ItsAlternatives()
    {
        var (vm, mutator) = LoadSas();
        var scenario = vm.Model.Scenario!;

        // Pick the first decision.
        var decision = scenario.Decisions[0];
        var altsBefore = scenario.Alternatives.Where(a => a.DecisionId == decision.Id).ToList();
        Assert.NotEmpty(altsBefore); // sas.json has alternatives for every decision

        mutator.DeleteDecision(decision.Id);

        var s = vm.Model.Scenario!;
        Assert.DoesNotContain(s.Decisions, d => d.Id == decision.Id);
        Assert.DoesNotContain(s.Alternatives, a => a.DecisionId == decision.Id);
    }

    [Fact]
    public void DeleteDecision_RemovesCoefficients_ForDeletedAlternatives()
    {
        var (vm, mutator) = LoadSas();
        var scenario = vm.Model.Scenario!;

        var decision = scenario.Decisions[0];
        var altIds = scenario.Alternatives
            .Where(a => a.DecisionId == decision.Id)
            .Select(a => a.Id)
            .ToHashSet();

        mutator.DeleteDecision(decision.Id);

        var s = vm.Model.Scenario!;
        Assert.DoesNotContain(s.Coefficients, c => altIds.Contains(c.AlternativeId));
    }

    [Fact]
    public void DeleteDecision_RemovesDependencyConstraints_ReferencingDeletedAlternatives()
    {
        var (vm, mutator) = LoadSas();
        var scenario = vm.Model.Scenario!;

        // Find a decision whose alternatives are referenced by any dependency constraint.
        string? decisionId = null;
        foreach (var decision in scenario.Decisions)
        {
            var altIds = scenario.Alternatives
                .Where(a => a.DecisionId == decision.Id)
                .Select(a => a.Id)
                .ToHashSet();

            bool hasRef = scenario.Constraints.OfType<DependencyConstraintM>()
                .Any(dep => altIds.Contains(dep.SourceAlternativeId)
                         || altIds.Contains(dep.TargetAlternativeId));
            if (hasRef)
            {
                decisionId = decision.Id;
                break;
            }
        }

        if (decisionId is null)
        {
            // sas.json has no dependency constraints referencing a decision's alts;
            // we still verify a delete without such refs works cleanly.
            decisionId = scenario.Decisions[0].Id;
        }

        var altIds2 = scenario.Alternatives
            .Where(a => a.DecisionId == decisionId)
            .Select(a => a.Id)
            .ToHashSet();

        mutator.DeleteDecision(decisionId!);

        var s = vm.Model.Scenario!;
        Assert.DoesNotContain(s.Constraints.OfType<DependencyConstraintM>(),
            dep => altIds2.Contains(dep.SourceAlternativeId)
                || altIds2.Contains(dep.TargetAlternativeId));
        Assert.DoesNotContain(s.Constraints.OfType<ConflictConstraintM>(),
            conf => altIds2.Contains(conf.AlternativeAId)
                 || altIds2.Contains(conf.AlternativeBId));
    }

    [Fact]
    public void DeleteDecision_ScenarioRemainsConsistent()
    {
        var (vm, mutator) = LoadSas();
        var scenario = vm.Model.Scenario!;

        var decision = scenario.Decisions[0];
        mutator.DeleteDecision(decision.Id);

        var s = vm.Model.Scenario!;

        // Cross-reference validity: every alternative's decisionId exists.
        var decisionIds = s.Decisions.Select(d => d.Id).ToHashSet();
        Assert.All(s.Alternatives, a => Assert.Contains(a.DecisionId, decisionIds));

        // Cross-reference validity: every coefficient refs valid alt + prop.
        var altIds = s.Alternatives.Select(a => a.Id).ToHashSet();
        var propIds = s.Properties.Select(p => p.Id).ToHashSet();
        Assert.All(s.Coefficients, c =>
        {
            Assert.Contains(c.AlternativeId, altIds);
            Assert.Contains(c.PropertyId, propIds);
        });
    }

    // -----------------------------------------------------------------------
    // Alternative cascade (spec editors.md §2.2)
    // -----------------------------------------------------------------------

    [Fact]
    public void DeleteAlternative_RemovesAlternative_And_ItsCoefficients()
    {
        var (vm, mutator) = LoadSas();
        var scenario = vm.Model.Scenario!;

        var alt = scenario.Alternatives[0];
        var coeffsBefore = scenario.Coefficients.Where(c => c.AlternativeId == alt.Id).Count();
        Assert.True(coeffsBefore > 0);

        mutator.DeleteAlternative(alt.Id);

        var s = vm.Model.Scenario!;
        Assert.DoesNotContain(s.Alternatives, a => a.Id == alt.Id);
        Assert.DoesNotContain(s.Coefficients, c => c.AlternativeId == alt.Id);
    }

    [Fact]
    public void DeleteAlternative_RemovesConstraints_ReferencingIt()
    {
        var (vm, mutator) = LoadSas();
        var scenario = vm.Model.Scenario!;

        // Find an alternative that appears in any constraint.
        AlternativeM? targetAlt = null;
        foreach (var alt in scenario.Alternatives)
        {
            bool inConstraint = scenario.Constraints.Any(c => c switch
            {
                DependencyConstraintM dep =>
                    dep.SourceAlternativeId == alt.Id || dep.TargetAlternativeId == alt.Id,
                ConflictConstraintM conf =>
                    conf.AlternativeAId == alt.Id || conf.AlternativeBId == alt.Id,
                _ => false
            });
            if (inConstraint)
            {
                targetAlt = alt;
                break;
            }
        }

        if (targetAlt is null)
        {
            // No constraints in sas.json reference alternatives; still verify clean delete.
            targetAlt = scenario.Alternatives[0];
        }

        mutator.DeleteAlternative(targetAlt.Id);

        var s = vm.Model.Scenario!;
        Assert.DoesNotContain(s.Constraints.OfType<DependencyConstraintM>(),
            dep => dep.SourceAlternativeId == targetAlt.Id
                || dep.TargetAlternativeId == targetAlt.Id);
        Assert.DoesNotContain(s.Constraints.OfType<ConflictConstraintM>(),
            conf => conf.AlternativeAId == targetAlt.Id
                 || conf.AlternativeBId == targetAlt.Id);
    }

    // -----------------------------------------------------------------------
    // Property cascade (spec editors.md §2.3)
    // -----------------------------------------------------------------------

    [Fact]
    public void DeleteProperty_RemovesProperty_And_ItsCoefficients()
    {
        var (vm, mutator) = LoadSas();
        var scenario = vm.Model.Scenario!;

        var prop = scenario.Properties[0];
        var coeffsBefore = scenario.Coefficients.Count(c => c.PropertyId == prop.Id);
        Assert.True(coeffsBefore > 0);

        mutator.DeleteProperty(prop.Id);

        var s = vm.Model.Scenario!;
        Assert.DoesNotContain(s.Properties, p => p.Id == prop.Id);
        Assert.DoesNotContain(s.Coefficients, c => c.PropertyId == prop.Id);
    }

    [Fact]
    public void DeleteProperty_RemovesThresholdConstraints_ReferencingIt()
    {
        var (vm, mutator) = LoadSas();
        var scenario = vm.Model.Scenario!;

        // Find a property that has a threshold constraint.
        PropertyM? targetProp = null;
        foreach (var prop in scenario.Properties)
        {
            if (scenario.Constraints.OfType<ThresholdConstraintM>()
                .Any(t => t.PropertyId == prop.Id))
            {
                targetProp = prop;
                break;
            }
        }

        if (targetProp is null)
        {
            // No threshold constraints in sas.json for any property; still verify clean delete.
            targetProp = scenario.Properties[0];
        }

        mutator.DeleteProperty(targetProp.Id);

        var s = vm.Model.Scenario!;
        Assert.DoesNotContain(s.Constraints.OfType<ThresholdConstraintM>(),
            t => t.PropertyId == targetProp.Id);
    }

    [Fact]
    public void DeleteProperty_ScenarioRemainsConsistent()
    {
        var (vm, mutator) = LoadSas();
        var scenario = vm.Model.Scenario!;

        var prop = scenario.Properties[0];
        mutator.DeleteProperty(prop.Id);

        var s = vm.Model.Scenario!;

        var propIds = s.Properties.Select(p => p.Id).ToHashSet();
        var altIds = s.Alternatives.Select(a => a.Id).ToHashSet();
        Assert.All(s.Coefficients, c =>
        {
            Assert.Contains(c.AlternativeId, altIds);
            Assert.Contains(c.PropertyId, propIds);
        });
    }

    // -----------------------------------------------------------------------
    // Edge cases
    // -----------------------------------------------------------------------

    [Fact]
    public void DeleteDecision_ThrowsForUnknownId()
    {
        var (vm, mutator) = LoadSas();
        Assert.Throws<ScenarioMutationException>(() => mutator.DeleteDecision("d-does-not-exist"));
    }

    [Fact]
    public void DeleteAlternative_ThrowsForUnknownId()
    {
        var (vm, mutator) = LoadSas();
        Assert.Throws<ScenarioMutationException>(() => mutator.DeleteAlternative("a-does-not-exist"));
    }

    [Fact]
    public void DeleteProperty_ThrowsForUnknownId()
    {
        var (vm, mutator) = LoadSas();
        Assert.Throws<ScenarioMutationException>(() => mutator.DeleteProperty("p-does-not-exist"));
    }

    // -----------------------------------------------------------------------
    // Add cascades (spec editors.md §2.2, §2.3) — parity with Python+TS tests
    // -----------------------------------------------------------------------

    [Fact]
    public void AddAlternative_CreatesZeroFuzzyCoefficients_ForEveryProperty()
    {
        var (vm, mutator) = LoadSas();
        var scenario = vm.Model.Scenario!;
        var decisionId = scenario.Decisions[0].Id;
        var propIds = scenario.Properties.Select(p => p.Id).ToHashSet();

        mutator.AddAlternative(decisionId);

        var s = vm.Model.Scenario!;
        // The new alternative is the last one whose decisionId matches.
        var newAlt = s.Alternatives.Last(a => a.DecisionId == decisionId);
        var newCoeffs = s.Coefficients.Where(c => c.AlternativeId == newAlt.Id).ToList();

        Assert.Equal(propIds.Count, newCoeffs.Count);
        Assert.All(newCoeffs, c =>
        {
            Assert.Contains(c.PropertyId, propIds);
            Assert.Equal(0.0, c.Value.Lower);
            Assert.Equal(0.0, c.Value.Modal);
            Assert.Equal(0.0, c.Value.Upper);
        });
    }

    [Fact]
    public void AddProperty_CreatesZeroFuzzyCoefficients_ForEveryAlternative()
    {
        var (vm, mutator) = LoadSas();
        var scenario = vm.Model.Scenario!;
        var altIds = scenario.Alternatives.Select(a => a.Id).ToHashSet();

        mutator.AddProperty();

        var s = vm.Model.Scenario!;
        var newProp = s.Properties[^1];
        var newCoeffs = s.Coefficients.Where(c => c.PropertyId == newProp.Id).ToList();

        Assert.Equal(altIds.Count, newCoeffs.Count);
        Assert.All(newCoeffs, c =>
        {
            Assert.Contains(c.AlternativeId, altIds);
            Assert.Equal(0.0, c.Value.Lower);
            Assert.Equal(0.0, c.Value.Modal);
            Assert.Equal(0.0, c.Value.Upper);
        });
    }

    [Fact]
    public void UpdateProperty_ThrowsForNonPositiveWeight()
    {
        var (vm, mutator) = LoadSas();
        var scenario = vm.Model.Scenario!;
        var propId = scenario.Properties[0].Id;

        Assert.Throws<ScenarioMutationException>(
            () => mutator.UpdateProperty(propId, name: null, kind: null, weight: 0.0));
        Assert.Throws<ScenarioMutationException>(
            () => mutator.UpdateProperty(propId, name: null, kind: null, weight: -1.0));
    }

    [Fact]
    public void AddProperty_ThrowsForNonPositiveWeight()
    {
        var (_, mutator) = LoadSas();

        Assert.Throws<ScenarioMutationException>(
            () => mutator.AddProperty(name: null, kind: null, weight: 0.0));
        Assert.Throws<ScenarioMutationException>(
            () => mutator.AddProperty(name: null, kind: null, weight: -1.0));
    }

    [Fact]
    public void DeleteDecision_ScenarioValidatesAgainstSchema()
    {
        // spec/editors.md §6: after a delete cascade the scenario must still
        // validate against the JSON schema. Save+reload runs the schema-
        // validating loader, so a clean reload proves schema validity (not
        // just the manual cross-reference checks above).
        var vm = ScenarioVMFactory.Create();
        var cmds = ScenarioVMFactory.GetCommands(vm);
        cmds.OpenCmd.Execute(FindSasJsonPath());
        var decision = vm.Model.Scenario!.Decisions[0];

        cmds.Mutator.DeleteDecision(decision.Id);

        var tempPath = Path.GetTempFileName() + ".json";
        try
        {
            cmds.SaveAsCmd.Execute(tempPath);

            var vm2 = ScenarioVMFactory.Create();
            var cmds2 = ScenarioVMFactory.GetCommands(vm2);
            cmds2.OpenCmd.Execute(tempPath);

            // OpenCmd leaves Scenario null if the loader rejects the file
            // (schema or invariant violation); a populated scenario == valid.
            Assert.NotNull(vm2.Model.Scenario);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }
}
