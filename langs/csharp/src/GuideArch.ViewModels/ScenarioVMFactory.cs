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
    /// A fresh empty scenario used by NewCmd. Also the shape the View's
    /// add-before-open auto-create policy produces (MainWindow runs NewCmd
    /// first when the user clicks Add with no scenario loaded).
    /// </summary>
    internal static readonly ScenarioM EmptyScenario = new(
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
            ImmutableArray<CandidateM> candidates;
            ImmutableArray<CriticalDecisionM> critDec;
            ImmutableArray<CriticalConstraintM> critCon;
            try
            {
                candidates = Solver.Solve(scenario);
                critDec = CriticalDecisions.Analyze(scenario, candidates);
                critCon = CriticalConstraints.Analyze(scenario);
            }
            catch (Exception ex)
            {
                // Mirror Python+TS: surface solver errors to Status instead of
                // letting them bubble to the Avalonia dispatcher (which would
                // crash the app on an unhandled exception).
                SetState(state with
                {
                    Candidates = ImmutableArray<CandidateM>.Empty,
                    CriticalDecisions = ImmutableArray<CriticalDecisionM>.Empty,
                    CriticalConstraints = ImmutableArray<CriticalConstraintM>.Empty,
                    Status = $"Solve error: {ex.Message}",
                    SelectedCandidateIndex = null
                });
                return;
            }

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
        //
        // spec/viewmodels.md §3.2 says NewCmd 'Replaces scenario with a fresh
        // empty ScenarioM'. Earlier versions used ScenarioState.Empty which
        // sets Scenario = null and required EnsureScenario to lazily hydrate
        // it on the first Add — UI state was inconsistent in the meantime
        // (HasScenario = false, all tabs showed 'no scenario loaded'). Now
        // NewCmd creates the empty scenario directly so the UI immediately
        // reflects the fresh-empty state, matching Python+TS New.
        // -----------------------------------------------------------------------
        ICommand newCmd = RelayCommand.Builder()
            .Task(() =>
            {
                SetState(ScenarioState.Empty with
                {
                    Scenario = EmptyScenario,
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
                    // Reset SelectedCandidateIndex on Open so Solve()'s
                    // preserve-if-in-range branch (commit 04c7d1e family)
                    // starts from a clean slate. Otherwise a prior selection
                    // could survive a scenario swap if the new scenario still
                    // had >= prior+1 candidates. Python + TS reset
                    // explicitly; C# was the odd one out.
                    SetState(state with
                    {
                        Scenario = scenario,
                        FilePath = path,
                        IsDirty = false,
                        Warnings = scenario.Warnings,
                        SelectedCandidateIndex = null,
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
                ValidateSavableScenario(state.Scenario);
                WriteScenario(state.Scenario, path);
            }
            catch (Exception ex)
            {
                // Match Python+TS Save breadth — narrowing to IOException only
                // misses PathTooLongException, JSON-serialize bugs, etc.
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

    private static void ValidateSavableScenario(ScenarioM scenario)
    {
        if (scenario.Decisions.IsEmpty)
            throw new ScenarioValidationException("Scenario must contain at least one decision before saving.");
        if (scenario.Alternatives.IsEmpty)
            throw new ScenarioValidationException("Scenario must contain at least one alternative before saving.");
        if (scenario.Properties.IsEmpty)
            throw new ScenarioValidationException("Scenario must contain at least one property before saving.");
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
        // Atomic write (tmp sibling + move) so a crash or disk-full mid-write
        // can't destroy the user's existing scenario file — same pattern as
        // AppVMFactory.PersistTheme.
        var tmp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(tmp, json);
            ScenarioLoader.Load(tmp);
            File.Move(tmp, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(tmp))
                File.Delete(tmp);
        }
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

    private ScenarioM RequireScenario()
    {
        if (State.Scenario is null)
            throw new ScenarioMutationException("No scenario loaded.");
        return State.Scenario;
    }

    /// <summary>
    /// Validates that both alternative ids exist and differ (invariants 2.5 +
    /// 7.1 at the mutation boundary). Shared by the dependency/conflict
    /// Add and Update paths.
    /// </summary>
    private static void RequireAltPair(
        ScenarioM s, string firstId, string secondId, string selfEdgeMessage)
    {
        if (!s.Alternatives.Any(a => a.Id == firstId))
            throw new ScenarioMutationException($"Alternative '{firstId}' not found.");
        if (!s.Alternatives.Any(a => a.Id == secondId))
            throw new ScenarioMutationException($"Alternative '{secondId}' not found.");
        if (firstId == secondId)
            throw new ScenarioMutationException(selfEdgeMessage);
    }

    /// <summary>
    /// Removes dependency/conflict constraints referencing any of the given
    /// alternative ids; threshold constraints are untouched (spec editors.md
    /// §2.1/§2.2 cascades).
    /// </summary>
    private static ImmutableArray<ConstraintM> RemoveConstraintsReferencing(
        ImmutableArray<ConstraintM> constraints, IReadOnlySet<string> altIds)
        => constraints
            .Where(c => c switch
            {
                DependencyConstraintM dep =>
                    !altIds.Contains(dep.SourceAlternativeId) &&
                    !altIds.Contains(dep.TargetAlternativeId),
                ConflictConstraintM conf =>
                    !altIds.Contains(conf.AlternativeAId) &&
                    !altIds.Contains(conf.AlternativeBId),
                _ => true
            })
            .ToImmutableArray();

    /// <summary>
    /// Appends a warning to the VM state through the factory's state setter.
    /// The View must use this (never write vm.Model directly) so the factory's
    /// closure state stays authoritative — a direct Model write is silently
    /// discarded by the next SetState. No solve is triggered.
    /// </summary>
    public void AddWarning(string message)
    {
        var s = State;
        _setState(s with { Warnings = s.Warnings.Add(message) });
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

    public void UpdateScenarioName(string name)
    {
        var s = RequireScenario();
        if (s.Name == name) return; // no-op: don't dirty the scenario
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

    /// <summary>
    /// Append a new decision. `name` defaults to "New decision" to match
    /// Python's `add_decision(name="New decision")` and TypeScript's
    /// `addDecision(name?)`. Throws when no scenario is loaded — the
    /// add-before-open auto-create convenience is View policy (the View runs
    /// NewCmd first), matching Python and TypeScript.
    /// </summary>
    public void AddDecision(string? name = null)
    {
        var s = RequireScenario();
        var id = $"d-{Guid.NewGuid()}";
        var newDecision = new DecisionM(id, name ?? "New decision");
        _setState(State with
        {
            Scenario = s with { Decisions = s.Decisions.Add(newDecision) },
            IsDirty = true
        });
        _solve();
    }

    /// <summary>
    /// Rename a decision. Does NOT trigger solve (spec viewmodels.md §3.3).
    /// The method name mirrors Python's update_decision_name and TypeScript's
    /// updateDecisionName so the three impls share the same mutation verb.
    /// </summary>
    public void UpdateDecisionName(string id, string newName)
    {
        var s = RequireScenario();
        // Single-pass index lookup. The previous
        // `IndexOf(FirstOrDefault(...)!)` form walked the list twice and
        // null-forgave a value that can legitimately be null on unknown id —
        // the `idx < 0` check after the fact masked it.
        int idx = -1;
        for (int i = 0; i < s.Decisions.Length; i++)
        {
            if (s.Decisions[i].Id == id) { idx = i; break; }
        }
        if (idx < 0) throw new ScenarioMutationException($"Decision '{id}' not found.");
        if (s.Decisions[idx].Name == newName) return; // no-op: don't dirty
        var updated = s.Decisions.SetItem(idx, s.Decisions[idx] with { Name = newName });
        _setState(State with
        {
            Scenario = s with { Decisions = updated },
            IsDirty = true
        });
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
        var newConstraints = RemoveConstraintsReferencing(s.Constraints, altIds);

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

    /// <summary>
    /// Append a new alternative under <paramref name="decisionId"/> and create
    /// zero-fuzzy coefficients for every existing property. `name` defaults to
    /// "New alternative" to match Python+TypeScript.
    /// </summary>
    public void AddAlternative(string decisionId, string? name = null)
    {
        var s = RequireScenario();
        if (!s.Decisions.Any(d => d.Id == decisionId))
            throw new ScenarioMutationException($"Decision '{decisionId}' not found.");

        var id = $"a-{Guid.NewGuid()}";
        var newAlt = new AlternativeM(id, decisionId, name ?? "New alternative");

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

    /// <summary>
    /// Rename an alternative. Does NOT trigger solve (spec viewmodels.md §3.3).
    /// See UpdateDecisionName for the naming-parity rationale.
    /// </summary>
    public void UpdateAlternativeName(string id, string newName)
    {
        var s = RequireScenario();
        var alt = s.Alternatives.FirstOrDefault(a => a.Id == id);
        if (alt is null) throw new ScenarioMutationException($"Alternative '{id}' not found.");
        if (alt.Name == newName) return; // no-op: don't dirty
        var idx = s.Alternatives.IndexOf(alt);
        _setState(State with
        {
            Scenario = s with { Alternatives = s.Alternatives.SetItem(idx, alt with { Name = newName }) },
            IsDirty = true
        });
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
        var newConstraints = RemoveConstraintsReferencing(
            s.Constraints, new HashSet<string> { id });

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

    /// <summary>
    /// Append a new property and create zero-fuzzy coefficients for every
    /// existing alternative. Defaults match Python's
    /// `add_property(name="New property", kind="min", weight=1.0)`.
    /// Throws when no scenario is loaded — auto-create is View policy,
    /// matching Python and TypeScript (see AddDecision).
    /// </summary>
    public void AddProperty(string? name = null, PropertyKind? kind = null, double? weight = null)
    {
        var s = RequireScenario();
        var id = $"p-{Guid.NewGuid()}";
        // NaN <= 0 is false, so the >0 guard alone would let NaN through and
        // poison every downstream score — hence the explicit finiteness check.
        if (weight.HasValue && (!double.IsFinite(weight.Value) || weight.Value <= 0))
            throw new ScenarioMutationException(
                $"Property weight must be > 0 (got {weight.Value}).");
        var newProp = new PropertyM(
            id,
            name ?? "New property",
            kind ?? PropertyKind.Min,
            weight ?? 1.0);

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

        // See AddProperty: NaN must not slip past the >0 guard.
        if (weight.HasValue && (!double.IsFinite(weight.Value) || weight.Value <= 0))
            throw new ScenarioMutationException(
                $"Property weight must be > 0 (got {weight.Value}).");

        var idx = s.Properties.IndexOf(prop);
        var updated = prop with
        {
            Name = name ?? prop.Name,
            Kind = kind ?? prop.Kind,
            Weight = weight ?? prop.Weight
        };
        if (updated == prop) return; // full no-op: no dirty, no solve
        bool triggerSolve = updated.Kind != prop.Kind || updated.Weight != prop.Weight;

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
        if (!s.Alternatives.Any(a => a.Id == alternativeId))
            throw new ScenarioMutationException($"Alternative '{alternativeId}' not found.");
        if (!s.Properties.Any(p => p.Id == propertyId))
            throw new ScenarioMutationException($"Property '{propertyId}' not found.");
        // JSON cannot encode NaN/Infinity, so a non-finite component would
        // solve "successfully" into NaN scores and then fail at save time.
        if (!double.IsFinite(lower) || !double.IsFinite(modal) || !double.IsFinite(upper))
            throw new ScenarioMutationException(
                $"Coefficient components must be finite (got {lower}, {modal}, {upper}).");
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
        // a re-edit that fixes the values clears the warning chip. Two
        // formats exist for the same condition: the VM's own
        // "Coefficient (a, p): ordering …" and the loader's
        // "Invariant 4.1: coefficient (a, p) …" (a file opened with
        // out-of-order values carries the latter). Sweep both so fixing the
        // cell always clears the chip. Ordinal comparison: ids are arbitrary
        // user strings and culture-sensitive StartsWith can mismatch.
        var stalePrefix = $"Coefficient ({alternativeId}, {propertyId}): ordering";
        var staleLoaderPrefix = $"Invariant 4.1: coefficient ({alternativeId}, {propertyId})";
        ImmutableArray<string> warnings = State.Warnings.RemoveAll(w =>
            w.StartsWith(stalePrefix, StringComparison.Ordinal) ||
            w.StartsWith(staleLoaderPrefix, StringComparison.Ordinal));
        if (lower > modal || modal > upper)
        {
            warnings = warnings.Add(
                $"Coefficient ({alternativeId}, {propertyId}): " +
                $"ordering violated lower={lower} modal={modal} upper={upper}");
        }

        var newValue = new TriangularFuzzyM(lower, modal, upper);
        // Full no-op (same value, no warning to add or sweep): skip dirty+solve.
        if (idx >= 0 && s.Coefficients[idx].Value == newValue
            && warnings == State.Warnings && !(lower > modal || modal > upper))
            return;
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
            throw new ScenarioMutationException(
                "ThresholdConstraint requires at least one of min or max.");
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

    // `index` is the GLOBAL index into Scenario.Constraints — the same index
    // space used by CriticalConstraintM.ConstraintIndex and by the Python and
    // TypeScript mutator APIs. See spec/viewmodels.md §5.5.
    //
    // null on propertyId/min/max means "leave unchanged". Because null cannot
    // also mean "clear", clearing one bound goes through the explicit
    // clearMin/clearMax flags — Python expresses the same distinction with
    // its _UNSET sentinel, and TS replaces the whole constraint.
    public void UpdateThresholdConstraint(
        int index, string? propertyId, double? min, double? max,
        bool clearMin = false, bool clearMax = false)
    {
        var s = RequireScenario();
        if (index < 0 || index >= s.Constraints.Length)
            throw new ScenarioMutationException($"Constraint index {index} out of range.");
        if (s.Constraints[index] is not ThresholdConstraintM tc)
            throw new ScenarioMutationException(
                $"Constraint at index {index} is not a ThresholdConstraint.");
        // Invariant 2.4 at the mutation boundary — matches the Add path and
        // the dependency/conflict Update paths (and TS's _validateConstraint).
        // Without this an unknown id round-trips into a file that fails the
        // loader's fatal invariant 2.4 on reopen.
        if (propertyId is not null && !s.Properties.Any(p => p.Id == propertyId))
            throw new ScenarioMutationException($"Property '{propertyId}' not found.");
        if (clearMin && min.HasValue)
            throw new ScenarioMutationException("clearMin and a min value are mutually exclusive.");
        if (clearMax && max.HasValue)
            throw new ScenarioMutationException("clearMax and a max value are mutually exclusive.");

        var newPropId = propertyId ?? tc.PropertyId;
        var newMin = clearMin ? null : (min ?? tc.Min);
        var newMax = clearMax ? null : (max ?? tc.Max);

        // After applying the patch, the schema $defs/Constraint anyOf still
        // requires at least one of min/max to be present — guard so we don't
        // silently produce an invalid threshold by clearing both.
        if (newMin is null && newMax is null)
            throw new ScenarioMutationException(
                "ThresholdConstraint requires at least one of min or max.");

        // Invariant 6.2: min ≤ max is FATAL (the loader throws). The earlier
        // warn-and-persist behavior would have let an editor save a file that
        // failed to re-open. Match Add* + loader by throwing here too.
        if (newMin.HasValue && newMax.HasValue && newMin.Value > newMax.Value)
            throw new ScenarioMutationException(
                $"Threshold constraint min ({newMin.Value}) must be ≤ max ({newMax.Value}).");

        var updatedTc = new ThresholdConstraintM(newPropId, newMin, newMax);
        if (updatedTc == tc) return; // no-op: no dirty, no solve

        _setState(State with
        {
            Scenario = s with
            {
                Constraints = s.Constraints.SetItem(index, updatedTc)
            },
            IsDirty = true
        });
        _solve();
    }

    public void AddDependencyConstraint(string sourceAltId, string targetAltId)
    {
        var s = RequireScenario();
        // Invariants 2.5 + 7.1 are FATAL at load time (spec/domain/
        // invariants.md), so the mutation boundary throws too — otherwise an
        // editor edit could produce a file that fails its next round-trip.
        RequireAltPair(s, sourceAltId, targetAltId,
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

    // `index` is the GLOBAL index into Scenario.Constraints. See §5.5.
    public void UpdateDependencyConstraint(int index, string? sourceAltId, string? targetAltId)
    {
        var s = RequireScenario();
        if (index < 0 || index >= s.Constraints.Length)
            throw new ScenarioMutationException($"Constraint index {index} out of range.");
        if (s.Constraints[index] is not DependencyConstraintM dc)
            throw new ScenarioMutationException(
                $"Constraint at index {index} is not a DependencyConstraint.");

        var newSource = sourceAltId ?? dc.SourceAlternativeId;
        var newTarget = targetAltId ?? dc.TargetAlternativeId;
        RequireAltPair(s, newSource, newTarget,
            "Self-edge on dependency constraint (source must differ from target).");
        if (newSource == dc.SourceAlternativeId && newTarget == dc.TargetAlternativeId)
            return; // no-op: no dirty, no solve

        _setState(State with
        {
            Scenario = s with
            {
                Constraints = s.Constraints.SetItem(index,
                    new DependencyConstraintM(newSource, newTarget))
            },
            IsDirty = true
        });
        _solve();
    }

    public void AddConflictConstraint(string altAId, string altBId)
    {
        var s = RequireScenario();
        RequireAltPair(s, altAId, altBId,
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

    // `index` is the GLOBAL index into Scenario.Constraints. See §5.5.
    public void UpdateConflictConstraint(int index, string? altAId, string? altBId)
    {
        var s = RequireScenario();
        if (index < 0 || index >= s.Constraints.Length)
            throw new ScenarioMutationException($"Constraint index {index} out of range.");
        if (s.Constraints[index] is not ConflictConstraintM cc)
            throw new ScenarioMutationException(
                $"Constraint at index {index} is not a ConflictConstraint.");

        var newA = altAId ?? cc.AlternativeAId;
        var newB = altBId ?? cc.AlternativeBId;
        RequireAltPair(s, newA, newB,
            "Self-edge on conflict constraint (alternativeA must differ from alternativeB).");
        if (newA == cc.AlternativeAId && newB == cc.AlternativeBId)
            return; // no-op: no dirty, no solve

        _setState(State with
        {
            Scenario = s with
            {
                Constraints = s.Constraints.SetItem(index,
                    new ConflictConstraintM(newA, newB))
            },
            IsDirty = true
        });
        _solve();
    }

    // Kind-agnostic delete by GLOBAL index into Scenario.Constraints — matches
    // the Python `delete_constraint` and TypeScript `deleteConstraint` APIs.
    public void DeleteConstraint(int index)
    {
        var s = RequireScenario();
        if (index < 0 || index >= s.Constraints.Length)
            throw new ScenarioMutationException($"Constraint index {index} out of range.");

        _setState(State with
        {
            Scenario = s with { Constraints = s.Constraints.RemoveAt(index) },
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
