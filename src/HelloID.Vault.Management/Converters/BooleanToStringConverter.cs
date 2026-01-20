using System;
using System.Globalization;
using System.Windows.Data;

namespace HelloID.Vault.Management.Converters;

/// <summary>
/// Converts boolean values to string representations.
/// Supports parameter format: "trueValue|falseValue"
/// </summary>
public class BooleanToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            var param = parameter as string;
            if (!string.IsNullOrEmpty(param))
            {
                var parts = param.Split('|');
                if (parts.Length >= 2)
                {
                    return boolValue ? parts[0] : parts[1];
                }
            }
            return boolValue ? "Yes" : "No";
        }
        return "No";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string strValue)
        {
            if (string.Equals(strValue, "true", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(strValue, "yes", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(strValue, "1", StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }
}