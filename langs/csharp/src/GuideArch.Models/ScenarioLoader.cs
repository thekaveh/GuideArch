using System.Collections.Immutable;
using System.Text.Json;
using Json.Schema;

namespace GuideArch.Models;

/// <summary>
/// Loads and validates a scenario JSON file → ScenarioM.
/// Uses JsonSchema.Net (Json.Schema NuGet) to validate against
/// spec/domain/scenario.schema.json, then enforces semantic invariants.
/// Throws <see cref="ScenarioValidationException"/> on fatal violations.
/// </summary>
public static class ScenarioLoader
{
    private static readonly string DefaultSchemaPath = FindSchemaPath();

    // Cache compiled schemas. JsonSchema.Net's SchemaRegistry rejects
    // double-registration of the same $id (8.x: throws on second register
    // with same $id; 9.x: same, but the message reads "Overwriting
    // registered schemas is not permitted"). The disk path and the
    // `<embedded>` sentinel resolve to the SAME schema content with the
    // SAME $id, so a path-keyed cache calls FromText twice for that
    // content and fails on the second call. Dedupe by content text:
    // identical text → one FromText call → one registry entry.
    private static readonly Dictionary<string, JsonSchema> SchemaCache = new();
    private static readonly object SchemaCacheLock = new();

    // Sentinel returned by FindSchemaPath when the filesystem walk finds nothing.
    // GetSchema treats this as "load from embedded manifest resource".
    private const string EmbeddedSchemaSentinel = "<embedded>";

    private static string FindSchemaPath()
    {
        // Walk up from this assembly's location to find the repo root. Works in
        // dev (binaries land under bin/Release/net8.0/) and in-repo tests.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (int i = 0; i < 10; i++)
        {
            var candidate = Path.Combine(dir.FullName, "spec", "domain", "scenario.schema.json");
            if (File.Exists(candidate))
                return candidate;
            if (dir.Parent is null) break;
            dir = dir.Parent;
        }
        // Not on disk — release.yml publishes a self-contained single-file
        // binary where the spec/ directory does not travel with it. Fall back
        // to the embedded manifest resource (see GuideArch.Models.csproj).
        return EmbeddedSchemaSentinel;
    }

    private static JsonSchema GetSchema(string resolvedSchemaPath)
    {
        // Read schema text first (cheap), then dedupe by content. Reading
        // outside the lock is safe — File.ReadAllText / stream read have no
        // shared mutable state worth guarding.
        string schemaText;
        if (resolvedSchemaPath == EmbeddedSchemaSentinel)
        {
            var asm = typeof(ScenarioLoader).Assembly;
            using var stream = asm.GetManifestResourceStream("scenario.schema.json")
                ?? throw new InvalidOperationException(
                    "scenario.schema.json was neither found on disk nor embedded as a manifest resource. "
                    + "Rebuild GuideArch.Models; the csproj must embed spec/domain/scenario.schema.json.");
            using var reader = new StreamReader(stream);
            schemaText = reader.ReadToEnd();
        }
        else
        {
            schemaText = File.ReadAllText(resolvedSchemaPath);
        }

        lock (SchemaCacheLock)
        {
            if (!SchemaCache.TryGetValue(schemaText, out var cached))
            {
                cached = JsonSchema.FromText(schemaText);
                SchemaCache[schemaText] = cached;
            }
            return cached;
        }
    }

    public static ScenarioM Load(string path, string? schemaPath = null)
    {
        schemaPath ??= DefaultSchemaPath;

        // The embedded-resource sentinel is intentionally not a real path —
        // don't normalize it (Path.IsPathRooted("<embedded>") is false, so
        // GetFullPath would turn the sentinel into <cwd>/<embedded> and
        // GetSchema's `== EmbeddedSchemaSentinel` comparison would miss).
        // Only normalize when the caller passed a genuine relative path.
        if (schemaPath != EmbeddedSchemaSentinel && !Path.IsPathRooted(schemaPath))
            schemaPath = Path.GetFullPath(schemaPath);

        string jsonText = File.ReadAllText(path);
        using var doc = JsonDocument.Parse(jsonText);

        // ------------------------------------------------------------------ //
        // JSON Schema structural validation using JsonSchema.Net
        // ------------------------------------------------------------------ //
        var schema = GetSchema(schemaPath);

        var evalOpts = new EvaluationOptions
        {
            OutputFormat = OutputFormat.List
        };
        var result = schema.Evaluate(doc.RootElement, evalOpts);
        if (!result.IsValid)
        {
            var msgs = (result.Details ?? Enumerable.Empty<EvaluationResults>())
                .Where(d => !d.IsValid && d.Errors != null)
                .SelectMany(d => d.Errors!.Select(kvp => $"{d.InstanceLocation}: {kvp.Value}"))
                .Take(5);
            throw new ScenarioValidationException($"JSON Schema validation failed: {string.Join("; ", msgs)}");
        }

        var root = doc.RootElement;

        var warnings = new List<string>();

        // ------------------------------------------------------------------ //
        // Parse raw objects
        // ------------------------------------------------------------------ //
        var decisions = root.GetProperty("decisions").EnumerateArray()
            .Select(d => new DecisionM(d.GetProperty("id").GetString()!, d.GetProperty("name").GetString()!))
            .ToImmutableArray();

        var alternatives = root.GetProperty("alternatives").EnumerateArray()
            .Select(a => new AlternativeM(
                a.GetProperty("id").GetString()!,
                a.GetProperty("decisionId").GetString()!,
                a.GetProperty("name").GetString()!
            ))
            .ToImmutableArray();

        var properties = root.GetProperty("properties").EnumerateArray()
            .Select(p => new PropertyM(
                p.GetProperty("id").GetString()!,
                p.GetProperty("name").GetString()!,
                p.GetProperty("kind").GetString()! == "min" ? PropertyKind.Min : PropertyKind.Max,
                p.GetProperty("weight").GetDouble()
            ))
            .ToImmutableArray();

        static TriangularFuzzyM ParseTfm(JsonElement v) =>
            new(v.GetProperty("lower").GetDouble(),
                v.GetProperty("modal").GetDouble(),
                v.GetProperty("upper").GetDouble());

        var coefficients = root.GetProperty("coefficients").EnumerateArray()
            .Select(c => new CoefficientM(
                c.GetProperty("alternativeId").GetString()!,
                c.GetProperty("propertyId").GetString()!,
                ParseTfm(c.GetProperty("value"))
            ))
            .ToImmutableArray();

        var constraintsList = new List<ConstraintM>();
        foreach (var c in root.GetProperty("constraints").EnumerateArray())
        {
            string kind = c.GetProperty("kind").GetString()!;
            if (kind == "threshold")
            {
                double? minVal = c.TryGetProperty("min", out var minEl) ? minEl.GetDouble() : (double?)null;
                double? maxVal = c.TryGetProperty("max", out var maxEl) ? maxEl.GetDouble() : (double?)null;
                constraintsList.Add(new ThresholdConstraintM(
                    c.GetProperty("propertyId").GetString()!,
                    minVal,
                    maxVal
                ));
            }
            else if (kind == "dependency")
            {
                constraintsList.Add(new DependencyConstraintM(
                    c.GetProperty("sourceAlternativeId").GetString()!,
                    c.GetProperty("targetAlternativeId").GetString()!
                ));
            }
            else if (kind == "conflict")
            {
                constraintsList.Add(new ConflictConstraintM(
                    c.GetProperty("alternativeAId").GetString()!,
                    c.GetProperty("alternativeBId").GetString()!
                ));
            }
        }
        var constraints = constraintsList.ToImmutableArray();

        var wRaw = root.GetProperty("config").GetProperty("weights");
        var weights = new NormalizedFuzzyM(
            wRaw.GetProperty("positive").GetDouble(),
            wRaw.GetProperty("average").GetDouble(),
            wRaw.GetProperty("negative").GetDouble()
        );
        string aggStr = root.GetProperty("config").GetProperty("aggregation").GetString()!;
        var aggregation = aggStr == "sum" ? Aggregation.Sum : Aggregation.Max;
        var config = new ConfigM(aggregation, weights);

        // ------------------------------------------------------------------ //
        // Invariant 1: Identifier uniqueness (fatal)
        // ------------------------------------------------------------------ //
        var decIds = decisions.Select(d => d.Id).ToList();
        var altIds = alternatives.Select(a => a.Id).ToList();
        var propIds = properties.Select(p => p.Id).ToList();

        if (decIds.Count != decIds.Distinct().Count())
            throw new ScenarioValidationException("Invariant 1.1: duplicate decision id(s)");
        if (altIds.Count != altIds.Distinct().Count())
            throw new ScenarioValidationException("Invariant 1.2: duplicate alternative id(s)");
        if (propIds.Count != propIds.Distinct().Count())
            throw new ScenarioValidationException("Invariant 1.3: duplicate property id(s)");

        var decIdSet = new HashSet<string>(decIds);
        var altIdSet = new HashSet<string>(altIds);
        var propIdSet = new HashSet<string>(propIds);

        var overlapDa = decIdSet.Intersect(altIdSet).OrderBy(x => x, StringComparer.Ordinal).ToList();
        var overlapDp = decIdSet.Intersect(propIdSet).OrderBy(x => x, StringComparer.Ordinal).ToList();
        var overlapAp = altIdSet.Intersect(propIdSet).OrderBy(x => x, StringComparer.Ordinal).ToList();
        if (overlapDa.Count > 0 || overlapDp.Count > 0 || overlapAp.Count > 0)
            // Serialise the sorted overlap lists as JSON arrays so the message
            // text is byte-identical to the Python and TypeScript loaders.
            throw new ScenarioValidationException(
                "Invariant 1.4: id namespace collision: " +
                $"d∩a={JsonSerializer.Serialize(overlapDa)}, " +
                $"d∩p={JsonSerializer.Serialize(overlapDp)}, " +
                $"a∩p={JsonSerializer.Serialize(overlapAp)}");

        // ------------------------------------------------------------------ //
        // Invariant 2: Cross-reference validity (fatal)
        // ------------------------------------------------------------------ //
        foreach (var a in alternatives)
        {
            if (!decIdSet.Contains(a.DecisionId))
                throw new ScenarioValidationException(
                    $"Invariant 2.1: alternative '{a.Id}' references unknown decision '{a.DecisionId}'");
        }
        foreach (var c in coefficients)
        {
            if (!altIdSet.Contains(c.AlternativeId))
                throw new ScenarioValidationException(
                    $"Invariant 2.2: coefficient references unknown alternative '{c.AlternativeId}'");
            if (!propIdSet.Contains(c.PropertyId))
                throw new ScenarioValidationException(
                    $"Invariant 2.3: coefficient references unknown property '{c.PropertyId}'");
        }
        foreach (var con in constraints)
        {
            if (con is ThresholdConstraintM tc)
            {
                if (!propIdSet.Contains(tc.PropertyId))
                    throw new ScenarioValidationException(
                        $"Invariant 2.4: threshold constraint references unknown property '{tc.PropertyId}'");
            }
            else if (con is DependencyConstraintM dc)
            {
                if (!altIdSet.Contains(dc.SourceAlternativeId))
                    throw new ScenarioValidationException(
                        $"Invariant 2.5: dependency constraint references unknown source '{dc.SourceAlternativeId}'");
                if (!altIdSet.Contains(dc.TargetAlternativeId))
                    throw new ScenarioValidationException(
                        $"Invariant 2.5: dependency constraint references unknown target '{dc.TargetAlternativeId}'");
            }
            else if (con is ConflictConstraintM cc)
            {
                if (!altIdSet.Contains(cc.AlternativeAId))
                    throw new ScenarioValidationException(
                        $"Invariant 2.5: conflict constraint references unknown alternative A '{cc.AlternativeAId}'");
                if (!altIdSet.Contains(cc.AlternativeBId))
                    throw new ScenarioValidationException(
                        $"Invariant 2.5: conflict constraint references unknown alternative B '{cc.AlternativeBId}'");
            }
        }

        // ------------------------------------------------------------------ //
        // Invariant 3: Coefficient completeness (fatal)
        // ------------------------------------------------------------------ //
        var coeffPairs = coefficients.Select(c => (c.AlternativeId, c.PropertyId)).ToList();
        var coeffPairSet = new HashSet<(string, string)>(coeffPairs);
        if (coeffPairs.Count != coeffPairSet.Count)
            throw new ScenarioValidationException(
                "Invariant 3.1: duplicate (alternativeId, propertyId) coefficient");
        foreach (var aId in altIds)
        {
            foreach (var pId in propIds)
            {
                if (!coeffPairSet.Contains((aId, pId)))
                    throw new ScenarioValidationException(
                        $"Invariant 3.1: missing coefficient for (alternative={aId}, property={pId})");
            }
        }

        // ------------------------------------------------------------------ //
        // Invariant 4: Triangular ordering (warning)
        // ------------------------------------------------------------------ //
        foreach (var c in coefficients)
        {
            var v = c.Value;
            if (!(v.Lower <= v.Modal && v.Modal <= v.Upper))
                warnings.Add(
                    $"Invariant 4.1: coefficient ({c.AlternativeId}, {c.PropertyId}) " +
                    $"has lower={v.Lower} modal={v.Modal} upper={v.Upper} — ordering violated");
        }

        // ------------------------------------------------------------------ //
        // Invariant 5: Weights (fatal)
        // ------------------------------------------------------------------ //
        var w = config.Weights;
        foreach (var (val, label) in new[] { (w.Positive, "positive"), (w.Average, "average"), (w.Negative, "negative") })
        {
            if (val < 0.0 || val > 1.0)
                throw new ScenarioValidationException($"Invariant 5.2: weight.{label}={val} is outside [0, 1]");
        }
        double wSum = w.Positive + w.Average + w.Negative;
        if (Math.Abs(wSum - 1.0) > 1e-9)
            throw new ScenarioValidationException(
                $"Invariant 5.1: weights sum to {wSum}, expected 1.0 (tolerance 1e-9)");

        // ------------------------------------------------------------------ //
        // Invariant 6: Threshold constraints (fatal)
        // ------------------------------------------------------------------ //
        for (int i = 0; i < constraints.Length; i++)
        {
            if (constraints[i] is ThresholdConstraintM tc)
            {
                if (!tc.Min.HasValue && !tc.Max.HasValue)
                    throw new ScenarioValidationException(
                        $"Invariant 6.1: threshold constraint[{i}] has neither min nor max");
                if (tc.Min.HasValue && tc.Max.HasValue && tc.Min.Value > tc.Max.Value)
                    throw new ScenarioValidationException(
                        $"Invariant 6.2: threshold constraint[{i}] has min={tc.Min} > max={tc.Max}");
            }
        }

        // ------------------------------------------------------------------ //
        // Invariant 7: Dependency / conflict self-edges (fatal + warning)
        // ------------------------------------------------------------------ //
        var decOf = alternatives.ToDictionary(a => a.Id, a => a.DecisionId);
        for (int i = 0; i < constraints.Length; i++)
        {
            if (constraints[i] is DependencyConstraintM dc)
            {
                if (dc.SourceAlternativeId == dc.TargetAlternativeId)
                    throw new ScenarioValidationException(
                        $"Invariant 7.1: dependency constraint[{i}] is a self-edge");
                if (decOf.TryGetValue(dc.SourceAlternativeId, out var srcDec) &&
                    decOf.TryGetValue(dc.TargetAlternativeId, out var tgtDec) &&
                    srcDec == tgtDec)
                    warnings.Add(
                        $"Invariant 7.2: dependency constraint[{i}] connects alternatives of the same decision — rarely meaningful");
            }
            else if (constraints[i] is ConflictConstraintM cc)
            {
                if (cc.AlternativeAId == cc.AlternativeBId)
                    throw new ScenarioValidationException(
                        $"Invariant 7.1: conflict constraint[{i}] is a self-edge");
                if (decOf.TryGetValue(cc.AlternativeAId, out var aDec) &&
                    decOf.TryGetValue(cc.AlternativeBId, out var bDec) &&
                    aDec == bDec)
                    warnings.Add(
                        $"Invariant 7.2: conflict constraint[{i}] connects alternatives of the same decision — rarely meaningful");
            }
        }

        // ------------------------------------------------------------------ //
        // Invariant 8: Decision occupancy (fatal)
        // ------------------------------------------------------------------ //
        var altsByDec = new Dictionary<string, List<string>>(decIds.Count);
        foreach (var d in decisions) altsByDec[d.Id] = new List<string>();
        foreach (var a in alternatives) altsByDec[a.DecisionId].Add(a.Id);
        foreach (var d in decisions)
        {
            if (altsByDec[d.Id].Count == 0)
                throw new ScenarioValidationException(
                    $"Invariant 8.1: decision '{d.Id}' ('{d.Name}') has no alternatives");
        }

        return new ScenarioM(
            SchemaVersion: root.GetProperty("schemaVersion").GetString()!,
            Name: root.GetProperty("name").GetString()!,
            Description: root.TryGetProperty("description", out var descEl)
                ? descEl.GetString() ?? ""
                : "",
            Decisions: decisions,
            Alternatives: alternatives,
            Properties: properties,
            Coefficients: coefficients,
            Constraints: constraints,
            Config: config,
            Warnings: warnings.ToImmutableArray()
        );
    }
}
