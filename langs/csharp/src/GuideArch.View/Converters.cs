using System;
using System.Collections;
using System.Globalization;
using Avalonia.Data.Converters;

namespace GuideArch.View;

/// <summary>
/// Joins an IEnumerable&lt;string&gt; (or ImmutableArray&lt;string&gt;) into a comma-separated string
/// for display in DataGrid cells.
/// </summary>
public sealed class StringJoinConverter : IValueConverter
{
    public static readonly StringJoinConverter Instance = new();

    public StringJoinConverter() { }

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is IEnumerable enumerable and not string)
        {
            var parts = new System.Collections.Generic.List<string>();
            foreach (var item in enumerable)
                parts.Add(item?.ToString() ?? string.Empty);
            return string.Join(", ", parts);
        }
        return value?.ToString();
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Uppercases a string value at bind time using the **invariant** culture
/// (not the system culture — the Turkish dotted-I / dotless-i case mapping
/// would otherwise scramble the brand kicker on Turkish-locale machines).
/// Used by the first-launch hero kicker so the C# side keeps the data
/// pristine ("Welcome to GuideArch") while rendering as "WELCOME TO
/// GUIDEARCH" — matching the TS/Python impls where CSS
/// `text-transform: uppercase` does the same job at render time. Avalonia
/// `TextBlock` has no analogous text-transform property, hence the
/// converter.
/// </summary>
public sealed class ToUpperConverter : IValueConverter
{
    public static readonly ToUpperConverter Instance = new();

    public ToUpperConverter() { }

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is string s ? s.ToUpperInvariant() : value?.ToString();

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
