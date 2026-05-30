using GuideArch.Models;
using GuideArch.Models.Topsis;
using System.Text.Json;

/// <summary>
/// Conformance runner — compares solver output against spec/conformance/expected/*.json.
/// Exits 0 on pass, non-zero on fail.
/// Tolerance: 1e-9 absolute on all scalar outputs (spec/conformance/tolerances.json).
/// Ranking is exact.
/// </summary>
class Program
{
    private const double AbsTol = 1e-9;

    static int Main(string[] args)
    {
        // Locate repo root by walking up from assembly location
        string specDir = FindSpecDir();
        string scenariosDir = Path.Combine(specDir, "scenarios");
        string expectedDir = Path.Combine(specDir, "expected");

        if (!Directory.Exists(scenariosDir))
        {
            Console.Error.WriteLine($"ERROR: scenarios directory not found: {scenariosDir}");
            return 2;
        }

        var allDiffs = new List<string>();
        var scenarioFiles = Directory.GetFiles(scenariosDir, "*.json").OrderBy(f => f).ToList();

        if (scenarioFiles.Count == 0)
        {
            Console.Error.WriteLine($"ERROR: no scenario files found in {scenariosDir}");
            return 2;
        }

        foreach (var scenarioPath in scenarioFiles)
        {
            string name = Path.GetFileNameWithoutExtension(scenarioPath).ToLowerInvariant();
            Console.WriteLine($"Running scenario: {name}");

            ScenarioM scenario;
            try
            {
                scenario = ScenarioLoader.Load(scenarioPath);
            }
            catch (Exception ex)
            {
                allDiffs.Add($"[{name}] LOAD ERROR: {ex.Message}");
                continue;
            }

            var candidates = Solver.Solve(scenario);
            var cd = CriticalDecisions.Analyze(scenario, candidates);
            var cc = CriticalConstraints.Analyze(scenario);

            // Compare candidates
            string candPath = Path.Combine(expectedDir, $"{name}.candidates.json");
            if (File.Exists(candPath))
            {
                var expList = JsonDocument.Parse(File.ReadAllText(candPath)).RootElement;
                CompareCandidates(expList, candidates, name, allDiffs);
            }
            else
            {
                allDiffs.Add($"[{name}] candidates: expected file missing: {candPath}");
            }

            // Compare critical decisions
            string cdPath = Path.Combine(expectedDir, $"{name}.critical-decisions.json");
            if (File.Exists(cdPath))
            {
                var expList = JsonDocument.Parse(File.ReadAllText(cdPath)).RootElement;
                CompareCriticalDecisions(expList, cd, name, allDiffs);
            }
            else
            {
                allDiffs.Add($"[{name}] critical-decisions: expected file missing: {cdPath}");
            }

            // Compare critical constraints
            string ccPath = Path.Combine(expectedDir, $"{name}.critical-constraints.json");
            if (File.Exists(ccPath))
            {
                var expList = JsonDocument.Parse(File.ReadAllText(ccPath)).RootElement;
                CompareCriticalConstraints(expList, cc, name, allDiffs);
            }
            else
            {
                allDiffs.Add($"[{name}] critical-constraints: expected file missing: {ccPath}");
            }
        }

        if (allDiffs.Count == 0)
        {
            Console.WriteLine($"PASS: all conformance checks passed ({scenarioFiles.Count} scenario(s))");
            return 0;
        }

        Console.WriteLine($"FAIL: {allDiffs.Count} conformance difference(s):");
        foreach (var d in allDiffs)
            Console.WriteLine($"  {d}");
        return 1;
    }

    private static void CompareCandidates(
        JsonElement expList,
        System.Collections.Immutable.ImmutableArray<CandidateM> actual,
        string scenario,
        List<string> diffs)
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
            // alternativeIds (exact)
            var expIds = exp.GetProperty("alternativeIds").EnumerateArray().Select(e => e.GetString()!).ToList();
            var actIds = act.AlternativeIds.ToList();
            if (!expIds.SequenceEqual(actIds))
                diffs.Add($"[{scenario}] candidates[{i}].alternativeIds: expected={string.Join(",", expIds)}, actual={string.Join(",", actIds)}");

            // triangularValue
            var tv = exp.GetProperty("triangularValue");
            CheckDouble(tv.GetProperty("lower").GetDouble(), act.TriangularValue.Lower, $"[{scenario}] candidates[{i}].triangularValue.lower", diffs);
            CheckDouble(tv.GetProperty("modal").GetDouble(), act.TriangularValue.Modal, $"[{scenario}] candidates[{i}].triangularValue.modal", diffs);
            CheckDouble(tv.GetProperty("upper").GetDouble(), act.TriangularValue.Upper, $"[{scenario}] candidates[{i}].triangularValue.upper", diffs);

            // normalizedValue
            var nv = exp.GetProperty("normalizedValue");
            CheckDouble(nv.GetProperty("positive").GetDouble(), act.NormalizedValue.Positive, $"[{scenario}] candidates[{i}].normalizedValue.positive", diffs);
            CheckDouble(nv.GetProperty("average").GetDouble(), act.NormalizedValue.Average, $"[{scenario}] candidates[{i}].normalizedValue.average", diffs);
            CheckDouble(nv.GetProperty("negative").GetDouble(), act.NormalizedValue.Negative, $"[{scenario}] candidates[{i}].normalizedValue.negative", diffs);

            // score
            CheckDouble(exp.GetProperty("score").GetDouble(), act.Score, $"[{scenario}] candidates[{i}].score", diffs);

            // rank (exact)
            int expRank = exp.GetProperty("rank").GetInt32();
            if (expRank != act.Rank)
                diffs.Add($"[{scenario}] candidates[{i}].rank: expected={expRank}, actual={act.Rank}");

            i++;
        }
    }

    private static void CompareCriticalDecisions(
        JsonElement expList,
        System.Collections.Immutable.ImmutableArray<CriticalDecisionM> actual,
        string scenario,
        List<string> diffs)
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
                diffs.Add($"[{scenario}] critical-decisions[{i}].decisionId: expected={expId}, actual={act.DecisionId}");

            var tv = exp.GetProperty("triangularValue");
            CheckDouble(tv.GetProperty("lower").GetDouble(), act.TriangularValue.Lower, $"[{scenario}] critical-decisions[{i}].triangularValue.lower", diffs);
            CheckDouble(tv.GetProperty("modal").GetDouble(), act.TriangularValue.Modal, $"[{scenario}] critical-decisions[{i}].triangularValue.modal", diffs);
            CheckDouble(tv.GetProperty("upper").GetDouble(), act.TriangularValue.Upper, $"[{scenario}] critical-decisions[{i}].triangularValue.upper", diffs);

            var nv = exp.GetProperty("normalizedValue");
            CheckDouble(nv.GetProperty("positive").GetDouble(), act.NormalizedValue.Positive, $"[{scenario}] critical-decisions[{i}].normalizedValue.positive", diffs);
            CheckDouble(nv.GetProperty("average").GetDouble(), act.NormalizedValue.Average, $"[{scenario}] critical-decisions[{i}].normalizedValue.average", diffs);
            CheckDouble(nv.GetProperty("negative").GetDouble(), act.NormalizedValue.Negative, $"[{scenario}] critical-decisions[{i}].normalizedValue.negative", diffs);

            CheckDouble(exp.GetProperty("score").GetDouble(), act.Score, $"[{scenario}] critical-decisions[{i}].score", diffs);

            int expRank = exp.GetProperty("rank").GetInt32();
            if (expRank != act.Rank)
                diffs.Add($"[{scenario}] critical-decisions[{i}].rank: expected={expRank}, actual={act.Rank}");

            i++;
        }
    }

    private static void CompareCriticalConstraints(
        JsonElement expList,
        System.Collections.Immutable.ImmutableArray<CriticalConstraintM> actual,
        string scenario,
        List<string> diffs)
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
                diffs.Add($"[{scenario}] critical-constraints[{i}].constraintIndex: expected={expIdx}, actual={act.ConstraintIndex}");

            string expKind = exp.GetProperty("kind").GetString()!;
            if (expKind != act.Kind)
                diffs.Add($"[{scenario}] critical-constraints[{i}].kind: expected={expKind}, actual={act.Kind}");

            int expElim = exp.GetProperty("eliminated").GetInt32();
            if (expElim != act.Eliminated)
                diffs.Add($"[{scenario}] critical-constraints[{i}].eliminated: expected={expElim}, actual={act.Eliminated}");

            int expTotal = exp.GetProperty("total").GetInt32();
            if (expTotal != act.Total)
                diffs.Add($"[{scenario}] critical-constraints[{i}].total: expected={expTotal}, actual={act.Total}");

            bool expRedundant = exp.GetProperty("redundant").GetBoolean();
            if (expRedundant != act.Redundant)
                diffs.Add($"[{scenario}] critical-constraints[{i}].redundant: expected={expRedundant}, actual={act.Redundant}");

            i++;
        }
    }

    private static void CheckDouble(double expected, double actual, string label, List<string> diffs)
    {
        if (Math.Abs(expected - actual) > AbsTol)
            diffs.Add($"{label}: expected={expected:R}, actual={actual:R}, diff={Math.Abs(expected - actual):E3}");
    }

    private static string FindSpecDir()
    {
        // Walk up from AppContext.BaseDirectory to find spec/conformance
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (int i = 0; i < 15; i++)
        {
            var candidate = Path.Combine(dir.FullName, "spec", "conformance");
            if (Directory.Exists(candidate))
                return candidate;
            if (dir.Parent is null) break;
            dir = dir.Parent;
        }
        throw new InvalidOperationException(
            $"Cannot locate spec/conformance from {AppContext.BaseDirectory}. " +
            "Run from within the GuideArch repo.");
    }
}
