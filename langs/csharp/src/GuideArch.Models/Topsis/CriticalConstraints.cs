using System.Collections.Immutable;

namespace GuideArch.Models.Topsis;

/// <summary>
/// Critical constraints analysis — topsis.md §6.
/// For each constraint, counts how many unconstrained candidates it eliminates.
/// </summary>
public static class CriticalConstraints
{
    public static ImmutableArray<CriticalConstraintM> Analyze(ScenarioM scenario)
    {
        if (scenario.Constraints.Length == 0)
            return ImmutableArray<CriticalConstraintM>.Empty;

        // Build unconstrained Cartesian product
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

        var unconstrained = CartesianProduct(pools);
        int total = unconstrained.Count;

        var coeff = Solver.BuildCoeffLookup(scenario);
        var propKind = scenario.Properties.ToDictionary(p => p.Id, p => p.Kind);

        var builder = ImmutableArray.CreateBuilder<CriticalConstraintM>(scenario.Constraints.Length);
        for (int idx = 0; idx < scenario.Constraints.Length; idx++)
        {
            var constraint = scenario.Constraints[idx];
            int passed = unconstrained.Count(c => ApplySingleConstraint(c, constraint, coeff, propKind));
            int eliminated = total - passed;
            builder.Add(new CriticalConstraintM(
                ConstraintIndex: idx,
                Kind: constraint.Kind,
                Eliminated: eliminated,
                Total: total,
                Redundant: eliminated == 0
            ));
        }

        // Sort by eliminated descending (most-binding first)
        return builder.ToImmutable()
            .Sort((a, b) => b.Eliminated.CompareTo(a.Eliminated));
    }

    private static bool ApplySingleConstraint(
        List<string> candidate,
        ConstraintM constraint,
        Dictionary<(string, string), TriangularFuzzyM> coeff,
        Dictionary<string, PropertyKind> propKind)
    {
        var cSet = new HashSet<string>(candidate);

        if (constraint is DependencyConstraintM dc)
        {
            bool srcIn = cSet.Contains(dc.SourceAlternativeId);
            bool tgtIn = cSet.Contains(dc.TargetAlternativeId);
            return srcIn == tgtIn; // biconditional
        }

        if (constraint is ConflictConstraintM cc)
        {
            return !(cSet.Contains(cc.AlternativeAId) && cSet.Contains(cc.AlternativeBId));
        }

        if (constraint is ThresholdConstraintM tc)
        {
            var contrib = TriangularFuzzyM.Zero;
            foreach (var aId in candidate)
                contrib = contrib + coeff[(aId, tc.PropertyId)];
            double defuzzed = contrib.Lower; // §4.2: lower vertex only
            var kind = propKind[tc.PropertyId];
            if (kind == PropertyKind.Min)
            {
                if (tc.Max.HasValue && defuzzed > tc.Max.Value)
                    return false;
            }
            else // max
            {
                if (tc.Min.HasValue && defuzzed < tc.Min.Value)
                    return false;
            }
            return true;
        }

        return true;
    }

    private static List<List<string>> CartesianProduct(List<List<string>> pools)
    {
        var result = new List<List<string>> { new List<string>() };
        foreach (var pool in pools)
        {
            if (pool.Count == 0)
                return new List<List<string>>();
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
        return result;
    }
}
