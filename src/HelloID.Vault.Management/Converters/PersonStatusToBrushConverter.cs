using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace HelloID.Vault.Management.Converters;

/// <summary>
/// Converts PersonStatus string values to Brush colors for badge backgrounds.
/// Active = #10cf85, Future = #6dc0f9, Past = #f8d053, No Contract = Red
/// </summary>
public class PersonStatusToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string status)
        {
            return status switch
            {
                "Active" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10cf85")),
                "Future" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6dc0f9")),
                "Past" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#f8d053")),
                "No Contract" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F44336")),
                _ => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9E9E9E"))
            };
        }
        return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9E9E9E"));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
