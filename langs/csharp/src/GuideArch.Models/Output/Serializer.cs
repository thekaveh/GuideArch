using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GuideArch.Models.Output;

/// <summary>
/// Deterministic JSON serialization for TOPSIS outputs.
/// Uses System.Text.Json with full precision (round-trip) floating-point.
/// Field ordering matches Python expected files (insertion order in property dicts).
/// </summary>
public static class Serializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        NumberHandling = JsonNumberHandling.Strict,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    // -----------------------------------------------------------------------
    // Candidates  (field order: alternativeIds, triangularValue, normalizedValue, score, rank)
    // -----------------------------------------------------------------------

    public static string SerializeCandidates(ImmutableArray<CandidateM> candidates)
    {
        var list = candidates.Select(c => new
        {
            alternativeIds = c.AlternativeIds.ToArray(),
            triangularValue = new { lower = c.TriangularValue.Lower, modal = c.TriangularValue.Modal, upper = c.TriangularValue.Upper },
            normalizedValue = new { positive = c.NormalizedValue.Positive, average = c.NormalizedValue.Average, negative = c.NormalizedValue.Negative },
            score = c.Score,
            rank = c.Rank,
        });
        return JsonSerializer.Serialize(list, Options);
    }

    public static List<System.Text.Json.Nodes.JsonObject> CandidatesToObjects(ImmutableArray<CandidateM> candidates)
    {
        return candidates.Select(c =>
        {
            var obj = new System.Text.Json.Nodes.JsonObject();
            var ids = new System.Text.Json.Nodes.JsonArray();
            foreach (var id in c.AlternativeIds) ids.Add(id);
            obj["alternativeIds"] = ids;
            obj["triangularValue"] = TfmToNode(c.TriangularValue);
            obj["normalizedValue"] = NfmToNode(c.NormalizedValue);
            obj["score"] = c.Score;
            obj["rank"] = c.Rank;
            return obj;
        }).ToList();
    }

    // -----------------------------------------------------------------------
    // Critical decisions
    // -----------------------------------------------------------------------

    public static string SerializeCriticalDecisions(ImmutableArray<CriticalDecisionM> decisions)
    {
        var list = decisions.Select(d => new
        {
            decisionId = d.DecisionId,
            triangularValue = new { lower = d.TriangularValue.Lower, modal = d.TriangularValue.Modal, upper = d.TriangularValue.Upper },
            normalizedValue = new { positive = d.NormalizedValue.Positive, average = d.NormalizedValue.Average, negative = d.NormalizedValue.Negative },
            score = d.Score,
            rank = d.Rank,
        });
        return JsonSerializer.Serialize(list, Options);
    }

    public static List<System.Text.Json.Nodes.JsonObject> CriticalDecisionsToObjects(ImmutableArray<CriticalDecisionM> decisions)
    {
        return decisions.Select(d =>
        {
            var obj = new System.Text.Json.Nodes.JsonObject();
            obj["decisionId"] = d.DecisionId;
            obj["triangularValue"] = TfmToNode(d.TriangularValue);
            obj["normalizedValue"] = NfmToNode(d.NormalizedValue);
            obj["score"] = d.Score;
            obj["rank"] = d.Rank;
            return obj;
        }).ToList();
    }

    // -----------------------------------------------------------------------
    // Critical constraints
    // -----------------------------------------------------------------------

    public static string SerializeCriticalConstraints(ImmutableArray<CriticalConstraintM> constraints)
    {
        var list = constraints.Select(c => new
        {
            constraintIndex = c.ConstraintIndex,
            kind = c.Kind,
            eliminated = c.Eliminated,
            total = c.Total,
            redundant = c.Redundant,
        });
        return JsonSerializer.Serialize(list, Options);
    }

    public static List<System.Text.Json.Nodes.JsonObject> CriticalConstraintsToObjects(ImmutableArray<CriticalConstraintM> constraints)
    {
        return constraints.Select(c =>
        {
            var obj = new System.Text.Json.Nodes.JsonObject();
            obj["constraintIndex"] = c.ConstraintIndex;
            obj["kind"] = c.Kind;
            obj["eliminated"] = c.Eliminated;
            obj["total"] = c.Total;
            obj["redundant"] = c.Redundant;
            return obj;
        }).ToList();
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static System.Text.Json.Nodes.JsonObject TfmToNode(TriangularFuzzyM t)
    {
        var obj = new System.Text.Json.Nodes.JsonObject();
        obj["lower"] = t.Lower;
        obj["modal"] = t.Modal;
        obj["upper"] = t.Upper;
        return obj;
    }

    private static System.Text.Json.Nodes.JsonObject NfmToNode(NormalizedFuzzyM n)
    {
        var obj = new System.Text.Json.Nodes.JsonObject();
        obj["positive"] = n.Positive;
        obj["average"] = n.Average;
        obj["negative"] = n.Negative;
        return obj;
    }
}
