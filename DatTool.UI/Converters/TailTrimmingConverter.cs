using System;
using System.Globalization;
using System.Windows.Data;

namespace DatTool.UI.Converters;

public sealed class TailTrimmingConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string text || string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var maxChars = 40;
        if (parameter is string param && int.TryParse(param, out var parsed) && parsed > 0)
        {
            maxChars = parsed;
        }

        return text.Length <= maxChars
            ? text
            : "..." + text.Substring(text.Length - maxChars, maxChars);
    }

    public object? ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

