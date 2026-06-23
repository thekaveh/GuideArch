using System.Xml.Linq;
using Xunit;

/// <summary>
/// Verifies that each editor/analysis tab has a compact §5.8 empty-state Border
/// that shows when the matching collection is empty, and that the DataGrid hides
/// in the same zero-row case.
///
/// Emptiness mechanism: <c>*View</c> properties are <c>ImmutableArray&lt;T&gt;</c> which
/// already expose <c>.IsEmpty</c>.  We bind the empty Border's <c>IsVisible</c> to
/// <c>Model.&lt;Name&gt;View.IsEmpty</c> directly — no IsZeroConverter needed.
/// </summary>
public class EmptyStateTests
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

    [Theory]
    [InlineData("DecisionsEmptyState")]
    [InlineData("AlternativesEmptyState")]
    [InlineData("PropertiesEmptyState")]
    [InlineData("CriticalDecisionsEmptyState")]
    [InlineData("CriticalConstraintsEmptyState")]
    public void Section_has_a_compact_empty_state_border(string name)
    {
        var border = MainWindow().Descendants()
            .SingleOrDefault(e => e.Name.LocalName == "Border"
                                  && (string?)e.Attribute("Name") == name);
        Assert.NotNull(border);

        // Compact empty = a headline TextBlock (14-pt) somewhere inside.
        var hasHeadline = border!.Descendants()
            .Any(e => e.Name.LocalName == "TextBlock"
                      && (string?)e.Attribute("FontSize") == "14");
        Assert.True(hasHeadline, $"{name} has no 14-pt headline");

        // Visibility is gated on the collection's .IsEmpty property
        // (ImmutableArray<T>.IsEmpty — no converter required).
        var vis = (string?)border.Attribute("IsVisible") ?? "";
        Assert.Contains("IsEmpty", vis);
    }

    [Theory]
    [InlineData("DecisionsGrid")]
    [InlineData("AlternativesGrid")]
    [InlineData("PropertiesGrid")]
    [InlineData("CriticalDecisionsGrid")]
    [InlineData("CriticalConstraintsGrid")]
    public void DataGrid_hides_when_section_is_empty(string gridName)
    {
        var grid = MainWindow().Descendants()
            .SingleOrDefault(e => e.Name.LocalName == "DataGrid"
                                  && (string?)e.Attribute("Name") == gridName);
        Assert.NotNull(grid);

        // The grid's IsVisible must reference the collection's !IsEmpty
        // so grid and empty Border are mutually exclusive.
        var vis = (string?)grid!.Attribute("IsVisible") ?? "";
        Assert.Contains("IsEmpty", vis);
        Assert.Contains("!", vis); // negated — grid visible when NOT empty
    }
}
