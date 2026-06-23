using System.Xml.Linq;
using Xunit;

public class SectionHeaderTests
{
    private static XDocument MainWindow()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "langs")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return XDocument.Load(Path.Combine(dir!.FullName,
            "langs/csharp/src/GuideArch.View/MainWindow.axaml"));
    }

    /// <summary>
    /// The header Border is the sibling preceding the named grid inside the
    /// same parent Grid (RowDefinitions="Auto,*"). We find the named element,
    /// walk to its parent, and assert that parent contains a header Border with
    /// the §5.9 padding and a 16-pt title TextBlock.
    /// </summary>
    [Theory]
    [InlineData("CriticalDecisionsGrid")]
    [InlineData("CriticalConstraintsGrid")]
    [InlineData("ResultsGrid")]
    public void Tab_has_a_section_header_border(string gridName)
    {
        var grid = MainWindow().Descendants()
            .Single(e => (string?)e.Attribute("Name") == gridName);

        // Walk up to the enclosing RowDefinitions="Auto,*" grid that hosts both
        // the header (Row 0) and the content (Row 1).
        var host = grid.Ancestors()
            .First(a => a.Name.LocalName == "Grid"
                        && (string?)a.Attribute("RowDefinitions") == "Auto,*");

        var headerBorder = host.Elements()
            .Where(e => e.Name.LocalName == "Border")
            .FirstOrDefault(b => (string?)b.Attribute("Padding") == "24,16,24,12");
        Assert.NotNull(headerBorder);

        // Title TextBlock — 16-pt SemiBold.
        var hasTitle = headerBorder!.Descendants()
            .Any(e => e.Name.LocalName == "TextBlock"
                      && (string?)e.Attribute("FontSize") == "16");
        Assert.True(hasTitle, $"{gridName} header has no 16-pt title TextBlock");
    }
}
