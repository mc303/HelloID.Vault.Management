using System;
using System.Globalization;
using System.Windows.Data;

namespace HelloID.Vault.Management.Converters;

/// <summary>
/// Converts between enum values and boolean for RadioButton/CheckBox bindings.
/// Usage: IsChecked="{Binding MyEnumProperty, Converter={StaticResource EnumToBooleanConverter}, ConverterParameter=MyEnumValue}"
/// </summary>
public class EnumToBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null || parameter == null)
            return false;

        // Check if the enum value equals the parameter value
        return value.ToString().Equals(parameter.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue && boolValue && parameter != null)
        {
            // Parse the parameter string to the target enum type
            return Enum.Parse(targetType, parameter.ToString()!);
        }
        return Binding.DoNothing;
    }
}
