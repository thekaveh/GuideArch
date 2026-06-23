using System.Xml.Linq;
using Xunit;

public class ToolbarMarkupTests
{
    private static string ViewDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "langs")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "langs/csharp/src/GuideArch.View");
    }

    private static XDocument MainWindow() =>
        XDocument.Load(Path.Combine(ViewDir(), "MainWindow.axaml"));

    private static XDocument Styles() =>
        XDocument.Load(Path.Combine(ViewDir(), "Resources/Styles.axaml"));

    [Theory]
    [InlineData("BtnNew")]
    [InlineData("BtnOpen")]
    [InlineData("BtnSave")]
    [InlineData("BtnSaveAs")]
    public void File_group_button_renders_a_path_icon(string name)
    {
        var btn = MainWindow().Descendants()
            .Single(e => e.Name.LocalName == "Button"
                         && (string?)e.Attribute("Name") == name);
        // No bare Content="…" string — the icon+label live in nested elements.
        Assert.Null(btn.Attribute("Content"));
        Assert.Contains(btn.Descendants(), e => e.Name.LocalName == "Path");
    }

    [Fact]
    public void Styles_define_an_icon_button_class()
    {
        var hasIconBtn = Styles().Descendants()
            .Where(e => e.Name.LocalName == "Style")
            .Select(e => (string?)e.Attribute("Selector"))
            .Any(s => s is not null && s.Contains("icon-btn"));
        Assert.True(hasIconBtn, "Styles.axaml must define a Button.icon-btn style");
    }
}
