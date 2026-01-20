using System;
using System.Globalization;
using System.Windows.Data;

namespace HelloID.Vault.Management.Converters;

/// <summary>
/// Converts between sort order string ("ASC"/"DESC") and boolean for ToggleSwitch.
/// True = DESC (descending), False = ASC (ascending).
/// </summary>
public class SortOrderToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null)
            return false;

        return value.ToString().Equals("DESC", StringComparison.OrdinalIgnoreCase);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return boolValue ? "DESC" : "ASC";
        }
        return "ASC";
    }
}
