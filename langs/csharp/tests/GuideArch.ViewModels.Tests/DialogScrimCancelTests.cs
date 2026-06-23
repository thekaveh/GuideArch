using Xunit;

public class DialogScrimCancelTests
{
    private static string BuildDialogShellBody()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "langs")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        var src = File.ReadAllText(Path.Combine(dir!.FullName,
            "langs/csharp/src/GuideArch.View/MainWindow.axaml.cs"));
        var start = src.IndexOf("BuildDialogShell(", StringComparison.Ordinal);
        Assert.True(start >= 0);
        // Slice the helper body generously (to the next top-level `private `).
        var next = src.IndexOf("\n    private async Task ShowErrorAsync",
            start, StringComparison.Ordinal);
        return src.Substring(start, (next < 0 ? src.Length : next) - start);
    }

    [Fact]
    public void Scrim_border_closes_the_dialog_on_pointer_press()
    {
        var body = BuildDialogShellBody();
        Assert.Contains("PointerPressed", body);
        Assert.Contains("window.Close()", body);
    }

    [Fact]
    public void Scrim_click_is_guarded_so_card_clicks_do_not_dismiss()
    {
        // Either a source check against the scrim Border, or an e.Handled = true
        // on the card (the §5.10 / TS stopPropagation equivalent).
        var body = BuildDialogShellBody();
        var guarded =
            body.Contains("ReferenceEquals", System.StringComparison.Ordinal)
            || body.Contains("e.Handled = true", System.StringComparison.Ordinal)
            || body.Contains("e.Source", System.StringComparison.Ordinal);
        Assert.True(guarded,
            "scrim-click dismissal must be guarded so a click on the card does not cancel");
    }
}
