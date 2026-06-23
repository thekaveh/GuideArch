using Xunit;

public class ChartThemeTests
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
    public void Charts_resolve_colors_from_theme_brushes_not_catppuccin_literals()
    {
        var src = CodeBehind();
        Assert.Contains("ResolvePlotColor", src);
        // The Catppuccin dark-only literals are gone from the chart code.
        Assert.DoesNotContain("#1E1E2E", src);
        Assert.DoesNotContain("#89B4FA", src);
        Assert.DoesNotContain("0x89, 0xB4, 0xFA", src);
    }

    [Fact]
    public void Ranking_bars_resolve_the_accent_brush()
    {
        Assert.Contains("ResolvePlotColor(\"AccentBrush\")", CodeBehind());
    }

    [Fact]
    public void Fuzzy_triangle_uses_the_three_fuzzy_brushes()
    {
        var src = CodeBehind();
        Assert.Contains("FuzzyPositiveBrush", src);
        Assert.Contains("FuzzyAverageBrush", src);
        Assert.Contains("FuzzyNegativeBrush", src);
    }

    [Fact]
    public void Theme_toggle_re_renders_the_charts()
    {
        // ApplyTheme must re-init / re-render so plots retint on toggle.
        var src = CodeBehind();
        var idx = src.IndexOf("private void ApplyTheme", StringComparison.Ordinal);
        Assert.True(idx >= 0);
        var body = src.Substring(idx, System.Math.Min(900, src.Length - idx));
        Assert.True(
            body.Contains("InitCharts") || body.Contains("RenderChart") || body.Contains("RefreshCharts"),
            "ApplyTheme must re-render charts so they retint on theme toggle");
    }
}
