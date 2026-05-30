using System.Text.Json;
using System.Text.Json.Nodes;
using GuideArch.Models;

namespace GuideArch.ViewModels;

/// <summary>
/// Serializes a <see cref="ScenarioM"/> to the exact JSON format that
/// <see cref="ScenarioLoader"/> can round-trip back in.
/// The format is schema-compliant per spec/domain/scenario.schema.json.
/// </summary>
public static class ScenarioSerializer
{
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>
    /// Converts a <see cref="ScenarioM"/> to an indented JSON string.
    /// </summary>
    public static string Serialize(ScenarioM scenario)
    {
        var root = new JsonObject
        {
            ["schemaVersion"] = scenario.SchemaVersion,
            ["name"] = scenario.Name,
            ["description"] = scenario.Description,
            ["decisions"] = DecisionsToJson(scenario.Decisions),
            ["alternatives"] = AlternativesToJson(scenario.Alternatives),
            ["properties"] = PropertiesToJson(scenario.Properties),
            ["coefficients"] = CoefficientsToJson(scenario.Coefficients),
            ["constraints"] = ConstraintsToJson(scenario.Constraints),
            ["config"] = ConfigToJson(scenario.Config)
        };

        return root.ToJsonString(WriteOptions);
    }

    private static JsonArray DecisionsToJson(System.Collections.Immutable.ImmutableArray<DecisionM> decisions)
    {
        var arr = new JsonArray();
        foreach (var d in decisions)
        {
            arr.Add(new JsonObject
            {
                ["id"] = d.Id,
                ["name"] = d.Name
            });
        }
        return arr;
    }

    private static JsonArray AlternativesToJson(System.Collections.Immutable.ImmutableArray<AlternativeM> alternatives)
    {
        var arr = new JsonArray();
        foreach (var a in alternatives)
        {
            arr.Add(new JsonObject
            {
                ["id"] = a.Id,
                ["decisionId"] = a.DecisionId,
                ["name"] = a.Name
            });
        }
        return arr;
    }

    private static JsonArray PropertiesToJson(System.Collections.Immutable.ImmutableArray<PropertyM> properties)
    {
        var arr = new JsonArray();
        foreach (var p in properties)
        {
            arr.Add(new JsonObject
            {
                ["id"] = p.Id,
                ["name"] = p.Name,
                ["kind"] = p.Kind == PropertyKind.Min ? "min" : "max",
                ["weight"] = p.Weight
            });
        }
        return arr;
    }

    private static JsonArray CoefficientsToJson(System.Collections.Immutable.ImmutableArray<CoefficientM> coefficients)
    {
        var arr = new JsonArray();
        foreach (var c in coefficients)
        {
            arr.Add(new JsonObject
            {
                ["alternativeId"] = c.AlternativeId,
                ["propertyId"] = c.PropertyId,
                ["value"] = new JsonObject
                {
                    ["lower"] = c.Value.Lower,
                    ["modal"] = c.Value.Modal,
                    ["upper"] = c.Value.Upper
                }
            });
        }
        return arr;
    }

    private static JsonArray ConstraintsToJson(System.Collections.Immutable.ImmutableArray<ConstraintM> constraints)
    {
        var arr = new JsonArray();
        foreach (var c in constraints)
        {
            JsonObject obj;
            switch (c)
            {
                case ThresholdConstraintM tc:
                    obj = new JsonObject { ["kind"] = "threshold", ["propertyId"] = tc.PropertyId };
                    if (tc.Min.HasValue) obj["min"] = tc.Min.Value;
                    if (tc.Max.HasValue) obj["max"] = tc.Max.Value;
                    break;
                case DependencyConstraintM dc:
                    obj = new JsonObject
                    {
                        ["kind"] = "dependency",
                        ["sourceAlternativeId"] = dc.SourceAlternativeId,
                        ["targetAlternativeId"] = dc.TargetAlternativeId
                    };
                    break;
                case ConflictConstraintM cc:
                    obj = new JsonObject
                    {
                        ["kind"] = "conflict",
                        ["alternativeAId"] = cc.AlternativeAId,
                        ["alternativeBId"] = cc.AlternativeBId
                    };
                    break;
                default:
                    continue; // Unknown constraint type — skip.
            }
            arr.Add(obj);
        }
        return arr;
    }

    private static JsonObject ConfigToJson(ConfigM config)
    {
        return new JsonObject
        {
            ["aggregation"] = config.Aggregation == Aggregation.Sum ? "sum" : "max",
            ["weights"] = new JsonObject
            {
                ["positive"] = config.Weights.Positive,
                ["average"] = config.Weights.Average,
                ["negative"] = config.Weights.Negative
            }
        };
    }
}
