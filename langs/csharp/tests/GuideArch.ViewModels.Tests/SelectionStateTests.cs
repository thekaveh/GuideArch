using System.Collections.Immutable;
using GuideArch.Models;
using GuideArch.ViewModels;
using VMx.Components;
using Xunit;

namespace GuideArch.ViewModels.Tests;

/// <summary>
/// Tests for SelectedCandidateIndex propagation (spec charts.md §6).
/// Verifies that the field defaults to 0 after a solve with non-empty candidates,
/// that SelectCandidate mutations propagate to the VM state, and that
/// selection clears when there are no candidates.
/// </summary>
public class SelectionStateTests
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
        Assert.True(vm.Model.Candidates.Length > 0);
        return (vm, cmds);
    }

    // -----------------------------------------------------------------------
    // Default selection after first solve
    // -----------------------------------------------------------------------

    [Fact]
    public void SelectedCandidateIndex_IsNull_BeforeLoad()
    {
        var vm = ScenarioVMFactory.Create();
        Assert.Null(vm.Model.SelectedCandidateIndex);
    }

    [Fact]
    public void SelectedCandidateIndex_DefaultsToZero_AfterFirstSolveWithCandidates()
    {
        var (vm, _) = LoadSas();

        // After loading sas.json (which has candidates), index must default to 0.
        Assert.Equal(0, vm.Model.SelectedCandidateIndex);
    }

    [Fact]
    public void SelectedCandidateIndex_IsNull_WhenNoCandidatesAfterNew()
    {
        var (vm, cmds) = LoadSas();
        Assert.Equal(0, vm.Model.SelectedCandidateIndex);

        cmds.NewCmd.Execute(null);

        // After New, candidates are empty and selection should be null.
        Assert.Empty(vm.Model.Candidates);
        Assert.Null(vm.Model.SelectedCandidateIndex);
    }

    // -----------------------------------------------------------------------
    // SelectCandidate mutation
    // -----------------------------------------------------------------------

    [Fact]
    public void SelectCandidate_SetsIndex_InState()
    {
        var (vm, cmds) = LoadSas();
        var mutator = cmds.Mutator;

        int candidateCount = vm.Model.Candidates.Length;
        Assert.True(candidateCount > 1, "sas.json should have more than one candidate");

        // Select rank 1.
        mutator.SelectCandidate(1);
        Assert.Equal(1, vm.Model.SelectedCandidateIndex);
    }

    [Fact]
    public void SelectCandidate_Null_Clears_Selection()
    {
        var (vm, cmds) = LoadSas();
        var mutator = cmds.Mutator;

        mutator.SelectCandidate(1);
        Assert.Equal(1, vm.Model.SelectedCandidateIndex);

        mutator.SelectCandidate(null);
        Assert.Null(vm.Model.SelectedCandidateIndex);
    }

    [Fact]
    public void SelectCandidate_ThrowsForNegativeIndex()
    {
        var (vm, cmds) = LoadSas();
        Assert.Throws<ScenarioMutationException>(() => cmds.Mutator.SelectCandidate(-1));
    }

    [Fact]
    public void SelectCandidate_ThrowsForIndexOutOfRange()
    {
        var (vm, cmds) = LoadSas();
        int outOfRange = vm.Model.Candidates.Length; // Length is one past the last valid index.
        Assert.Throws<ScenarioMutationException>(() => cmds.Mutator.SelectCandidate(outOfRange));
    }

    [Fact]
    public void SelectCandidate_DoesNotTriggerReSolve()
    {
        var (vm, cmds) = LoadSas();
        var mutator = cmds.Mutator;

        // Record initial candidates.
        var candidatesBefore = vm.Model.Candidates;

        mutator.SelectCandidate(1);

        // Candidates array should be identical (no re-solve occurred).
        Assert.Equal(candidatesBefore, vm.Model.Candidates);
    }

    // -----------------------------------------------------------------------
    // Selection propagates through observers
    // -----------------------------------------------------------------------

    [Fact]
    public void SelectionChange_PropagatesViaPropertyChanged()
    {
        var (vm, cmds) = LoadSas();
        var mutator = cmds.Mutator;

        int? capturedIndex = -1;
        bool eventFired = false;

        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ComponentVM<ScenarioState>.Model))
            {
                capturedIndex = vm.Model.SelectedCandidateIndex;
                eventFired = true;
            }
        };

        mutator.SelectCandidate(2);

        Assert.True(eventFired, "PropertyChanged should fire when selection changes");
        Assert.Equal(2, capturedIndex);
    }

    // -----------------------------------------------------------------------
    // Selection preserved across re-solve if still valid
    // -----------------------------------------------------------------------

    [Fact]
    public void SelectedCandidateIndex_PreservedAfterReSolve_WhenInRange()
    {
        var (vm, cmds) = LoadSas();
        var mutator = cmds.Mutator;

        mutator.SelectCandidate(1);
        Assert.Equal(1, vm.Model.SelectedCandidateIndex);

        // Re-solve should preserve index = 1 if still in range.
        cmds.SolveCmd.Execute(null);

        Assert.Equal(1, vm.Model.SelectedCandidateIndex);
    }
}
