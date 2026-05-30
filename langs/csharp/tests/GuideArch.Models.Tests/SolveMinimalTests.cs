using System.Collections.Immutable;
using GuideArch.Models;
using GuideArch.Models.Topsis;
using Xunit;

namespace GuideArch.Models.Tests;

/// <summary>
/// Hand-computable minimal scenario: 1 decision, 2 alternatives, 1 property.
/// This lets us verify the pipeline numerically without relying on the JSON files.
/// </summary>
public class SolveMinimalTests
{
    private static ScenarioM MakeMinimalScenario()
    {
        // 1 decision, 2 alternatives, 1 min property, weight=1
        // alt-A coeff: (1, 2, 3)
        // alt-B coeff: (2, 3, 4)
        // With aggregation=max, weights=(1/3, 1/3, 1/3)
        //
        // §3.4 Normalizer:
        //   M(p1) for min: max(lower_A, lower_B) = max(1, 2) = 2
        //
        // §3.5 Total value:
        //   candidate [A]: sign=+1, weight=1, contrib=(1,2,3)/2 = (0.5, 1.0, 1.5)
        //   candidate [B]: sign=+1, weight=1, contrib=(2,3,4)/2 = (1.0, 1.5, 2.0)
        //
        // §3.6 Z-space:
        //   z[A]: positive=|1.0-0.5|=0.5, average=1.0, negative=|1.5-1.0|=0.5
        //   z[B]: positive=|1.5-1.0|=0.5, average=1.5, negative=|2.0-1.5|=0.5
        //
        // §3.7 PIS/NIS:
        //   PIS: avg=min(1.0,1.5)=1.0, pos=max(0.5,0.5)=0.5, neg=min(0.5,0.5)=0.5
        //   NIS: avg=max(1.0,1.5)=1.5, pos=min(0.5,0.5)=0.5, neg=max(0.5,0.5)=0.5
        //
        // §3.8 Normalize:
        //   denom_avg = 1.5-1.0 = 0.5
        //   n_avg[A] = (1.0-1.0)/0.5 = 0.0
        //   n_avg[B] = (1.5-1.0)/0.5 = 1.0
        //   denom_pos = 0.5-0.5 = 0  → n_pos = 0.0 for both
        //   denom_neg = 0.5-0.5 = 0  → n_neg = 0.0 for both
        //
        // §3.10 Score (max): max(w*0, w*n_avg, w*0) = 1/3 * n_avg
        //   score[A] = 0.0 → rank 0 (lower = better)
        //   score[B] = 1/3 ≈ 0.3333... → rank 1

        var dec = new DecisionM("d1", "Decision 1");
        var altA = new AlternativeM("a1", "d1", "Alt A");
        var altB = new AlternativeM("a2", "d1", "Alt B");
        var prop = new PropertyM("p1", "Prop 1", PropertyKind.Min, 1.0);
        var coeffs = ImmutableArray.Create(
            new CoefficientM("a1", "p1", new TriangularFuzzyM(1.0, 2.0, 3.0)),
            new CoefficientM("a2", "p1", new TriangularFuzzyM(2.0, 3.0, 4.0))
        );
        var config = new ConfigM(
            Aggregation.Max,
            new NormalizedFuzzyM(1.0 / 3.0, 1.0 / 3.0, 1.0 / 3.0)
        );
        return new ScenarioM(
            SchemaVersion: "1.0.0",
            Name: "minimal",
            Description: "",
            Decisions: ImmutableArray.Create(dec),
            Alternatives: ImmutableArray.Create(altA, altB),
            Properties: ImmutableArray.Create(prop),
            Coefficients: coeffs,
            Constraints: ImmutableArray<ConstraintM>.Empty,
            Config: config,
            Warnings: ImmutableArray<string>.Empty
        );
    }

    [Fact]
    public void MinimalScenario_ProducesCorrectRanking()
    {
        var scenario = MakeMinimalScenario();
        var candidates = Solver.Solve(scenario);

        Assert.Equal(2, candidates.Length);
        // Lower score = better → rank 0 = alt A (lower values)
        Assert.Equal("a1", candidates[0].AlternativeIds[0]);
        Assert.Equal(0, candidates[0].Rank);
        Assert.Equal("a2", candidates[1].AlternativeIds[0]);
        Assert.Equal(1, candidates[1].Rank);
    }

    [Fact]
    public void MinimalScenario_ScoreA_IsZero()
    {
        var scenario = MakeMinimalScenario();
        var candidates = Solver.Solve(scenario);
        Assert.Equal(0, candidates[0].Rank);
        Assert.True(Math.Abs(candidates[0].Score - 0.0) < 1e-12, $"Expected score≈0, got {candidates[0].Score}");
    }

    [Fact]
    public void MinimalScenario_ScoreB_IsOneThird()
    {
        var scenario = MakeMinimalScenario();
        var candidates = Solver.Solve(scenario);
        Assert.Equal(1, candidates[1].Rank);
        double expected = 1.0 / 3.0;
        Assert.True(Math.Abs(candidates[1].Score - expected) < 1e-12,
            $"Expected score≈{expected:R}, got {candidates[1].Score:R}");
    }

    [Fact]
    public void MinimalScenario_TriangularValues_Correct()
    {
        var scenario = MakeMinimalScenario();
        var candidates = Solver.Solve(scenario);

        var tvA = candidates[0].TriangularValue;
        Assert.True(Math.Abs(tvA.Lower - 0.5) < 1e-12);
        Assert.True(Math.Abs(tvA.Modal - 1.0) < 1e-12);
        Assert.True(Math.Abs(tvA.Upper - 1.5) < 1e-12);
    }

    [Fact]
    public void MinimalScenario_NormalizedValues_Correct()
    {
        var scenario = MakeMinimalScenario();
        var candidates = Solver.Solve(scenario);

        var nvA = candidates[0].NormalizedValue;
        Assert.True(Math.Abs(nvA.Average - 0.0) < 1e-12);
        Assert.True(Math.Abs(nvA.Positive - 0.0) < 1e-12);
        Assert.True(Math.Abs(nvA.Negative - 0.0) < 1e-12);
    }

    [Fact]
    public void EmptyFeasibleSet_ReturnsEmpty()
    {
        var dec = new DecisionM("d1", "D1");
        var altA = new AlternativeM("a1", "d1", "A");
        var altB = new AlternativeM("a2", "d1", "B");
        var prop = new PropertyM("p1", "P", PropertyKind.Min, 1.0);
        var coeffs = ImmutableArray.Create(
            new CoefficientM("a1", "p1", new TriangularFuzzyM(1.0, 2.0, 3.0)),
            new CoefficientM("a2", "p1", new TriangularFuzzyM(1.0, 2.0, 3.0))
        );
        // Conflict eliminates everything (only one decision so conflict can't actually fire —
        // but we can use a threshold that eliminates all)
        var constraints = ImmutableArray.Create<ConstraintM>(
            new ThresholdConstraintM("p1", Max: 0.0, Min: null) // lower vertex must be <= 0, but it's 1.0
        );
        var scenario = new ScenarioM(
            SchemaVersion: "1.0.0", Name: "empty", Description: "",
            Decisions: ImmutableArray.Create(dec),
            Alternatives: ImmutableArray.Create(altA, altB),
            Properties: ImmutableArray.Create(prop),
            Coefficients: coeffs,
            Constraints: constraints,
            Config: new ConfigM(Aggregation.Max, new NormalizedFuzzyM(1.0 / 3.0, 1.0 / 3.0, 1.0 / 3.0)),
            Warnings: ImmutableArray<string>.Empty
        );
        var candidates = Solver.Solve(scenario);
        Assert.Empty(candidates);
    }
}
