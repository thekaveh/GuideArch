using System.Collections.Immutable;
using GuideArch.Models;
using Xunit;

namespace GuideArch.Models.Tests;

/// <summary>
/// Tests for ScenarioLoader fatal invariant violations.
/// Each test constructs a minimal valid scenario in memory as a temp JSON file,
/// then mutates one field to trigger the relevant invariant.
/// </summary>
public class ScenarioLoaderTests
{
    private static string FindSchemaPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (int i = 0; i < 15; i++)
        {
            var candidate = Path.Combine(dir.FullName, "spec", "domain", "scenario.schema.json");
            if (File.Exists(candidate)) return candidate;
            if (dir.Parent is null) break;
            dir = dir.Parent;
        }
        throw new InvalidOperationException("Cannot locate scenario.schema.json");
    }

    private static string FindSasPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (int i = 0; i < 15; i++)
        {
            var candidate = Path.Combine(dir.FullName, "spec", "conformance", "scenarios", "sas.json");
            if (File.Exists(candidate)) return candidate;
            if (dir.Parent is null) break;
            dir = dir.Parent;
        }
        throw new InvalidOperationException("Cannot locate sas.json");
    }

    private static string MinimalValidJson() => """
        {
          "schemaVersion": "1.0.0",
          "name": "test",
          "decisions": [{"id": "d1", "name": "D1"}],
          "alternatives": [{"id": "a1", "decisionId": "d1", "name": "A1"}],
          "properties": [{"id": "p1", "name": "P1", "kind": "min", "weight": 1.0}],
          "coefficients": [{"alternativeId": "a1", "propertyId": "p1", "value": {"lower": 1, "modal": 2, "upper": 3}}],
          "constraints": [],
          "config": {
            "aggregation": "max",
            "weights": {"positive": 0.3333333333333333, "average": 0.3333333333333333, "negative": 0.3333333333333334}
          }
        }
        """;

    private static ScenarioM LoadFromString(string json, string? schemaPath = null)
    {
        var tmp = Path.GetTempFileName() + ".json";
        File.WriteAllText(tmp, json);
        try
        {
            return ScenarioLoader.Load(tmp, schemaPath ?? FindSchemaPath());
        }
        finally
        {
            File.Delete(tmp);
        }
    }

    [Fact]
    public void ValidMinimalScenario_Loads()
    {
        var scenario = LoadFromString(MinimalValidJson());
        Assert.Equal("test", scenario.Name);
        Assert.Single(scenario.Decisions);
        Assert.Single(scenario.Alternatives);
        Assert.Single(scenario.Properties);
    }

    [Fact]
    public void Invariant_1_1_DuplicateDecisionId_Throws()
    {
        var json = """
            {
              "schemaVersion": "1.0.0", "name": "t",
              "decisions": [{"id": "d1", "name": "D1"}, {"id": "d1", "name": "D1b"}],
              "alternatives": [{"id": "a1", "decisionId": "d1", "name": "A1"}],
              "properties": [{"id": "p1", "name": "P", "kind": "min", "weight": 1.0}],
              "coefficients": [{"alternativeId": "a1", "propertyId": "p1", "value": {"lower": 1, "modal": 2, "upper": 3}}],
              "constraints": [],
              "config": {"aggregation": "max", "weights": {"positive": 0.333333333, "average": 0.333333333, "negative": 0.333333334}}
            }
            """;
        var ex = Assert.Throws<ScenarioValidationException>(() => LoadFromString(json));
        Assert.Contains("1.1", ex.Message);
    }

    [Fact]
    public void Invariant_1_2_DuplicateAlternativeId_Throws()
    {
        var json = """
            {
              "schemaVersion": "1.0.0", "name": "t",
              "decisions": [{"id": "d1", "name": "D1"}],
              "alternatives": [
                {"id": "a1", "decisionId": "d1", "name": "A1"},
                {"id": "a1", "decisionId": "d1", "name": "A1b"}
              ],
              "properties": [{"id": "p1", "name": "P", "kind": "min", "weight": 1.0}],
              "coefficients": [{"alternativeId": "a1", "propertyId": "p1", "value": {"lower": 1, "modal": 2, "upper": 3}}],
              "constraints": [],
              "config": {"aggregation": "max", "weights": {"positive": 0.333333333, "average": 0.333333333, "negative": 0.333333334}}
            }
            """;
        var ex = Assert.Throws<ScenarioValidationException>(() => LoadFromString(json));
        Assert.Contains("1.2", ex.Message);
    }

    [Fact]
    public void Invariant_2_1_UnknownDecisionIdInAlternative_Throws()
    {
        var json = """
            {
              "schemaVersion": "1.0.0", "name": "t",
              "decisions": [{"id": "d1", "name": "D1"}],
              "alternatives": [{"id": "a1", "decisionId": "d-UNKNOWN", "name": "A1"}],
              "properties": [{"id": "p1", "name": "P", "kind": "min", "weight": 1.0}],
              "coefficients": [{"alternativeId": "a1", "propertyId": "p1", "value": {"lower": 1, "modal": 2, "upper": 3}}],
              "constraints": [],
              "config": {"aggregation": "max", "weights": {"positive": 0.333333333, "average": 0.333333333, "negative": 0.333333334}}
            }
            """;
        var ex = Assert.Throws<ScenarioValidationException>(() => LoadFromString(json));
        Assert.Contains("2.1", ex.Message);
    }

    [Fact]
    public void Invariant_3_1_MissingCoefficient_Throws()
    {
        var json = """
            {
              "schemaVersion": "1.0.0", "name": "t",
              "decisions": [{"id": "d1", "name": "D1"}],
              "alternatives": [
                {"id": "a1", "decisionId": "d1", "name": "A1"},
                {"id": "a2", "decisionId": "d1", "name": "A2"}
              ],
              "properties": [{"id": "p1", "name": "P", "kind": "min", "weight": 1.0}],
              "coefficients": [
                {"alternativeId": "a1", "propertyId": "p1", "value": {"lower": 1, "modal": 2, "upper": 3}}
              ],
              "constraints": [],
              "config": {"aggregation": "max", "weights": {"positive": 0.333333333, "average": 0.333333333, "negative": 0.333333334}}
            }
            """;
        var ex = Assert.Throws<ScenarioValidationException>(() => LoadFromString(json));
        Assert.Contains("3.1", ex.Message);
    }

    [Fact]
    public void Invariant_5_1_WeightsSumNot1_Throws()
    {
        var json = """
            {
              "schemaVersion": "1.0.0", "name": "t",
              "decisions": [{"id": "d1", "name": "D1"}],
              "alternatives": [{"id": "a1", "decisionId": "d1", "name": "A1"}],
              "properties": [{"id": "p1", "name": "P", "kind": "min", "weight": 1.0}],
              "coefficients": [{"alternativeId": "a1", "propertyId": "p1", "value": {"lower": 1, "modal": 2, "upper": 3}}],
              "constraints": [],
              "config": {"aggregation": "max", "weights": {"positive": 0.5, "average": 0.5, "negative": 0.5}}
            }
            """;
        var ex = Assert.Throws<ScenarioValidationException>(() => LoadFromString(json));
        Assert.Contains("5.1", ex.Message);
    }

    [Fact]
    public void Invariant_7_1_SelfEdgeDependency_Throws()
    {
        var json = """
            {
              "schemaVersion": "1.0.0", "name": "t",
              "decisions": [{"id": "d1", "name": "D1"}],
              "alternatives": [{"id": "a1", "decisionId": "d1", "name": "A1"}],
              "properties": [{"id": "p1", "name": "P", "kind": "min", "weight": 1.0}],
              "coefficients": [{"alternativeId": "a1", "propertyId": "p1", "value": {"lower": 1, "modal": 2, "upper": 3}}],
              "constraints": [{"kind": "dependency", "sourceAlternativeId": "a1", "targetAlternativeId": "a1"}],
              "config": {"aggregation": "max", "weights": {"positive": 0.333333333, "average": 0.333333333, "negative": 0.333333334}}
            }
            """;
        var ex = Assert.Throws<ScenarioValidationException>(() => LoadFromString(json));
        Assert.Contains("7.1", ex.Message);
    }

    [Fact]
    public void Invariant_8_1_DecisionWithNoAlternatives_Throws()
    {
        var json = """
            {
              "schemaVersion": "1.0.0", "name": "t",
              "decisions": [{"id": "d1", "name": "D1"}, {"id": "d2", "name": "D2"}],
              "alternatives": [{"id": "a1", "decisionId": "d1", "name": "A1"}],
              "properties": [{"id": "p1", "name": "P", "kind": "min", "weight": 1.0}],
              "coefficients": [{"alternativeId": "a1", "propertyId": "p1", "value": {"lower": 1, "modal": 2, "upper": 3}}],
              "constraints": [],
              "config": {"aggregation": "max", "weights": {"positive": 0.333333333, "average": 0.333333333, "negative": 0.333333334}}
            }
            """;
        var ex = Assert.Throws<ScenarioValidationException>(() => LoadFromString(json));
        Assert.Contains("8.1", ex.Message);
    }

    [Fact]
    public void RealSasScenario_LoadsWithoutError()
    {
        var sasPath = FindSasPath();
        var scenario = ScenarioLoader.Load(sasPath);
        Assert.NotNull(scenario);
        Assert.True(scenario.Decisions.Length > 0);
        Assert.True(scenario.Alternatives.Length > 0);
    }
}
