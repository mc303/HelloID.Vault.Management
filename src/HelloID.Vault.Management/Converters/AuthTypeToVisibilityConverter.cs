using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace HelloID.Vault.Management.Converters;

/// <summary>
/// Converts an auth type string to visibility. Visible when the value matches
/// the ConverterParameter (case-insensitive), collapsed otherwise.
/// Used to show username/password fields only for "Simple" auth.
/// </summary>
public class AuthTypeToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var authType = value as string;
        var expected = parameter as string;

        if (string.Equals(authType, expected, StringComparison.OrdinalIgnoreCase))
        {
            return Visibility.Visible;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
