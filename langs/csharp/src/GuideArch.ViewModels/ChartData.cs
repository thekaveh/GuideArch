using System.Collections.Immutable;
using GuideArch.Models;

namespace GuideArch.ViewModels;

/// <summary>
/// Pure data-preparation helpers that transform solved scenario state into
/// arrays the View can pass directly to ScottPlot (or any chart library).
/// These helpers are in ViewModels (not View) so they are unit-testable
/// without a UI thread.
/// </summary>
public static class ChartData
{
    // -----------------------------------------------------------------------
    // Chart A — ranked candidates horizontal bar chart (spec charts.md §2)
    // -----------------------------------------------------------------------

    /// <summary>
    /// One entry per candidate bar in Chart A.
    /// </summary>
    public sealed record RankedCandidateBar(
        int Rank,
        double Score,
        /// <summary>Display label: "#{rank} ({score:G6})"</summary>
        string Label,
        /// <summary>Opacity factor [0.5, 1.0], full at rank 0, half at rank topN-1.</summary>
        double OpacityFactor,
        ImmutableArray<string> AlternativeIds
    );

    /// <summary>
    /// Builds the bar-chart input for the top <paramref name="topN"/> candidates.
    /// Candidates are already ranked (rank 0 = best). Result preserves that order
    /// so the caller can plot index → Y position directly.
    /// </summary>
    public static ImmutableArray<RankedCandidateBar> PrepRankedCandidates(
        ImmutableArray<CandidateM> candidates,
        int topN = 30)
    {
        var slice = candidates.Length <= topN ? candidates : candidates.Take(topN).ToImmutableArray();
        int n = slice.Length;

        var builder = ImmutableArray.CreateBuilder<RankedCandidateBar>(n);
        for (int i = 0; i < n; i++)
        {
            var c = slice[i];
            double opacityFactor = n <= 1
                ? 1.0
                : 1.0 - 0.5 * ((double)i / (n - 1)); // 1.0 → 0.5 as rank increases

            builder.Add(new RankedCandidateBar(
                Rank: c.Rank,
                Score: c.Score,
                Label: $"#{c.Rank} ({c.Score:G6})",
                OpacityFactor: opacityFactor,
                AlternativeIds: c.AlternativeIds
            ));
        }

        return builder.ToImmutable();
    }

    // -----------------------------------------------------------------------
    // Chart B — fuzzy triangle series for the selected candidate (spec §3)
    // -----------------------------------------------------------------------

    /// <summary>
    /// One triangle series per property, describing the triangular fuzzy membership
    /// function for that property's contribution to the selected candidate.
    /// X = value (Lower, Modal, Upper); Y = membership (0, 1, 0).
    /// </summary>
    public sealed record TriangleSeries(
        string PropertyId,
        string PropertyName,
        double Lower,
        double Modal,
        double Upper,
        /// <summary>X values for the triangle outline: [Lower, Modal, Upper, Lower] (closed).</summary>
        double[] Xs,
        /// <summary>Y values for the triangle outline: [0, 1, 0, 0] (closed).</summary>
        double[] Ys
    );

    /// <summary>
    /// Builds the triangle series input for Chart B, one series per property.
    /// Returns an empty array if no candidate is selected or no properties exist.
    /// </summary>
    public static ImmutableArray<TriangleSeries> PrepTriangleSeries(
        CandidateM candidate,
        ScenarioM scenario)
    {
        // We need to extract per-property triangular values.
        // CandidateM.TriangularValue is the aggregate; to get per-property breakdown
        // we use the coefficients for each alternative in the candidate.
        var altIds = candidate.AlternativeIds.ToHashSet();
        var properties = scenario.Properties;

        var builder = ImmutableArray.CreateBuilder<TriangleSeries>(properties.Length);

        foreach (var prop in properties)
        {
            // Find the coefficient for this candidate's alternative for this property.
            // Each candidate has exactly one alternative per decision.
            // Properties are orthogonal — we sum up contributions across alternatives.
            double sumLower = 0, sumModal = 0, sumUpper = 0;
            bool anyCoeff = false;

            foreach (var altId in candidate.AlternativeIds)
            {
                var coeff = scenario.Coefficients.FirstOrDefault(
                    c => c.AlternativeId == altId && c.PropertyId == prop.Id);
                if (coeff is not null)
                {
                    sumLower += coeff.Value.Lower;
                    sumModal += coeff.Value.Modal;
                    sumUpper += coeff.Value.Upper;
                    anyCoeff = true;
                }
            }

            if (!anyCoeff) continue;

            builder.Add(new TriangleSeries(
                PropertyId: prop.Id,
                PropertyName: prop.Name,
                Lower: sumLower,
                Modal: sumModal,
                Upper: sumUpper,
                Xs: new[] { sumLower, sumModal, sumUpper, sumLower },
                Ys: new[] { 0.0, 1.0, 0.0, 0.0 }
            ));
        }

        return builder.ToImmutable();
    }

    // -----------------------------------------------------------------------
    // Critical decisions display helpers (spec §4)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Display row for the critical-decisions DataGrid.
    /// </summary>
    public sealed record CriticalDecisionRow(
        int Rank,
        string DecisionName,
        string Score,
        string TriangularValue,
        string Normalized
    );

    /// <summary>
    /// Converts <paramref name="critDecisions"/> into display rows, resolving
    /// decision names from the scenario. Sorted ascending by rank.
    /// </summary>
    public static ImmutableArray<CriticalDecisionRow> PrepCriticalDecisions(
        ImmutableArray<CriticalDecisionM> critDecisions,
        ScenarioM scenario)
    {
        var decisionMap = scenario.Decisions.ToDictionary(d => d.Id, d => d.Name);

        return critDecisions
            .OrderBy(cd => cd.Rank)
            .Select(cd =>
            {
                decisionMap.TryGetValue(cd.DecisionId, out var name);
                return new CriticalDecisionRow(
                    Rank: cd.Rank,
                    DecisionName: name ?? cd.DecisionId,
                    Score: cd.Score.ToString("G6"),
                    TriangularValue: $"({cd.TriangularValue.Lower:G4}, {cd.TriangularValue.Modal:G4}, {cd.TriangularValue.Upper:G4})",
                    Normalized: $"({cd.NormalizedValue.Positive:G4}, {cd.NormalizedValue.Average:G4}, {cd.NormalizedValue.Negative:G4})"
                );
            })
            .ToImmutableArray();
    }

    // -----------------------------------------------------------------------
    // Critical constraints display helpers (spec §5)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Display row for the critical-constraints DataGrid.
    /// </summary>
    public sealed record CriticalConstraintRow(
        int Index,
        string Kind,
        int Eliminated,
        int Total,
        string EliminatedPercent,
        bool Redundant
    )
    {
        /// <summary>"Yes" when redundant, "No" otherwise — ready to display in grid.</summary>
        public string RedundantDisplay => Redundant ? "Yes" : "No";
    }

    /// <summary>
    /// Converts <paramref name="critConstraints"/> into display rows.
    /// Sorted descending by eliminated (most-binding first).
    /// </summary>
    public static ImmutableArray<CriticalConstraintRow> PrepCriticalConstraints(
        ImmutableArray<CriticalConstraintM> critConstraints)
    {
        return critConstraints
            .OrderByDescending(cc => cc.Eliminated)
            .Select(cc => new CriticalConstraintRow(
                Index: cc.ConstraintIndex,
                Kind: cc.Kind,
                Eliminated: cc.Eliminated,
                Total: cc.Total,
                EliminatedPercent: cc.Total > 0
                    ? $"{100.0 * cc.Eliminated / cc.Total:F1}%"
                    : "N/A",
                Redundant: cc.Redundant
            ))
            .ToImmutableArray();
    }
}
