using System.Xml.Linq;
using Xunit;

public class TabStripStyleTests
{
    private static XDocument Styles()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "langs")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return XDocument.Load(Path.Combine(dir!.FullName,
            "langs/csharp/src/GuideArch.View/Resources/Styles.axaml"));
    }

    /// <summary>Setters of the first &lt;Style&gt; whose Selector matches needle but not "/template/".</summary>
    private static Dictionary<string, string> Setters(string selectorNeedle)
    {
        var style = Styles().Descendants()
            .Where(e => e.Name.LocalName == "Style")
            .Single(e =>
            {
                var s = (string?)e.Attribute("Selector");
                return s?.Contains(selectorNeedle) == true && !s.Contains("/template/");
            });
        return style.Elements()
            .Where(e => e.Name.LocalName == "Setter")
            .ToDictionary(
                e => (string)e.Attribute("Property")!,
                e => (string)e.Attribute("Value")!);
    }

    [Fact]
    public void Selected_tab_draws_a_2px_accent_bottom_border()
    {
        var s = Setters("TabItem:selected");
        Assert.Contains("BorderThickness", s.Keys);
        // Bottom edge is the 4th value (left,top,right,bottom) and must be 2.
        var bottom = s["BorderThickness"].Split(',')[^1].Trim();
        Assert.Equal("2", bottom);
        Assert.Contains("AccentBrush", s["BorderBrush"]);
    }

    [Fact]
    public void Tab_strip_has_a_subtle_bottom_hairline()
    {
        var s = Setters("TabControl");
        Assert.Contains("BorderThickness", s.Keys);
        Assert.Contains("BorderSubtleBrush", s["BorderBrush"]);
    }
}
