using System.Xml.Linq;
using Xunit;

public class SolveButtonStyleTests
{
    private static string ViewDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "langs")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "langs/csharp/src/GuideArch.View");
    }

    private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";

    private static XElement Variant(string key)
    {
        var doc = XDocument.Load(Path.Combine(ViewDir(), "Resources/Colors.axaml"));
        return doc.Descendants()
            .Single(e => e.Name.LocalName == "ResourceDictionary"
                         && (string?)e.Attribute(X + "Key") == key);
    }

    [Theory]
    [InlineData("Dark")]
    [InlineData("Light")]
    public void Both_variants_define_a_solve_background_brush(string key)
    {
        var hasKey = Variant(key).Elements()
            .Any(e => (string?)e.Attribute(X + "Key") == "SolveBackgroundBrush");
        Assert.True(hasKey, $"{key} dictionary missing SolveBackgroundBrush");
    }

    [Fact]
    public void Dark_solve_background_is_a_gradient()
    {
        var brush = Variant("Dark").Elements()
            .Single(e => (string?)e.Attribute(X + "Key") == "SolveBackgroundBrush");
        Assert.Equal("LinearGradientBrush", brush.Name.LocalName);
    }

    [Fact]
    public void Styles_define_a_solve_button_class()
    {
        var doc = XDocument.Load(Path.Combine(ViewDir(), "Resources/Styles.axaml"));
        var has = doc.Descendants()
            .Where(e => e.Name.LocalName == "Style")
            .Select(e => (string?)e.Attribute("Selector"))
            .Any(s => s is not null && s.Contains("solve-btn"));
        Assert.True(has, "Styles.axaml must define a Button.solve-btn style");
    }
}
