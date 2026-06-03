using System.Collections.Immutable;

namespace GuideArch.Models.Topsis;

/// <summary>
/// Full TOPSIS pipeline — implements topsis.md §3 step-by-step.
/// Every numbered comment maps to the corresponding §3.x step.
/// </summary>
public static class Solver
{
    public static ImmutableArray<CandidateM> Solve(ScenarioM scenario)
    {
        // ------------------------------------------------------------------
        // §3.1  Enumerate raw candidates
        // ------------------------------------------------------------------
        var altsByDec = new Dictionary<string, List<string>>();
        foreach (var a in scenario.Alternatives)
        {
            if (!altsByDec.TryGetValue(a.DecisionId, out var list))
            {
                list = new List<string>();
                altsByDec[a.DecisionId] = list;
            }
            list.Add(a.Id);
        }

        var pools = scenario.Decisions
            .Select(d => altsByDec.TryGetValue(d.Id, out var l) ? l : new List<string>())
            .ToList();

        var rawCandidates = CartesianProduct(pools);

        if (rawCandidates.Count == 0)
            return ImmutableArray<CandidateM>.Empty;

        // ------------------------------------------------------------------
        // §3.2  Filter candidates (dependency → conflict → threshold)
        // ------------------------------------------------------------------
        var depConstraints = scenario.Constraints.OfType<DependencyConstraintM>().ToList();
        var confConstraints = scenario.Constraints.OfType<ConflictConstraintM>().ToList();
        var threshConstraints = scenario.Constraints.OfType<ThresholdConstraintM>().ToList();

        var coeff = BuildCoeffLookup(scenario);
        var propMap = scenario.Properties.ToDictionary(p => p.Id, p => p);

        var feasible = rawCandidates.Where(c => PassesFilters(c, depConstraints, confConstraints, threshConstraints, coeff, propMap)).ToList();

        if (feasible.Count == 0)
            return ImmutableArray<CandidateM>.Empty;

        // ------------------------------------------------------------------
        // §3.4  Per-property normalizer (over original alternative pool)
        // ------------------------------------------------------------------
        var M = ComputeNormalizer(scenario);

        // ------------------------------------------------------------------
        // §3.5  Total triangular value per candidate
        // ------------------------------------------------------------------
        var totalValues = feasible.Select(c => CandidateTotalValue(c, scenario, coeff, M)).ToList();

        // ------------------------------------------------------------------
        // §3.6  Convert to Z-space
        // ------------------------------------------------------------------
        var zValues = totalValues.Select(ToZ).ToList();

        // ------------------------------------------------------------------
        // §3.7–3.8  PIS/NIS normalization
        // ------------------------------------------------------------------
        var normValues = NormalizeCandidates(zValues);

        // ------------------------------------------------------------------
        // §3.9–3.10  Score, sort, rank
        // ------------------------------------------------------------------
        var scores = normValues.Select(n => Score(n, scenario.Config)).ToList();

        // Sort: primary = score ascending; secondary = alternativeIds lexicographic
        var indexed = Enumerable.Range(0, feasible.Count)
            .OrderBy(i => scores[i])
            .ThenBy(i => feasible[i], StringArrayComparer.Instance)
            .ToList();

        var builder = ImmutableArray.CreateBuilder<CandidateM>(indexed.Count);
        for (int rank = 0; rank < indexed.Count; rank++)
        {
            int i = indexed[rank];
            builder.Add(new CandidateM(
                AlternativeIds: ImmutableArray.CreateRange(feasible[i]),
                TriangularValue: totalValues[i],
                NormalizedValue: normValues[i],
                Score: scores[i],
                Rank: rank
            ));
        }
        return builder.MoveToImmutable();
    }

    // -----------------------------------------------------------------------
    // Internal helpers — mirrors Python topsis/solve.py exactly
    // -----------------------------------------------------------------------

    internal static Dictionary<(string AltId, string PropId), TriangularFuzzyM> BuildCoeffLookup(ScenarioM scenario)
    {
        var d = new Dictionary<(string, string), TriangularFuzzyM>(scenario.Coefficients.Length);
        foreach (var c in scenario.Coefficients)
            d[(c.AlternativeId, c.PropertyId)] = c.Value;
        return d;
    }

    /// <summary>Per-property normalizer M(pₖ) — topsis.md §3.4.</summary>
    internal static Dictionary<string, double> ComputeNormalizer(ScenarioM scenario)
    {
        var coeff = BuildCoeffLookup(scenario);

        var altsByDec = new Dictionary<string, List<string>>();
        foreach (var a in scenario.Alternatives)
        {
            if (!altsByDec.TryGetValue(a.DecisionId, out var list))
            {
                list = new List<string>();
                altsByDec[a.DecisionId] = list;
            }
            list.Add(a.Id);
        }

        var M = new Dictionary<string, double>(scenario.Properties.Length);
        foreach (var p in scenario.Properties)
        {
            double total = 0.0;
            foreach (var d in scenario.Decisions)
            {
                if (!altsByDec.TryGetValue(d.Id, out var groupAlts) || groupAlts.Count == 0)
                    continue;
                double best;
                if (p.Kind == PropertyKind.Max)
                    best = groupAlts.Max(aId => coeff[(aId, p.Id)].Upper);
                else
                    best = groupAlts.Max(aId => coeff[(aId, p.Id)].Lower);
                total += best;
            }
            M[p.Id] = total;
        }
        return M;
    }

    /// <summary>
    /// Per-alternative contribution — topsis.md §3.5, §5.
    /// sign(p) = +1 for min, -1 for max.
    /// </summary>
    internal static TriangularFuzzyM AltContribution(
        string altId,
        ScenarioM scenario,
        Dictionary<(string, string), TriangularFuzzyM> coeff,
        Dictionary<string, double> M)
    {
        var result = TriangularFuzzyM.Zero;
        foreach (var p in scenario.Properties)
        {
            double mp = M[p.Id];
            if (mp == 0.0)
            {
                // Invariant 10.1: degenerate — emit warning, skip. Parity
                // with Python (warnings.warn) and TS (console.warn) both of
                // which land on stderr; same message format for log-scrapers
                // that look across impls.
                Console.Error.WriteLine(
                    $"Property '{p.Id}' has M=0; skipping to avoid division by zero");
                continue;
            }
            double sign = p.Kind == PropertyKind.Min ? 1.0 : -1.0;
            var contribution = coeff[(altId, p.Id)] * (sign * p.Weight) / mp;
            result = result + contribution;
        }
        return result;
    }

    private static TriangularFuzzyM CandidateTotalValue(
        List<string> candidate,
        ScenarioM scenario,
        Dictionary<(string, string), TriangularFuzzyM> coeff,
        Dictionary<string, double> M)
    {
        var result = TriangularFuzzyM.Zero;
        foreach (var altId in candidate)
            result = result + AltContribution(altId, scenario, coeff, M);
        return result;
    }

    /// <summary>Convert TriangularFuzzy to Z-space — topsis.md §3.6.</summary>
    internal static NormalizedFuzzyM ToZ(TriangularFuzzyM t)
        => new(
            Positive: Math.Abs(t.Modal - t.Lower),
            Average: t.Modal,
            Negative: Math.Abs(t.Upper - t.Modal)
        );

    /// <summary>Compute PIS/NIS in Z-space and normalize — topsis.md §3.7, §3.8.</summary>
    internal static List<NormalizedFuzzyM> NormalizeCandidates(List<NormalizedFuzzyM> zValues)
    {
        if (zValues.Count == 0)
            return new List<NormalizedFuzzyM>();

        // §3.7 PIS and NIS
        double pisAvg = zValues.Min(z => z.Average);
        double pisPos = zValues.Max(z => z.Positive);
        double pisNeg = zValues.Min(z => z.Negative);

        double nisAvg = zValues.Max(z => z.Average);
        double nisPos = zValues.Min(z => z.Positive);
        double nisNeg = zValues.Max(z => z.Negative);

        var normalized = new List<NormalizedFuzzyM>(zValues.Count);
        foreach (var z in zValues)
        {
            // §3.8 — clip01 and 0/0 → 0
            double denomAvg = nisAvg - pisAvg;
            double nAvg = denomAvg != 0.0 ? Clip01((z.Average - pisAvg) / denomAvg) : 0.0;

            double denomPos = pisPos - nisPos;
            double nPos = denomPos != 0.0 ? Clip01((pisPos - z.Positive) / denomPos) : 0.0;

            double denomNeg = nisNeg - pisNeg;
            double nNeg = denomNeg != 0.0 ? Clip01((z.Negative - pisNeg) / denomNeg) : 0.0;

            normalized.Add(new NormalizedFuzzyM(Positive: nPos, Average: nAvg, Negative: nNeg));
        }
        return normalized;
    }

    /// <summary>Per-candidate score — topsis.md §3.9, §3.10.</summary>
    internal static double Score(NormalizedFuzzyM n, ConfigM config)
    {
        double phiPos = config.Weights.Positive * n.Positive;
        double phiAvg = config.Weights.Average * n.Average;
        double phiNeg = config.Weights.Negative * n.Negative;
        return config.Aggregation == Aggregation.Sum
            ? phiPos + phiAvg + phiNeg
            : Math.Max(Math.Max(phiPos, phiAvg), phiNeg);
    }

    private static double Clip01(double x) => Math.Max(0.0, Math.Min(1.0, x));

    private static bool PassesFilters(
        List<string> candidate,
        List<DependencyConstraintM> deps,
        List<ConflictConstraintM> confs,
        List<ThresholdConstraintM> thresholds,
        Dictionary<(string, string), TriangularFuzzyM> coeff,
        Dictionary<string, PropertyM> propMap)
    {
        var cSet = new HashSet<string>(candidate);

        // 1. Dependency (biconditional) — topsis.md §3.2 step 1
        foreach (var dc in deps)
        {
            bool srcIn = cSet.Contains(dc.SourceAlternativeId);
            bool tgtIn = cSet.Contains(dc.TargetAlternativeId);
            if (srcIn != tgtIn) // XOR — violates biconditional
                return false;
        }

        // 2. Conflict — topsis.md §3.2 step 2
        foreach (var cc in confs)
        {
            if (cSet.Contains(cc.AlternativeAId) && cSet.Contains(cc.AlternativeBId))
                return false;
        }

        // 3. Threshold — topsis.md §3.2 step 3
        foreach (var tc in thresholds)
        {
            var contrib = TriangularFuzzyM.Zero;
            foreach (var aId in candidate)
                contrib = contrib + coeff[(aId, tc.PropertyId)];
            double defuzzed = contrib.Lower; // §4.2: lower vertex only
            var prop = propMap[tc.PropertyId];
            if (prop.Kind == PropertyKind.Min)
            {
                if (tc.Max.HasValue && defuzzed > tc.Max.Value)
                    return false;
            }
            else // max
            {
                if (tc.Min.HasValue && defuzzed < tc.Min.Value)
                    return false;
            }
        }

        return true;
    }

    private static List<List<string>> CartesianProduct(List<List<string>> pools)
    {
        var result = new List<List<string>> { new List<string>() };
        foreach (var pool in pools)
        {
            var next = new List<List<string>>(result.Count * pool.Count);
            foreach (var existing in result)
            {
                foreach (var item in pool)
                {
                    var combo = new List<string>(existing) { item };
                    next.Add(combo);
                }
            }
            result = next;
        }
        // Return empty if any pool was empty (no candidates possible)
        if (pools.Any(p => p.Count == 0))
            return new List<List<string>>();
        return result;
    }

    /// <summary>Lexicographic comparison of string lists for tie-breaking.</summary>
    private sealed class StringArrayComparer : IComparer<List<string>>
    {
        public static readonly StringArrayComparer Instance = new();
        public int Compare(List<string>? x, List<string>? y)
        {
            if (x is null && y is null) return 0;
            if (x is null) return -1;
            if (y is null) return 1;
            int minLen = Math.Min(x.Count, y.Count);
            for (int i = 0; i < minLen; i++)
            {
                int cmp = string.Compare(x[i], y[i], StringComparison.Ordinal);
                if (cmp != 0) return cmp;
            }
            return x.Count.CompareTo(y.Count);
        }
    }
}
