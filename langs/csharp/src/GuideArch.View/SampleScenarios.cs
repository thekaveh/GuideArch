namespace GuideArch.View;

/// <summary>
/// Provides access to the bundled sample scenarios embedded as resources.
/// </summary>
public static class SampleScenarios
{
    public sealed record Sample(string Id, string Label, string ResourceName);

    public static IReadOnlyList<Sample> All { get; } = new[]
    {
        new Sample("sas", "SAS — Service-Oriented Architecture", "GuideArch.View.Assets.Samples.sas.json"),
        new Sample("eds", "EDS — Enterprise Decision Space",     "GuideArch.View.Assets.Samples.eds.json"),
    };

    /// <summary>Opens the embedded resource stream for the given sample.</summary>
    public static Stream Open(Sample s)
        => typeof(SampleScenarios).Assembly.GetManifestResourceStream(s.ResourceName)!;
}
