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
