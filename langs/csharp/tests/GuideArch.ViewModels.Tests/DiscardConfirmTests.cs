using Xunit;

public class DiscardConfirmTests
{
    private static string CodeBehind()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "langs")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return File.ReadAllText(Path.Combine(dir!.FullName,
            "langs/csharp/src/GuideArch.View/MainWindow.axaml.cs"));
    }

    [Fact]
    public void Post_hoc_discard_warning_is_gone()
    {
        var src = CodeBehind();
        // The post-hoc status-bar warning method and its callers are removed.
        Assert.DoesNotContain("StampDiscardWarning", src);
    }

    [Fact]
    public void No_plan5_todo_marker_remains_in_the_code_behind()
    {
        Assert.DoesNotContain("TODO(plan5)", CodeBehind());
    }

    [Fact]
    public void All_four_discard_handlers_gate_on_show_confirm_async()
    {
        var src = CodeBehind();
        foreach (var handler in new[]
                 {
                     "OnNewClicked", "OnOpenClicked",
                     "OnOpenSampleSasClicked", "OnOpenSampleEdsClicked",
                 })
        {
            var start = src.IndexOf($"private async void {handler}", StringComparison.Ordinal);
            Assert.True(start >= 0, $"{handler} must be an async void handler");
            var next = src.IndexOf("\n    private ", start + 1, StringComparison.Ordinal);
            var body = src.Substring(start, (next < 0 ? src.Length : next) - start);
            Assert.Contains("await ShowConfirmAsync", body);
        }
    }

    [Fact]
    public void Discard_confirm_is_non_destructive()
    {
        // Discard-unsaved is a non-destructive confirm (Confirm label, not Delete).
        var src = CodeBehind();
        var idx = src.IndexOf("ShowConfirmAsync(\"Discard", StringComparison.Ordinal);
        Assert.True(idx >= 0, "expected a Discard confirm call");
        var slice = src.Substring(idx, System.Math.Min(400, src.Length - idx));
        Assert.Contains("destructive: false", slice);
    }
}
