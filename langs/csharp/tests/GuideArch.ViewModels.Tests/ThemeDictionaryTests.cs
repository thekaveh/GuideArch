using System.Xml.Linq;
using Xunit;

public class ThemeDictionaryTests
{
    // Resolve Colors.axaml relative to the repo root (test runs from bin/).
    private static string ColorsPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "langs")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName,
            "langs/csharp/src/GuideArch.View/Resources/Colors.axaml");
    }

    private static readonly string[] RequiredColorKeys =
    {
        "BgPageColor", "BgSurfaceColor", "BgSurface2Color", "BgSurface3Color",
        "BorderSubtleColor", "BorderStrongColor",
        "TextPrimaryColor", "TextSecondaryColor", "TextMutedColor", "TextInverseColor",
        "AccentColor", "AccentHoverColor", "AccentMutedColor", "AccentOnColor",
        "SuccessColor", "WarningColor", "DangerColor", "InfoColor",
        "FuzzyPositiveColor", "FuzzyAverageColor", "FuzzyNegativeColor",
    };

    [Theory]
    [InlineData("Dark")]
    [InlineData("Light")]
    public void ThemeVariant_defines_every_color_key(string variant)
    {
        var doc = XDocument.Load(ColorsPath());
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";

        // Find the <ResourceDictionary x:Key="{variant}"> theme dictionary.
        var dict = doc.Descendants()
            .Where(e => e.Name.LocalName == "ResourceDictionary"
                        && (string?)e.Attribute(x + "Key") == variant)
            .ToList();
        Assert.Single(dict);

        var keys = dict[0].Elements()
            .Where(e => e.Name.LocalName == "Color")
            .Select(e => (string?)e.Attribute(x + "Key"))
            .ToHashSet();

        foreach (var k in RequiredColorKeys)
            Assert.Contains(k, keys);
    }
}
