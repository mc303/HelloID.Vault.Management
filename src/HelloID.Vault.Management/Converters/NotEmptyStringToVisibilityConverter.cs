using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace HelloID.Vault.Management.Converters;

/// <summary>
/// Converts string values to Visibility.
/// Empty or null string = Collapsed, Non-empty string = Visible.
/// </summary>
public class NotEmptyStringToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is string str && !string.IsNullOrWhiteSpace(str)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
