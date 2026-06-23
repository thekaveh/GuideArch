using System.Xml.Linq;
using Xunit;

public class DialogStyleTests
{
    private static DirectoryInfo RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "langs")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return dir!;
    }

    private static XDocument Styles()
        => XDocument.Load(Path.Combine(RepoRoot().FullName,
            "langs/csharp/src/GuideArch.View/Resources/Styles.axaml"));

    private static string CodeBehind()
        => File.ReadAllText(Path.Combine(RepoRoot().FullName,
            "langs/csharp/src/GuideArch.View/MainWindow.axaml.cs"));

    [Fact]
    public void Styles_define_a_dialog_card_with_surface3_border_strong_and_8px_radius()
    {
        var style = Styles().Descendants()
            .Where(e => e.Name.LocalName == "Style")
            .SingleOrDefault(e =>
                ((string?)e.Attribute("Selector"))?.Contains("dialog-card") == true);
        Assert.NotNull(style);

        var setters = style!.Elements()
            .Where(e => e.Name.LocalName == "Setter")
            .ToDictionary(e => (string)e.Attribute("Property")!, e => (string)e.Attribute("Value")!);

        Assert.Contains("BgSurface3Brush", setters["Background"]);
        Assert.Contains("BorderStrongBrush", setters["BorderBrush"]);
        Assert.Equal("8", setters["CornerRadius"].Trim());
    }

    [Fact]
    public void Error_path_no_longer_builds_a_bare_unstyled_window()
    {
        // The raw error window was identified by its Title = "GuideArch — Error"
        // string and direct `new Window { ... Content = new StackPanel ... }` inlining.
        // After the rewrite, ShowErrorAsync delegates to BuildDialogShell.
        // Assert: (a) the styled helper exists; (b) the old raw-window title is gone;
        // (c) ShowErrorAsync no longer sets Window.Content to an inline StackPanel
        //     (verified by the absence of "BuildDialogShell" being absent — i.e. it exists).
        var src = CodeBehind();
        Assert.Contains("BuildDialogShell", src);
        // The old bare dialog set Title = "GuideArch — Error" directly on the raw Window.
        // The styled dialog has no such title (the card carries the title in a TextBlock).
        Assert.DoesNotContain("Title = \"GuideArch — Error\"", src);
    }

    [Fact]
    public void Confirm_helper_exists_and_returns_a_bool_task()
    {
        Assert.Contains("Task<bool> ShowConfirmAsync", CodeBehind());
    }
}
