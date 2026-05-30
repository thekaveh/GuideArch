using System.Collections.Immutable;

namespace GuideArch.Models.Topsis;

/// <summary>
/// Critical decisions analysis — topsis.md §5.
/// Aggregation is always 'max' (legacy hardcodes Max here, ignoring config.aggregation).
/// </summary>
public static class CriticalDecisions
{
    private const int TopN = 20;    // topsis.md §8
    private const double Decay = 0.1; // topsis.md §8 — exp(-0.1 * rank)

    public static ImmutableArray<CriticalDecisionM> Analyze(
        ScenarioM scenario,
        ImmutableArray<CandidateM>? candidates = null)
    {
        var resolved = candidates ?? Solver.Solve(scenario);

        if (resolved.Length == 0)
            return ImmutableArray<CriticalDecisionM>.Empty;

        // Take top-N ranked candidates
        var topN = resolved.Take(Math.Min(TopN, resolved.Length)).ToList();

        var coeff = Solver.BuildCoeffLookup(scenario);
        var M = Solver.ComputeNormalizer(scenario);

        // Map decision_id → index in scenario.decisions
        var decIndex = new Dictionary<string, int>();
        for (int i = 0; i < scenario.Decisions.Length; i++)
            decIndex[scenario.Decisions[i].Id] = i;

        // -----------------------------------------------------------------------
        // §5  contribution(di) = Σ over c in Rt : exp(-0.1*rank(c)) * altContrib(c[i])
        // -----------------------------------------------------------------------
        var decContributions = new Dictionary<string, TriangularFuzzyM>();
        foreach (var d in scenario.Decisions)
            decContributions[d.Id] = TriangularFuzzyM.Zero;

        foreach (var c in topN)
        {
            double weightFactor = Math.Exp(-Decay * c.Rank);
            foreach (var d in scenario.Decisions)
            {
                int dIdx = decIndex[d.Id];
                string altId = c.AlternativeIds[dIdx];
                var altContrib = Solver.AltContribution(altId, scenario, coeff, M);
                decContributions[d.Id] = decContributions[d.Id] + altContrib * weightFactor;
            }
        }

        // -----------------------------------------------------------------------
        // Convert to Z-space, compute PIS/NIS, normalize (§3.6–3.8)
        // but over the *decision* set, with aggregation = max.
        // -----------------------------------------------------------------------
        var decisionIds = scenario.Decisions.Select(d => d.Id).ToList();
        var triangularValues = decisionIds.Select(id => decContributions[id]).ToList();
        var zValues = triangularValues.Select(Solver.ToZ).ToList();

        if (zValues.Count == 0)
            return ImmutableArray<CriticalDecisionM>.Empty;

        // PIS/NIS over decision set — same as §3.7
        double pisAvg = zValues.Min(z => z.Average);
        double pisPos = zValues.Max(z => z.Positive);
        double pisNeg = zValues.Min(z => z.Negative);

        double nisAvg = zValues.Max(z => z.Average);
        double nisPos = zValues.Min(z => z.Positive);
        double nisNeg = zValues.Max(z => z.Negative);

        var normValues = new List<NormalizedFuzzyM>(zValues.Count);
        foreach (var z in zValues)
        {
            double denomAvg = nisAvg - pisAvg;
            double nAvg = denomAvg != 0.0 ? Clip01((z.Average - pisAvg) / denomAvg) : 0.0;

            double denomPos = pisPos - nisPos;
            double nPos = denomPos != 0.0 ? Clip01((pisPos - z.Positive) / denomPos) : 0.0;

            double denomNeg = nisNeg - pisNeg;
            double nNeg = denomNeg != 0.0 ? Clip01((z.Negative - pisNeg) / denomNeg) : 0.0;

            normValues.Add(new NormalizedFuzzyM(Positive: nPos, Average: nAvg, Negative: nNeg));
        }

        // Score with max aggregation (legacy hardcodes Max here — topsis.md §5)
        var weights = scenario.Config.Weights;
        var scores = normValues.Select(n =>
            Math.Max(
                Math.Max(weights.Positive * n.Positive, weights.Average * n.Average),
                weights.Negative * n.Negative
            )
        ).ToList();

        // Sort ascending by score, tie-break by decision_id lexicographic
        var indexed = Enumerable.Range(0, decisionIds.Count)
            .OrderBy(i => scores[i])
            .ThenBy(i => decisionIds[i], StringComparer.Ordinal)
            .ToList();

        var builder = ImmutableArray.CreateBuilder<CriticalDecisionM>(indexed.Count);
        for (int rank = 0; rank < indexed.Count; rank++)
        {
            int i = indexed[rank];
            builder.Add(new CriticalDecisionM(
                DecisionId: decisionIds[i],
                TriangularValue: triangularValues[i],
                NormalizedValue: normValues[i],
                Score: scores[i],
                Rank: rank
            ));
        }
        return builder.MoveToImmutable();
    }

    private static double Clip01(double x) => Math.Max(0.0, Math.Min(1.0, x));
}
