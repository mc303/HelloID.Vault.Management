using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace HelloID.Vault.Management.Converters;

/// <summary>
/// Converts between double and string accepting BOTH '.' and ',' as decimal
/// separator. Needed because WPF bindings parse with the current culture
/// (e.g. nl-NL rejects "0.1"). Returns the string in invariant format.
/// </summary>
public class FlexibleDoubleConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double d)
        {
            return d.ToString("0.###", CultureInfo.InvariantCulture);
        }
        return string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string text || string.IsNullOrWhiteSpace(text))
        {
            return DependencyProperty.UnsetValue;
        }

        // Accept both separators regardless of system culture
        text = text.Trim().Replace(',', '.');
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
        {
            return result;
        }

        // Keep the previous value on invalid input instead of throwing
        return DependencyProperty.UnsetValue;
    }
}
