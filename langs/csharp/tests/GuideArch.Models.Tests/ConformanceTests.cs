using GuideArch.Models;
using GuideArch.Models.Topsis;
using System.Text.Json;
using Xunit;

namespace GuideArch.Models.Tests;

/// <summary>
/// Runs the conformance runner programmatically and fails the test on any divergence.
/// Tolerance comes from spec/conformance/tolerances.json (absolute on all
/// scalars); rank and alternativeIds are exact.
/// </summary>
public class ConformanceTests
{
    // Fallback only if tolerances.json is missing — matches its committed value.
    private const double FallbackAbsTol = 1e-9;

    private static double AbsTol { get; } = LoadAbsTol();

    private static double LoadAbsTol()
    {
        // Read the tolerance from the corpus like the standalone
        // GuideArch.Conformance runner (and the TS/Python runners) do, so a
        // corpus tolerance change can't silently diverge from this suite.
        try
        {
            var path = Path.Combine(FindSpecConformanceDir(), "tolerances.json");
            var doc = JsonDocument.Parse(File.ReadAllText(path));
            // Same path the standalone runner and the Python runner read:
            // all scalar fields share one absolute tolerance at v1.0.
            return doc.RootElement
                .GetProperty("candidates")
                .GetProperty("score")
                .GetProperty("absolute")
                .GetDouble();
        }
        catch
        {
            return FallbackAbsTol;
        }
    }

    private static string FindSpecConformanceDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (int i = 0; i < 15; i++)
        {
            var candidate = Path.Combine(dir.FullName, "spec", "conformance");
            if (Directory.Exists(candidate)) return candidate;
            if (dir.Parent is null) break;
            dir = dir.Parent;
        }
        throw new InvalidOperationException("Cannot locate spec/conformance from test directory");
    }

    public static IEnumerable<object[]> ScenarioNames()
    {
        string specDir;
        try { specDir = FindSpecConformanceDir(); }
        catch { return Enumerable.Empty<object[]>(); }

        var files = Directory.GetFiles(Path.Combine(specDir, "scenarios"), "*.json");
        return files.Select(f => new object[] { Path.GetFileNameWithoutExtension(f).ToLowerInvariant() });
    }

    [Theory]
    [MemberData(nameof(ScenarioNames))]
    public void Scenario_MatchesExpected(string name)
    {
        string specDir = FindSpecConformanceDir();
        string scenarioPath = Path.Combine(specDir, "scenarios", $"{name}.json");
        string expectedDir = Path.Combine(specDir, "expected");

        var scenario = ScenarioLoader.Load(scenarioPath);
        var candidates = Solver.Solve(scenario);
        var cd = CriticalDecisions.Analyze(scenario, candidates);
        var cc = CriticalConstraints.Analyze(scenario);

        var diffs = new List<string>();

        // A missing expected file is a conformance FAILURE, not a skip — a
        // deleted/renamed corpus file must not turn this suite vacuously
        // green (the TS and Python runners record a divergence here too).

        // Candidates
        string candPath = Path.Combine(expectedDir, $"{name}.candidates.json");
        if (File.Exists(candPath))
        {
            var expList = JsonDocument.Parse(File.ReadAllText(candPath)).RootElement;
            CompareCandidates(expList, candidates, name, diffs);
        }
        else
        {
            diffs.Add($"[{name}] expected file missing: {candPath}");
        }

        // Critical decisions
        string cdPath = Path.Combine(expectedDir, $"{name}.critical-decisions.json");
        if (File.Exists(cdPath))
        {
            var expList = JsonDocument.Parse(File.ReadAllText(cdPath)).RootElement;
            CompareCriticalDecisions(expList, cd, name, diffs);
        }
        else
        {
            diffs.Add($"[{name}] expected file missing: {cdPath}");
        }

        // Critical constraints
        string ccPath = Path.Combine(expectedDir, $"{name}.critical-constraints.json");
        if (File.Exists(ccPath))
        {
            var expList = JsonDocument.Parse(File.ReadAllText(ccPath)).RootElement;
            CompareCriticalConstraints(expList, cc, name, diffs);
        }
        else
        {
            diffs.Add($"[{name}] expected file missing: {ccPath}");
        }

        Assert.True(diffs.Count == 0,
            $"Conformance failures for '{name}':\n" + string.Join("\n", diffs));
    }

    private static void CompareCandidates(
        JsonElement expList,
        System.Collections.Immutable.ImmutableArray<CandidateM> actual,
        string scenario, List<string> diffs)
    {
        int expCount = expList.GetArrayLength();
        if (expCount != actual.Length)
        {
            diffs.Add($"[{scenario}] candidates count: expected={expCount}, actual={actual.Length}");
            return;
        }
        int i = 0;
        foreach (var exp in expList.EnumerateArray())
        {
            var act = actual[i];
            var expIds = exp.GetProperty("alternativeIds").EnumerateArray().Select(e => e.GetString()!).ToList();
            if (!expIds.SequenceEqual(act.AlternativeIds.ToList()))
                diffs.Add($"candidates[{i}].alternativeIds mismatch");

            CheckTfm(exp.GetProperty("triangularValue"), act.TriangularValue, $"candidates[{i}].triangularValue", diffs);
            CheckNfm(exp.GetProperty("normalizedValue"), act.NormalizedValue, $"candidates[{i}].normalizedValue", diffs);
            CheckDouble(exp.GetProperty("score").GetDouble(), act.Score, $"candidates[{i}].score", diffs);

            int expRank = exp.GetProperty("rank").GetInt32();
            if (expRank != act.Rank)
                diffs.Add($"candidates[{i}].rank: expected={expRank}, actual={act.Rank}");
            i++;
        }
    }

    private static void CompareCriticalDecisions(
        JsonElement expList,
        System.Collections.Immutable.ImmutableArray<CriticalDecisionM> actual,
        string scenario, List<string> diffs)
    {
        int expCount = expList.GetArrayLength();
        if (expCount != actual.Length)
        {
            diffs.Add($"[{scenario}] critical-decisions count: expected={expCount}, actual={actual.Length}");
            return;
        }
        int i = 0;
        foreach (var exp in expList.EnumerateArray())
        {
            var act = actual[i];
            string expId = exp.GetProperty("decisionId").GetString()!;
            if (expId != act.DecisionId)
                diffs.Add($"critical-decisions[{i}].decisionId: expected={expId}, actual={act.DecisionId}");

            CheckTfm(exp.GetProperty("triangularValue"), act.TriangularValue, $"critical-decisions[{i}].triangularValue", diffs);
            CheckNfm(exp.GetProperty("normalizedValue"), act.NormalizedValue, $"critical-decisions[{i}].normalizedValue", diffs);
            CheckDouble(exp.GetProperty("score").GetDouble(), act.Score, $"critical-decisions[{i}].score", diffs);

            int expRank = exp.GetProperty("rank").GetInt32();
            if (expRank != act.Rank)
                diffs.Add($"critical-decisions[{i}].rank: expected={expRank}, actual={act.Rank}");
            i++;
        }
    }

    private static void CompareCriticalConstraints(
        JsonElement expList,
        System.Collections.Immutable.ImmutableArray<CriticalConstraintM> actual,
        string scenario, List<string> diffs)
    {
        int expCount = expList.GetArrayLength();
        if (expCount != actual.Length)
        {
            diffs.Add($"[{scenario}] critical-constraints count: expected={expCount}, actual={actual.Length}");
            return;
        }
        int i = 0;
        foreach (var exp in expList.EnumerateArray())
        {
            var act = actual[i];
            int expIdx = exp.GetProperty("constraintIndex").GetInt32();
            if (expIdx != act.ConstraintIndex)
                diffs.Add($"critical-constraints[{i}].constraintIndex: expected={expIdx}, actual={act.ConstraintIndex}");
            string expKind = exp.GetProperty("kind").GetString()!;
            if (expKind != act.Kind)
                diffs.Add($"critical-constraints[{i}].kind: expected={expKind}, actual={act.Kind}");
            int expElim = exp.GetProperty("eliminated").GetInt32();
            if (expElim != act.Eliminated)
                diffs.Add($"critical-constraints[{i}].eliminated: expected={expElim}, actual={act.Eliminated}");
            int expTotal = exp.GetProperty("total").GetInt32();
            if (expTotal != act.Total)
                diffs.Add($"critical-constraints[{i}].total: expected={expTotal}, actual={act.Total}");
            bool expRedundant = exp.GetProperty("redundant").GetBoolean();
            if (expRedundant != act.Redundant)
                diffs.Add($"critical-constraints[{i}].redundant: expected={expRedundant}, actual={act.Redundant}");
            i++;
        }
    }

    private static void CheckTfm(JsonElement e, TriangularFuzzyM a, string label, List<string> diffs)
    {
        CheckDouble(e.GetProperty("lower").GetDouble(), a.Lower, $"{label}.lower", diffs);
        CheckDouble(e.GetProperty("modal").GetDouble(), a.Modal, $"{label}.modal", diffs);
        CheckDouble(e.GetProperty("upper").GetDouble(), a.Upper, $"{label}.upper", diffs);
    }

    private static void CheckNfm(JsonElement e, NormalizedFuzzyM a, string label, List<string> diffs)
    {
        CheckDouble(e.GetProperty("positive").GetDouble(), a.Positive, $"{label}.positive", diffs);
        CheckDouble(e.GetProperty("average").GetDouble(), a.Average, $"{label}.average", diffs);
        CheckDouble(e.GetProperty("negative").GetDouble(), a.Negative, $"{label}.negative", diffs);
    }

    private static void CheckDouble(double expected, double actual, string label, List<string> diffs)
    {
        if (Math.Abs(expected - actual) > AbsTol)
            diffs.Add($"{label}: expected={expected:R}, actual={actual:R}, diff={Math.Abs(expected - actual):E3}");
    }
}
