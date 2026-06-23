using System.Xml.Linq;
using Xunit;

public class TableStyleTests
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

    [Fact]
    public void Selected_row_draws_a_2px_accent_left_border()
    {
        var style = Styles().Descendants()
            .Where(e => e.Name.LocalName == "Style")
            .Single(e => (string?)e.Attribute("Selector") == "DataGridRow:selected");

        var setters = style.Elements()
            .Where(e => e.Name.LocalName == "Setter")
            .ToDictionary(e => (string)e.Attribute("Property")!, e => (string)e.Attribute("Value")!);

        Assert.Contains("AccentBrush", setters["BorderBrush"]);
        // Left edge is the 1st value of (left,top,right,bottom) and must be 2.
        var left = setters["BorderThickness"].Split(',')[0].Trim();
        Assert.Equal("2", left);
    }
}
