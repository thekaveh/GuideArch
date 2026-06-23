using System.Xml.Linq;
using Xunit;

public class InputFocusStyleTests
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

    private static IEnumerable<XElement> StylesWhereSelectorContains(string needle)
        => Styles().Descendants()
            .Where(e => e.Name.LocalName == "Style"
                        && ((string?)e.Attribute("Selector"))?.Contains(needle) == true);

    [Theory]
    [InlineData("TextBox")]
    [InlineData("NumericUpDown")]
    [InlineData("ComboBox")]
    public void Input_has_a_focus_style_with_accent_border(string control)
    {
        // Collect all focus[-within] styles for this control type.
        // Exclude /template/ variants (e.g. "TextBox:focus /template/ Border") —
        // those target inner template parts and are distinct from the outer focus style.
        var focusStyles = StylesWhereSelectorContains(control)
            .Where(e =>
            {
                var sel = (string)e.Attribute("Selector")!;
                return sel.Contains(":focus") && !sel.Contains("/template/");
            })
            .ToList();
        Assert.NotEmpty(focusStyles);

        // Every matching focus style must set a 2px AccentBrush border —
        // this covers the case where TextBox/ComboBox have both :focus and :focus-within.
        foreach (var style in focusStyles)
        {
            var setters = style.Elements()
                .Where(e => e.Name.LocalName == "Setter")
                .ToList();

            var borderBrush = setters.Single(
                s => (string?)s.Attribute("Property") == "BorderBrush");
            Assert.Contains("AccentBrush", (string)borderBrush.Attribute("Value")!);

            var thickness = setters.Single(
                s => (string?)s.Attribute("Property") == "BorderThickness");
            Assert.Equal("2", ((string)thickness.Attribute("Value")!).Trim());
        }
    }
}
