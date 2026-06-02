using System.Collections.Immutable;
using GuideArch.Models;
using GuideArch.ViewModels;
using Xunit;

namespace GuideArch.ViewModels.Tests;

/// <summary>
/// Tests for pure data-preparation helpers in <see cref="ChartData"/>
/// (spec charts.md §7 — chart-data unit tests).
/// These tests run without a UI thread.
/// </summary>
public class ChartDataTests
{
    // -----------------------------------------------------------------------
    // Helpers — minimal scenario / candidates
    // -----------------------------------------------------------------------

    private static ScenarioM MakeScenario(int nDecisions, int nAltsEach, int nProps)
    {
        var decisions = Enumerable.Range(0, nDecisions)
            .Select(i => new DecisionM($"d{i}", $"Decision {i}"))
            .ToImmutableArray();

        var alternatives = decisions.SelectMany(d =>
            Enumerable.Range(0, nAltsEach)
                .Select(i => new AlternativeM($"{d.Id}-a{i}", d.Id, $"Alt {i} of {d.Name}"))
        ).ToImmutableArray();

        var properties = Enumerable.Range(0, nProps)
            .Select(i => new PropertyM($"p{i}", $"Property {i}", PropertyKind.Min, 1.0))
            .ToImmutableArray();

        var coefficients = alternatives.SelectMany(a =>
            properties.Select(p => new CoefficientM(
                a.Id, p.Id,
                new TriangularFuzzyM(1.0 + p.Id.Length * 0.1, 2.0, 3.0)))
        ).ToImmutableArray();

        return new ScenarioM(
            SchemaVersion: "1",
            Name: "Test Scenario",
            Description: "",
            Decisions: decisions,
            Alternatives: alternatives,
            Properties: properties,
            Coefficients: coefficients,
            Constraints: ImmutableArray<ConstraintM>.Empty,
            Config: new ConfigM(Aggregation.Max, new NormalizedFuzzyM(0.33, 0.34, 0.33)),
            Warnings: ImmutableArray<string>.Empty
        );
    }

    private static ImmutableArray<CandidateM> MakeCandidates(int count, int nAlts = 3)
    {
        return Enumerable.Range(0, count).Select(i =>
            new CandidateM(
                AlternativeIds: Enumerable.Range(0, nAlts)
                    .Select(j => $"d{j}-a{i % 2}")
                    .ToImmutableArray(),
                TriangularValue: new TriangularFuzzyM(i * 0.1, i * 0.1 + 0.05, i * 0.1 + 0.1),
                NormalizedValue: new NormalizedFuzzyM(0.3, 0.4, 0.3),
                Score: 0.01 + i * 0.01, // ascending scores (rank 0 = best = lowest score)
                Rank: i
            )
        ).ToImmutableArray();
    }

    // -----------------------------------------------------------------------
    // PrepRankedCandidates — Chart A data (spec §2)
    // -----------------------------------------------------------------------

    [Fact]
    public void PrepRankedCandidates_PreservesRankOrder()
    {
        var candidates = MakeCandidates(10);
        var bars = ChartData.PrepRankedCandidates(candidates);

        for (int i = 0; i < bars.Length - 1; i++)
        {
            Assert.True(bars[i].Rank < bars[i + 1].Rank,
                $"Bars should be in rank order: bars[{i}].Rank={bars[i].Rank} < bars[{i + 1}].Rank={bars[i + 1].Rank}");
        }
    }

    [Fact]
    public void PrepRankedCandidates_PreservesScoreValues()
    {
        var candidates = MakeCandidates(5);
        var bars = ChartData.PrepRankedCandidates(candidates);

        for (int i = 0; i < bars.Length; i++)
        {
            Assert.Equal(candidates[i].Score, bars[i].Score);
        }
    }

    [Fact]
    public void PrepRankedCandidates_CapsAtTopN()
    {
        var candidates = MakeCandidates(50);
        var bars = ChartData.PrepRankedCandidates(candidates, topN: 30);

        Assert.Equal(30, bars.Length);
    }

    [Fact]
    public void PrepRankedCandidates_ReturnsAll_WhenCountLessThanTopN()
    {
        var candidates = MakeCandidates(5);
        var bars = ChartData.PrepRankedCandidates(candidates, topN: 30);

        Assert.Equal(5, bars.Length);
    }

    [Fact]
    public void PrepRankedCandidates_OpacityFactor_FullAtRankZero()
    {
        var candidates = MakeCandidates(10);
        var bars = ChartData.PrepRankedCandidates(candidates);

        Assert.Equal(1.0, bars[0].OpacityFactor, precision: 6);
    }

    [Fact]
    public void PrepRankedCandidates_OpacityFactor_HalfAtLastRank()
    {
        var candidates = MakeCandidates(10);
        var bars = ChartData.PrepRankedCandidates(candidates, topN: 10);

        // Last bar should have opacity 0.5.
        Assert.Equal(0.5, bars[bars.Length - 1].OpacityFactor, precision: 6);
    }

    [Fact]
    public void PrepRankedCandidates_OpacityFactor_OneWhenSingleCandidate()
    {
        var candidates = MakeCandidates(1);
        var bars = ChartData.PrepRankedCandidates(candidates);

        Assert.Equal(1.0, bars[0].OpacityFactor, precision: 6);
    }

    [Fact]
    public void PrepRankedCandidates_AlternativeIds_MatchesCandidate()
    {
        var candidates = MakeCandidates(3);
        var bars = ChartData.PrepRankedCandidates(candidates);

        for (int i = 0; i < bars.Length; i++)
        {
            Assert.Equal(candidates[i].AlternativeIds, bars[i].AlternativeIds);
        }
    }

    [Fact]
    public void PrepRankedCandidates_EmptyInput_ReturnsEmpty()
    {
        var bars = ChartData.PrepRankedCandidates(ImmutableArray<CandidateM>.Empty);
        Assert.Empty(bars);
    }

    // -----------------------------------------------------------------------
    // PrepTriangleSeries — Chart B data (spec §3)
    // -----------------------------------------------------------------------

    [Fact]
    public void PrepTriangleSeries_ReturnsOneSeriesPerProperty()
    {
        var scenario = MakeScenario(nDecisions: 2, nAltsEach: 2, nProps: 3);
        var candidate = new CandidateM(
            AlternativeIds: ImmutableArray.Create("d0-a0", "d1-a0"),
            TriangularValue: TriangularFuzzyM.Zero,
            NormalizedValue: new NormalizedFuzzyM(0.3, 0.4, 0.3),
            Score: 0.1,
            Rank: 0);

        var triangles = ChartData.PrepTriangleSeries(candidate, scenario);

        Assert.Equal(3, triangles.Length); // 3 properties
    }

    [Fact]
    public void PrepTriangleSeries_XsHaveFourPoints_ClosedTriangle()
    {
        var scenario = MakeScenario(nDecisions: 1, nAltsEach: 2, nProps: 2);
        var candidate = new CandidateM(
            AlternativeIds: ImmutableArray.Create("d0-a0"),
            TriangularValue: TriangularFuzzyM.Zero,
            NormalizedValue: new NormalizedFuzzyM(0.3, 0.4, 0.3),
            Score: 0.1,
            Rank: 0);

        var triangles = ChartData.PrepTriangleSeries(candidate, scenario);

        foreach (var t in triangles)
        {
            Assert.Equal(4, t.Xs.Length); // closed triangle: L, M, U, L
            Assert.Equal(4, t.Ys.Length);
            Assert.Equal(0.0, t.Ys[0]); // membership at Lower = 0
            Assert.Equal(1.0, t.Ys[1]); // membership at Modal = 1
            Assert.Equal(0.0, t.Ys[2]); // membership at Upper = 0
            Assert.Equal(0.0, t.Ys[3]); // closed: back to Lower
        }
    }

    [Fact]
    public void PrepTriangleSeries_XsMatchLowerModalUpper()
    {
        var scenario = MakeScenario(nDecisions: 1, nAltsEach: 1, nProps: 1);
        var coeff = scenario.Coefficients[0]; // d0-a0, p0

        var candidate = new CandidateM(
            AlternativeIds: ImmutableArray.Create("d0-a0"),
            TriangularValue: TriangularFuzzyM.Zero,
            NormalizedValue: new NormalizedFuzzyM(0.3, 0.4, 0.3),
            Score: 0.1,
            Rank: 0);

        var triangles = ChartData.PrepTriangleSeries(candidate, scenario);

        Assert.Single(triangles);
        var t = triangles[0];
        Assert.Equal(coeff.Value.Lower, t.Xs[0]);
        Assert.Equal(coeff.Value.Modal, t.Xs[1]);
        Assert.Equal(coeff.Value.Upper, t.Xs[2]);
        Assert.Equal(coeff.Value.Lower, t.Xs[3]); // closed
    }

    [Fact]
    public void PrepTriangleSeries_PropertyNames_Match()
    {
        var scenario = MakeScenario(nDecisions: 1, nAltsEach: 1, nProps: 2);
        var candidate = new CandidateM(
            AlternativeIds: ImmutableArray.Create("d0-a0"),
            TriangularValue: TriangularFuzzyM.Zero,
            NormalizedValue: new NormalizedFuzzyM(0.3, 0.4, 0.3),
            Score: 0.1,
            Rank: 0);

        var triangles = ChartData.PrepTriangleSeries(candidate, scenario);

        var propNames = scenario.Properties.Select(p => p.Name).ToList();
        foreach (var t in triangles)
        {
            Assert.Contains(t.PropertyName, propNames);
        }
    }

    // -----------------------------------------------------------------------
    // PrepCriticalDecisions (spec §4)
    // -----------------------------------------------------------------------

    [Fact]
    public void PrepCriticalDecisions_SortedAscendingByRank()
    {
        var scenario = MakeScenario(nDecisions: 3, nAltsEach: 1, nProps: 1);
        var critDec = ImmutableArray.Create(
            new CriticalDecisionM("d2", TriangularFuzzyM.Zero, new NormalizedFuzzyM(0, 0, 0), 0.9, 2),
            new CriticalDecisionM("d0", TriangularFuzzyM.Zero, new NormalizedFuzzyM(0, 0, 0), 0.1, 0),
            new CriticalDecisionM("d1", TriangularFuzzyM.Zero, new NormalizedFuzzyM(0, 0, 0), 0.5, 1)
        );

        var rows = ChartData.PrepCriticalDecisions(critDec, scenario);

        Assert.Equal(3, rows.Length);
        Assert.Equal(0, rows[0].Rank);
        Assert.Equal(1, rows[1].Rank);
        Assert.Equal(2, rows[2].Rank);
    }

    [Fact]
    public void PrepCriticalDecisions_ResolvesDecisionName()
    {
        var scenario = MakeScenario(nDecisions: 2, nAltsEach: 1, nProps: 1);
        var critDec = ImmutableArray.Create(
            new CriticalDecisionM("d0", TriangularFuzzyM.Zero, new NormalizedFuzzyM(0, 0, 0), 0.1, 0)
        );

        var rows = ChartData.PrepCriticalDecisions(critDec, scenario);

        Assert.Equal("Decision 0", rows[0].DecisionName);
    }

    [Fact]
    public void PrepCriticalDecisions_UsesDecisionId_WhenNameNotFound()
    {
        var scenario = MakeScenario(nDecisions: 1, nAltsEach: 1, nProps: 1);
        var critDec = ImmutableArray.Create(
            new CriticalDecisionM("d-unknown", TriangularFuzzyM.Zero, new NormalizedFuzzyM(0, 0, 0), 0.1, 0)
        );

        var rows = ChartData.PrepCriticalDecisions(critDec, scenario);

        // Falls back to the ID.
        Assert.Equal("d-unknown", rows[0].DecisionName);
    }

    [Fact]
    public void PrepCriticalDecisions_ScoreFormattedTo6SigFigs()
    {
        var scenario = MakeScenario(nDecisions: 1, nAltsEach: 1, nProps: 1);
        var score = 0.031180695179944085;
        var critDec = ImmutableArray.Create(
            new CriticalDecisionM("d0", TriangularFuzzyM.Zero, new NormalizedFuzzyM(0, 0, 0), score, 0)
        );

        var rows = ChartData.PrepCriticalDecisions(critDec, scenario);

        Assert.Equal(score.ToString("G6"), rows[0].Score);
    }

    // -----------------------------------------------------------------------
    // PrepCriticalConstraints (spec §5)
    // -----------------------------------------------------------------------

    [Fact]
    public void PrepCriticalConstraints_SortedDescendingByEliminated()
    {
        var critCon = ImmutableArray.Create(
            new CriticalConstraintM(0, "threshold", Eliminated: 5, Total: 100, Redundant: false),
            new CriticalConstraintM(1, "dependency", Eliminated: 20, Total: 100, Redundant: false),
            new CriticalConstraintM(2, "conflict", Eliminated: 1, Total: 100, Redundant: true)
        );

        var rows = ChartData.PrepCriticalConstraints(critCon);

        Assert.Equal(3, rows.Length);
        Assert.Equal(20, rows[0].Eliminated);
        Assert.Equal(5, rows[1].Eliminated);
        Assert.Equal(1, rows[2].Eliminated);
    }

    [Fact]
    public void PrepCriticalConstraints_EliminatedPercent_IsCorrect()
    {
        var critCon = ImmutableArray.Create(
            new CriticalConstraintM(0, "threshold", Eliminated: 25, Total: 100, Redundant: false)
        );

        var rows = ChartData.PrepCriticalConstraints(critCon);

        Assert.Equal("25.0%", rows[0].EliminatedPercent);
    }

    [Fact]
    public void PrepCriticalConstraints_EliminatedPercent_HandlesZeroTotal()
    {
        var critCon = ImmutableArray.Create(
            new CriticalConstraintM(0, "threshold", Eliminated: 0, Total: 0, Redundant: false)
        );

        var rows = ChartData.PrepCriticalConstraints(critCon);

        Assert.Equal("N/A", rows[0].EliminatedPercent);
    }

    [Fact]
    public void PrepCriticalConstraints_RedundantDisplay_Yes_WhenRedundant()
    {
        var critCon = ImmutableArray.Create(
            new CriticalConstraintM(0, "conflict", Eliminated: 10, Total: 100, Redundant: true)
        );

        var rows = ChartData.PrepCriticalConstraints(critCon);

        Assert.Equal("Yes", rows[0].RedundantDisplay);
    }

    [Fact]
    public void PrepCriticalConstraints_RedundantDisplay_No_WhenNotRedundant()
    {
        var critCon = ImmutableArray.Create(
            new CriticalConstraintM(0, "threshold", Eliminated: 10, Total: 100, Redundant: false)
        );

        var rows = ChartData.PrepCriticalConstraints(critCon);

        Assert.Equal("No", rows[0].RedundantDisplay);
    }

    [Fact]
    public void PrepCriticalConstraints_EmptyInput_ReturnsEmpty()
    {
        var rows = ChartData.PrepCriticalConstraints(ImmutableArray<CriticalConstraintM>.Empty);
        Assert.Empty(rows);
    }

    // -----------------------------------------------------------------------
    // PrepComparisonSeries — Chart C data (spec §4)
    //
    // Mirrors langs/typescript/tests/unit/chart-comparison.test.ts and
    // langs/python/tests/unit/test_chart_comparison.py so the three impls
    // lock the same invariants.
    // -----------------------------------------------------------------------

    private static ScenarioM MakeScenarioWithExplicitCoeffs(
        int nProps,
        params (string AltId, string PropId, double Modal)[] coeffs)
    {
        // One decision with explicit alternatives derived from the coeff list.
        var altIds = coeffs.Select(c => c.AltId).Distinct().ToImmutableArray();
        var decisions = ImmutableArray.Create(new DecisionM("d0", "Decision"));
        var alternatives = altIds
            .Select(id => new AlternativeM(id, "d0", id))
            .ToImmutableArray();
        var properties = Enumerable.Range(0, nProps)
            .Select(i => new PropertyM($"p{i + 1}", $"P{i + 1}", PropertyKind.Max, 1.0))
            .ToImmutableArray();
        var coefficients = coeffs
            .Select(c => new CoefficientM(
                c.AltId, c.PropId,
                new TriangularFuzzyM(c.Modal, c.Modal, c.Modal)))
            .ToImmutableArray();

        return new ScenarioM(
            SchemaVersion: "1",
            Name: "Comparison Test",
            Description: "",
            Decisions: decisions,
            Alternatives: alternatives,
            Properties: properties,
            Coefficients: coefficients,
            Constraints: ImmutableArray<ConstraintM>.Empty,
            Config: new ConfigM(Aggregation.Max, new NormalizedFuzzyM(0.33, 0.34, 0.33)),
            Warnings: ImmutableArray<string>.Empty
        );
    }

    [Fact]
    public void PrepComparisonSeries_EmptyCandidates_ReturnsEmpty()
    {
        var scenario = MakeScenario(1, 1, 1);
        var result = ChartData.PrepComparisonSeries(
            ImmutableArray<CandidateM>.Empty, scenario);
        Assert.Empty(result);
    }

    [Fact]
    public void PrepComparisonSeries_EmptyProperties_ReturnsEmpty()
    {
        var scenario = MakeScenario(1, 1, 0);
        var candidates = MakeCandidates(3);
        var result = ChartData.PrepComparisonSeries(candidates, scenario);
        Assert.Empty(result);
    }

    [Fact]
    public void PrepComparisonSeries_DefaultTopN_Is10()
    {
        Assert.Equal(10, ChartData.DefaultComparisonTopN);
    }

    [Fact]
    public void PrepComparisonSeries_CapsAtDefaultTopN()
    {
        var scenario = MakeScenario(1, 2, 1);
        var candidates = MakeCandidates(25);
        var result = ChartData.PrepComparisonSeries(candidates, scenario);
        Assert.Equal(10, result.Length);
    }

    [Fact]
    public void PrepComparisonSeries_CapsAtExplicitTopN()
    {
        var scenario = MakeScenario(1, 2, 1);
        var candidates = MakeCandidates(15);
        var result = ChartData.PrepComparisonSeries(candidates, scenario, topN: 5);
        Assert.Equal(5, result.Length);
    }

    [Fact]
    public void PrepComparisonSeries_FewerCandidatesThanTopN_ReturnsAll()
    {
        var scenario = MakeScenario(1, 2, 1);
        var candidates = MakeCandidates(3);
        var result = ChartData.PrepComparisonSeries(candidates, scenario, topN: 10);
        Assert.Equal(3, result.Length);
    }

    [Fact]
    public void PrepComparisonSeries_PaletteIndex_MatchesArrayPosition()
    {
        var scenario = MakeScenario(1, 2, 1);
        var candidates = MakeCandidates(3);
        var result = ChartData.PrepComparisonSeries(candidates, scenario);

        Assert.Equal(0, result[0].PaletteIndex);
        Assert.Equal(1, result[1].PaletteIndex);
        Assert.Equal(2, result[2].PaletteIndex);
    }

    [Fact]
    public void PrepComparisonSeries_LabelFormat_RankAndScoreG3()
    {
        var scenario = MakeScenario(1, 2, 1);
        var candidates = ImmutableArray.Create(
            new CandidateM(
                AlternativeIds: ImmutableArray.Create("d0-a0"),
                TriangularValue: TriangularFuzzyM.Zero,
                NormalizedValue: new NormalizedFuzzyM(0.3, 0.4, 0.3),
                Score: 0.03118,
                Rank: 0),
            new CandidateM(
                AlternativeIds: ImmutableArray.Create("d0-a1"),
                TriangularValue: TriangularFuzzyM.Zero,
                NormalizedValue: new NormalizedFuzzyM(0.3, 0.4, 0.3),
                Score: 0.0271423,
                Rank: 7));
        var result = ChartData.PrepComparisonSeries(candidates, scenario);

        Assert.Equal("#0 (0.0312)", result[0].Label);
        Assert.Equal("#7 (0.0271)", result[1].Label);
    }

    [Fact]
    public void PrepComparisonSeries_RankPreserved()
    {
        var scenario = MakeScenario(1, 2, 1);
        var candidates = MakeCandidates(3);
        var result = ChartData.PrepComparisonSeries(candidates, scenario);

        Assert.Equal(0, result[0].Rank);
        Assert.Equal(1, result[1].Rank);
        Assert.Equal(2, result[2].Rank);
    }

    [Fact]
    public void PrepComparisonSeries_ModalSum_AcrossCandidateAlternatives()
    {
        // Candidate selects a1 + a2 (a3 is noise, not selected).
        // p1: 0.6 + 0.3 = 0.9
        // p2: 0.1 + 0.2 = 0.3
        var scenario = MakeScenarioWithExplicitCoeffs(
            nProps: 2,
            ("a1", "p1", 0.6),
            ("a1", "p2", 0.1),
            ("a2", "p1", 0.3),
            ("a2", "p2", 0.2),
            ("a3", "p1", 99.0), // noise
            ("a3", "p2", 99.0));
        var candidates = ImmutableArray.Create(
            new CandidateM(
                AlternativeIds: ImmutableArray.Create("a1", "a2"),
                TriangularValue: TriangularFuzzyM.Zero,
                NormalizedValue: new NormalizedFuzzyM(0.3, 0.4, 0.3),
                Score: 0.5,
                Rank: 0));

        var result = ChartData.PrepComparisonSeries(candidates, scenario);

        Assert.Equal(2, result[0].Ys.Length);
        Assert.Equal(0.9, result[0].Ys[0], precision: 12);
        Assert.Equal(0.3, result[0].Ys[1], precision: 12);
    }

    [Fact]
    public void PrepComparisonSeries_MissingCoefficient_ContributesZero()
    {
        var scenario = MakeScenarioWithExplicitCoeffs(
            nProps: 2,
            ("a1", "p1", 0.5));  // (a1, p2) deliberately absent
        var candidates = ImmutableArray.Create(
            new CandidateM(
                AlternativeIds: ImmutableArray.Create("a1"),
                TriangularValue: TriangularFuzzyM.Zero,
                NormalizedValue: new NormalizedFuzzyM(0.3, 0.4, 0.3),
                Score: 0.5,
                Rank: 0));

        var result = ChartData.PrepComparisonSeries(candidates, scenario);

        Assert.Equal(0.5, result[0].Ys[0], precision: 12);
        Assert.Equal(0.0, result[0].Ys[1]);
    }

    [Fact]
    public void PrepComparisonSeries_Xs_IsZeroIndexedPerProperty()
    {
        var scenario = MakeScenario(1, 2, 3);
        var candidates = MakeCandidates(2);
        var result = ChartData.PrepComparisonSeries(candidates, scenario);

        Assert.Equal(new[] { 0.0, 1.0, 2.0 }, result[0].Xs);
    }

    [Fact]
    public void PrepComparisonSeries_ScoreExposed_ForLegendChip()
    {
        var scenario = MakeScenario(1, 2, 1);
        var candidates = ImmutableArray.Create(
            new CandidateM(
                AlternativeIds: ImmutableArray.Create("d0-a0"),
                TriangularValue: TriangularFuzzyM.Zero,
                NormalizedValue: new NormalizedFuzzyM(0.3, 0.4, 0.3),
                Score: 0.031180695179944,
                Rank: 0));

        var result = ChartData.PrepComparisonSeries(candidates, scenario);

        Assert.Equal(0.031180695179944, result[0].Score, precision: 12);
    }
}
