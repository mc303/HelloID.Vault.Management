using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace HelloID.Vault.Management.Converters;

/// <summary>
/// Multi-value converter: (score, threshold) → green brush when score >= threshold,
/// default foreground otherwise. Used to highlight recommendations that qualify
/// for bulk accept.
/// </summary>
public class ScoreThresholdToBrushConverter : IMultiValueConverter
{
    private static readonly Brush HighlightBrush = new SolidColorBrush(Color.FromRgb(0x10, 0x7C, 0x10)); // green

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length == 2 &&
            double.TryParse(values[0]?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var score) &&
            double.TryParse(values[1]?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var threshold))
        {
            if (score >= threshold)
            {
                return HighlightBrush;
            }
        }
        return DependencyProperty.UnsetValue;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
