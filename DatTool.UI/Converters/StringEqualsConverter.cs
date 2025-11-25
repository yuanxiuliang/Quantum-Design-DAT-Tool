using System;
using System.Globalization;
using System.Windows.Data;

namespace DatTool.UI.Converters;

public sealed class StringEqualsConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is null || parameter is null)
        {
            return false;
        }

        return string.Equals(value.ToString(), parameter.ToString(), StringComparison.Ordinal);
    }

    public object? ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue && boolValue && parameter is not null)
        {
            return parameter.ToString();
        }

        return Binding.DoNothing;
    }
}

