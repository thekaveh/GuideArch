using System.Collections.Immutable;
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
/// Also exposes M3 mutation methods for in-place editing.
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

            // Default SelectedCandidateIndex to 0 whenever candidates become non-empty;
            // preserve existing selection if still in range (spec charts.md §6).
            int? newSelIdx = null;
            if (candidates.Length > 0)
            {
                int prev = state.SelectedCandidateIndex ?? -1;
                newSelIdx = (prev >= 0 && prev < candidates.Length) ? prev : 0;
            }

            SetState(state with
            {
                Candidates = candidates,
                CriticalDecisions = critDec,
                CriticalConstraints = critCon,
                Status = $"Solved: {candidates.Length} candidates",
                SelectedCandidateIndex = newSelIdx
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
                    Status = "New scenario — nothing to solve."
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
                    });
                    Solve();
                }
                catch (Exception ex)
                {
                    var msg = $"Open failed: {ex.Message}";
                    SetState(state with
                    {
                        Status = msg,
                        Warnings = state.Warnings.Add(msg)
                    });
                }
            })
            .Build();

        // -----------------------------------------------------------------------
        // SaveCmd / SaveAsCmd — spec §3.2
        //
        // Spec says: SaveAsCmd "sets FilePath = path then SaveCmd()". A previous
        // implementation duplicated the write-and-clear-dirty logic in SaveAs,
        // bypassing the SaveCmd predicate (so a SaveAs would happen even when
        // SaveCmd was disabled). Now both share a single DoSave helper that
        // performs the write and clears the dirty flag.
        // -----------------------------------------------------------------------
        void DoSave(string path)
        {
            if (state.Scenario is null) return;
            try
            {
                WriteScenario(state.Scenario, path);
            }
            catch (IOException ex)
            {
                var msg = $"Save failed: {ex.Message}";
                SetState(state with
                {
                    Status = msg,
                    Warnings = state.Warnings.Add(msg)
                });
                return;
            }
            catch (UnauthorizedAccessException ex)
            {
                var msg = $"Save failed: {ex.Message}";
                SetState(state with
                {
                    Status = msg,
                    Warnings = state.Warnings.Add(msg)
                });
                return;
            }
            SetState(state with { FilePath = path, IsDirty = false });
        }

        ICommand saveCmd = RelayCommand.Builder()
            .Predicate(() => state.FilePath is not null && state.Scenario is not null)
            .Task(() =>
            {
                if (state.FilePath is null) return;
                DoSave(state.FilePath);
            })
            .Build();

        ICommand saveAsCmd = RelayCommand<string>.Builder()
            .Task(DoSave)
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

        // -----------------------------------------------------------------------
        // M3 Mutation helpers — capture SetState/Solve via closure
        // -----------------------------------------------------------------------
        ScenarioMutator mutator = new(
            getState: () => state,
            setState: SetState,
            solve: Solve);

        var cmdHolder = new ScenarioCommands(newCmd, openCmd, saveCmd, saveAsCmd, solveCmd, mutator);
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

    internal static void WriteScenario(ScenarioM scenario, string path)
    {
        // Use ScenarioSerializer to produce schema-compliant JSON (correct enum
        // string literals, correct field names for ConstraintM subclasses, etc.)
        // so that ScenarioLoader.Load() can round-trip the file.
        var json = ScenarioSerializer.Serialize(scenario);
        File.WriteAllText(path, json);
    }
}

// ---------------------------------------------------------------------------
// ScenarioMutator — M3 in-place editing helpers (spec editors.md §2)
// ---------------------------------------------------------------------------

/// <summary>
/// Encapsulates all M3 mutations on the scenario state. Called by the View
/// in response to user edits. All mutations validate invariants and either
/// throw <see cref="ScenarioMutationException"/> (fatal) or add to Warnings.
/// The mutator re-solves after each change.
/// </summary>
public sealed class ScenarioMutator
{
    private readonly Func<ScenarioState> _getState;
    private readonly Action<ScenarioState> _setState;
    private readonly Action _solve;

    internal ScenarioMutator(
        Func<ScenarioState> getState,
        Action<ScenarioState> setState,
        Action solve)
    {
        _getState = getState;
        _setState = setState;
        _solve = solve;
    }

    private ScenarioState State => _getState();

    /// <summary>
    /// Default empty scenario used when auto-creating on first add-X.
    /// </summary>
    private static readonly ScenarioM EmptyScenario = new(
        SchemaVersion: "1.0.0",
        Name: "New scenario",
        Description: "",
        Decisions: System.Collections.Immutable.ImmutableArray<DecisionM>.Empty,
        Alternatives: System.Collections.Immutable.ImmutableArray<AlternativeM>.Empty,
        Properties: System.Collections.Immutable.ImmutableArray<PropertyM>.Empty,
        Coefficients: System.Collections.Immutable.ImmutableArray<CoefficientM>.Empty,
        Constraints: System.Collections.Immutable.ImmutableArray<ConstraintM>.Empty,
        Config: new ConfigM(Aggregation.Max, new NormalizedFuzzyM(1.0 / 3.0, 1.0 / 3.0, 1.0 / 3.0)),
        Warnings: System.Collections.Immutable.ImmutableArray<string>.Empty
    );

    /// <summary>
    /// Returns the current scenario, auto-creating an empty one if null.
    /// Mirrors the Python fix: clicking Add Decision/Property before opening
    /// a scenario runs New automatically instead of throwing.
    /// </summary>
    private ScenarioM EnsureScenario()
    {
        if (State.Scenario is not null) return State.Scenario;
        _setState(State with
        {
            Scenario = EmptyScenario,
            IsDirty = true,
            Status = "New scenario — nothing to solve."
        });
        return _getState().Scenario!;
    }

    private ScenarioM RequireScenario()
    {
        if (State.Scenario is null)
            throw new ScenarioMutationException("No scenario is loaded.");
        return State.Scenario;
    }

    // ------------------------------------------------------------------
    // M4: Candidate selection (drives charts)
    // ------------------------------------------------------------------

    /// <summary>
    /// Sets the currently selected candidate index (spec charts.md §6).
    /// Pass null to deselect. Index must be within Candidates range when non-null.
    /// </summary>
    public void SelectCandidate(int? index)
    {
        var s = State;
        if (index.HasValue && (index.Value < 0 || index.Value >= s.Candidates.Length))
            throw new ScenarioMutationException(
                $"Candidate index {index.Value} out of range (0–{s.Candidates.Length - 1}).");
        _setState(s with { SelectedCandidateIndex = index });
        // Selection changes do NOT re-solve.
    }

    // ------------------------------------------------------------------
    // Scenario name / description
    // ------------------------------------------------------------------

    public void SetScenarioName(string name)
    {
        var s = RequireScenario();
        _setState(State with
        {
            Scenario = s with { Name = name },
            IsDirty = true
        });
        // Name change does NOT trigger solve (spec viewmodels.md §3.3).
    }

    // ------------------------------------------------------------------
    // Decisions
    // ------------------------------------------------------------------

    public void AddDecision()
    {
        var s = EnsureScenario();
        var id = $"d-{Guid.NewGuid()}";
        var newDecision = new DecisionM(id, "New decision");
        _setState(State with
        {
            Scenario = s with { Decisions = s.Decisions.Add(newDecision) },
            IsDirty = true
        });
        _solve();
    }

    public void RenameDecision(string id, string newName)
    {
        var s = RequireScenario();
        var idx = s.Decisions.IndexOf(s.Decisions.FirstOrDefault(d => d.Id == id)!);
        if (idx < 0) throw new ScenarioMutationException($"Decision '{id}' not found.");
        var updated = s.Decisions.SetItem(idx, s.Decisions[idx] with { Name = newName });
        _setState(State with
        {
            Scenario = s with { Decisions = updated },
            IsDirty = true
        });
        // Rename does NOT trigger solve (spec viewmodels.md §3.3).
    }

    /// <summary>
    /// Deletes a decision AND all its alternatives AND all coefficients/constraints
    /// referencing those alternatives (spec editors.md §2.1 cascade).
    /// </summary>
    public void DeleteDecision(string id)
    {
        var s = RequireScenario();
        if (!s.Decisions.Any(d => d.Id == id))
            throw new ScenarioMutationException($"Decision '{id}' not found.");

        // Collect all alternative IDs belonging to this decision.
        var altIds = s.Alternatives
            .Where(a => a.DecisionId == id)
            .Select(a => a.Id)
            .ToHashSet();

        var newDecisions = s.Decisions.Where(d => d.Id != id).ToImmutableArray();
        var newAlternatives = s.Alternatives.Where(a => a.DecisionId != id).ToImmutableArray();
        var newCoefficients = s.Coefficients
            .Where(c => !altIds.Contains(c.AlternativeId))
            .ToImmutableArray();
        var newConstraints = s.Constraints
            .Where(c => c switch
            {
                ThresholdConstraintM _ => true,
                DependencyConstraintM dep =>
                    !altIds.Contains(dep.SourceAlternativeId) &&
                    !altIds.Contains(dep.TargetAlternativeId),
                ConflictConstraintM conf =>
                    !altIds.Contains(conf.AlternativeAId) &&
                    !altIds.Contains(conf.AlternativeBId),
                _ => true
            })
            .ToImmutableArray();

        _setState(State with
        {
            Scenario = s with
            {
                Decisions = newDecisions,
                Alternatives = newAlternatives,
                Coefficients = newCoefficients,
                Constraints = newConstraints
            },
            IsDirty = true
        });
        _solve();
    }

    // ------------------------------------------------------------------
    // Alternatives
    // ------------------------------------------------------------------

    public void AddAlternative(string decisionId)
    {
        var s = RequireScenario();
        if (!s.Decisions.Any(d => d.Id == decisionId))
            throw new ScenarioMutationException($"Decision '{decisionId}' not found.");

        var id = $"a-{Guid.NewGuid()}";
        var newAlt = new AlternativeM(id, decisionId, "New alternative");

        // Add zero-fuzzy coefficients for every existing property.
        var newCoeffs = s.Properties
            .Select(p => new CoefficientM(id, p.Id, TriangularFuzzyM.Zero))
            .ToImmutableArray();

        _setState(State with
        {
            Scenario = s with
            {
                Alternatives = s.Alternatives.Add(newAlt),
                Coefficients = s.Coefficients.AddRange(newCoeffs)
            },
            IsDirty = true
        });
        _solve();
    }

    public void RenameAlternative(string id, string newName)
    {
        var s = RequireScenario();
        var alt = s.Alternatives.FirstOrDefault(a => a.Id == id);
        if (alt is null) throw new ScenarioMutationException($"Alternative '{id}' not found.");
        var idx = s.Alternatives.IndexOf(alt);
        _setState(State with
        {
            Scenario = s with { Alternatives = s.Alternatives.SetItem(idx, alt with { Name = newName }) },
            IsDirty = true
        });
        // Name change does NOT trigger solve.
    }

    /// <summary>
    /// Deletes an alternative AND its coefficients AND constraints referencing it
    /// (spec editors.md §2.2 cascade).
    /// </summary>
    public void DeleteAlternative(string id)
    {
        var s = RequireScenario();
        if (!s.Alternatives.Any(a => a.Id == id))
            throw new ScenarioMutationException($"Alternative '{id}' not found.");

        var newAlts = s.Alternatives.Where(a => a.Id != id).ToImmutableArray();
        var newCoeffs = s.Coefficients.Where(c => c.AlternativeId != id).ToImmutableArray();
        var newConstraints = s.Constraints
            .Where(c => c switch
            {
                ThresholdConstraintM _ => true,
                DependencyConstraintM dep =>
                    dep.SourceAlternativeId != id && dep.TargetAlternativeId != id,
                ConflictConstraintM conf =>
                    conf.AlternativeAId != id && conf.AlternativeBId != id,
                _ => true
            })
            .ToImmutableArray();

        _setState(State with
        {
            Scenario = s with
            {
                Alternatives = newAlts,
                Coefficients = newCoeffs,
                Constraints = newConstraints
            },
            IsDirty = true
        });
        _solve();
    }

    // ------------------------------------------------------------------
    // Properties
    // ------------------------------------------------------------------

    public void AddProperty()
    {
        var s = EnsureScenario();
        var id = $"p-{Guid.NewGuid()}";
        var newProp = new PropertyM(id, "New property", PropertyKind.Min, 1.0);

        // Add zero-fuzzy coefficients for every existing alternative.
        var newCoeffs = s.Alternatives
            .Select(a => new CoefficientM(a.Id, id, TriangularFuzzyM.Zero))
            .ToImmutableArray();

        _setState(State with
        {
            Scenario = s with
            {
                Properties = s.Properties.Add(newProp),
                Coefficients = s.Coefficients.AddRange(newCoeffs)
            },
            IsDirty = true
        });
        _solve();
    }

    public void UpdateProperty(string id, string? name, PropertyKind? kind, double? weight)
    {
        var s = RequireScenario();
        var prop = s.Properties.FirstOrDefault(p => p.Id == id);
        if (prop is null) throw new ScenarioMutationException($"Property '{id}' not found.");

        if (weight.HasValue && weight.Value <= 0)
            throw new ScenarioMutationException("Property weight must be > 0.");

        var idx = s.Properties.IndexOf(prop);
        var updated = prop with
        {
            Name = name ?? prop.Name,
            Kind = kind ?? prop.Kind,
            Weight = weight ?? prop.Weight
        };
        bool triggerSolve = (kind.HasValue && kind.Value != prop.Kind)
                         || (weight.HasValue && weight.Value != prop.Weight);

        _setState(State with
        {
            Scenario = s with { Properties = s.Properties.SetItem(idx, updated) },
            IsDirty = true
        });
        if (triggerSolve) _solve();
    }

    /// <summary>
    /// Deletes a property AND its coefficients AND threshold constraints referencing it
    /// (spec editors.md §2.3 cascade).
    /// </summary>
    public void DeleteProperty(string id)
    {
        var s = RequireScenario();
        if (!s.Properties.Any(p => p.Id == id))
            throw new ScenarioMutationException($"Property '{id}' not found.");

        var newProps = s.Properties.Where(p => p.Id != id).ToImmutableArray();
        var newCoeffs = s.Coefficients.Where(c => c.PropertyId != id).ToImmutableArray();
        var newConstraints = s.Constraints
            .Where(c => c is not ThresholdConstraintM t || t.PropertyId != id)
            .ToImmutableArray();

        _setState(State with
        {
            Scenario = s with
            {
                Properties = newProps,
                Coefficients = newCoeffs,
                Constraints = newConstraints
            },
            IsDirty = true
        });
        _solve();
    }

    // ------------------------------------------------------------------
    // Coefficients
    // ------------------------------------------------------------------

    public void UpdateCoefficient(string alternativeId, string propertyId,
        double lower, double modal, double upper)
    {
        var s = RequireScenario();
        var idx = -1;
        for (int i = 0; i < s.Coefficients.Length; i++)
        {
            if (s.Coefficients[i].AlternativeId == alternativeId &&
                s.Coefficients[i].PropertyId == propertyId)
            {
                idx = i;
                break;
            }
        }

        // Drop any prior ordering warning for this (alt, prop) pair before
        // deciding whether to emit one — matches the Python + TS pattern so
        // a re-edit that fixes the values clears the warning chip. The
        // message shape also matches Python + TS so log/UI parity holds.
        var stalePrefix = $"Coefficient ({alternativeId}, {propertyId}): ordering";
        ImmutableArray<string> warnings =
            State.Warnings.RemoveAll(w => w.StartsWith(stalePrefix));
        if (lower > modal || modal > upper)
        {
            warnings = warnings.Add(
                $"Coefficient ({alternativeId}, {propertyId}): " +
                $"ordering violated lower={lower} modal={modal} upper={upper}");
        }

        var newValue = new TriangularFuzzyM(lower, modal, upper);
        ImmutableArray<CoefficientM> newCoeffs;
        if (idx >= 0)
        {
            newCoeffs = s.Coefficients.SetItem(idx, s.Coefficients[idx] with { Value = newValue });
        }
        else
        {
            // New coefficient (shouldn't happen with a well-formed scenario, but be defensive).
            newCoeffs = s.Coefficients.Add(new CoefficientM(alternativeId, propertyId, newValue));
        }

        _setState(State with
        {
            Scenario = s with { Coefficients = newCoeffs },
            IsDirty = true,
            Warnings = warnings
        });
        _solve();
    }

    // ------------------------------------------------------------------
    // Constraints
    // ------------------------------------------------------------------

    public void AddThresholdConstraint(string propertyId, double? min, double? max)
    {
        var s = RequireScenario();
        if (!s.Properties.Any(p => p.Id == propertyId))
            throw new ScenarioMutationException($"Property '{propertyId}' not found.");
        if (min is null && max is null)
            throw new ScenarioMutationException("A threshold constraint needs at least one of min or max.");
        if (min.HasValue && max.HasValue && min.Value > max.Value)
            throw new ScenarioMutationException($"Threshold constraint min ({min}) must be ≤ max ({max}).");

        _setState(State with
        {
            Scenario = s with
            {
                Constraints = s.Constraints.Add(new ThresholdConstraintM(propertyId, min, max))
            },
            IsDirty = true
        });
        _solve();
    }

    public void UpdateThresholdConstraint(int index, string? propertyId, double? min, double? max)
    {
        var s = RequireScenario();
        var thresholds = s.Constraints
            .Select((c, i) => (c, i))
            .Where(x => x.c is ThresholdConstraintM)
            .ToList();

        if (index < 0 || index >= thresholds.Count)
            throw new ScenarioMutationException($"Threshold constraint index {index} out of range.");

        var (existing, globalIdx) = thresholds[index];
        var tc = (ThresholdConstraintM)existing;

        // null in this API has a meaning that ?? cannot express: the caller may
        // pass null *to clear* a bound. Distinguishing "leave unchanged" from
        // "clear" requires a 4-arg-flag approach; we model "clear" via an
        // explicit Set<...>Bound API in callers, but for the common
        // leave-unchanged path the existing API uses propertyId/min/max nullable
        // with null = "leave unchanged" — the caller is expected to read the
        // existing constraint first and pass the desired final value.
        var newPropId = propertyId ?? tc.PropertyId;
        var newMin = min ?? tc.Min;
        var newMax = max ?? tc.Max;

        // After applying the patch, the schema $defs/Constraint anyOf still
        // requires at least one of min/max to be present — guard so we don't
        // silently produce an invalid threshold by clearing both.
        if (newMin is null && newMax is null)
            throw new ScenarioMutationException(
                "ThresholdConstraint requires at least one of min or max.");

        if (newMin.HasValue && newMax.HasValue && newMin.Value > newMax.Value)
        {
            var w = $"Threshold constraint {index}: min > max, constraint skipped at solve.";
            var warnings = State.Warnings.Contains(w) ? State.Warnings : State.Warnings.Add(w);
            _setState(State with
            {
                Scenario = s with
                {
                    Constraints = s.Constraints.SetItem(globalIdx,
                        new ThresholdConstraintM(newPropId, newMin, newMax))
                },
                IsDirty = true,
                Warnings = warnings
            });
        }
        else
        {
            _setState(State with
            {
                Scenario = s with
                {
                    Constraints = s.Constraints.SetItem(globalIdx,
                        new ThresholdConstraintM(newPropId, newMin, newMax))
                },
                IsDirty = true
            });
        }
        _solve();
    }

    public void DeleteThresholdConstraint(int index)
    {
        var s = RequireScenario();
        var thresholds = s.Constraints
            .Select((c, i) => (c, i))
            .Where(x => x.c is ThresholdConstraintM)
            .ToList();

        if (index < 0 || index >= thresholds.Count)
            throw new ScenarioMutationException($"Threshold constraint index {index} out of range.");

        var (_, globalIdx) = thresholds[index];
        _setState(State with
        {
            Scenario = s with { Constraints = s.Constraints.RemoveAt(globalIdx) },
            IsDirty = true
        });
        _solve();
    }

    public void AddDependencyConstraint(string sourceAltId, string targetAltId)
    {
        var s = RequireScenario();
        if (!s.Alternatives.Any(a => a.Id == sourceAltId))
            throw new ScenarioMutationException($"Alternative '{sourceAltId}' not found.");
        if (!s.Alternatives.Any(a => a.Id == targetAltId))
            throw new ScenarioMutationException($"Alternative '{targetAltId}' not found.");

        // Invariant 7.1 (spec/domain/invariants.md) flags self-edges as FATAL,
        // and ScenarioLoader already throws on them at load time. The earlier
        // warn-and-accept behavior here would let a self-edge enter the
        // scenario via the editor, then fail the very next file round-trip.
        // Match the loader + Python + TypeScript: throw on self-edge.
        if (sourceAltId == targetAltId)
            throw new ScenarioMutationException(
                "Self-edge on dependency constraint (source must differ from target).");

        _setState(State with
        {
            Scenario = s with
            {
                Constraints = s.Constraints.Add(new DependencyConstraintM(sourceAltId, targetAltId))
            },
            IsDirty = true
        });
        _solve();
    }

    public void DeleteDependencyConstraint(int index)
    {
        var s = RequireScenario();
        var deps = s.Constraints
            .Select((c, i) => (c, i))
            .Where(x => x.c is DependencyConstraintM)
            .ToList();

        if (index < 0 || index >= deps.Count)
            throw new ScenarioMutationException($"Dependency constraint index {index} out of range.");

        var (_, globalIdx) = deps[index];
        _setState(State with
        {
            Scenario = s with { Constraints = s.Constraints.RemoveAt(globalIdx) },
            IsDirty = true
        });
        _solve();
    }

    public void AddConflictConstraint(string altAId, string altBId)
    {
        var s = RequireScenario();
        if (!s.Alternatives.Any(a => a.Id == altAId))
            throw new ScenarioMutationException($"Alternative '{altAId}' not found.");
        if (!s.Alternatives.Any(a => a.Id == altBId))
            throw new ScenarioMutationException($"Alternative '{altBId}' not found.");

        // Invariant 7.1: fatal, matching the loader and the Python/TS mutators.
        if (altAId == altBId)
            throw new ScenarioMutationException(
                "Self-edge on conflict constraint (alternativeA must differ from alternativeB).");

        _setState(State with
        {
            Scenario = s with
            {
                Constraints = s.Constraints.Add(new ConflictConstraintM(altAId, altBId))
            },
            IsDirty = true
        });
        _solve();
    }

    public void DeleteConflictConstraint(int index)
    {
        var s = RequireScenario();
        var conflicts = s.Constraints
            .Select((c, i) => (c, i))
            .Where(x => x.c is ConflictConstraintM)
            .ToList();

        if (index < 0 || index >= conflicts.Count)
            throw new ScenarioMutationException($"Conflict constraint index {index} out of range.");

        var (_, globalIdx) = conflicts[index];
        _setState(State with
        {
            Scenario = s with { Constraints = s.Constraints.RemoveAt(globalIdx) },
            IsDirty = true
        });
        _solve();
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

    /// <summary>M3 mutation helpers for in-place editing.</summary>
    public ScenarioMutator Mutator { get; }

    internal ScenarioCommands(
        ICommand newCmd,
        ICommand openCmd,
        ICommand saveCmd,
        ICommand saveAsCmd,
        ICommand solveCmd,
        ScenarioMutator mutator)
    {
        NewCmd = newCmd;
        OpenCmd = openCmd;
        SaveCmd = saveCmd;
        SaveAsCmd = saveAsCmd;
        SolveCmd = solveCmd;
        Mutator = mutator;
    }
}
