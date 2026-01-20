using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace HelloID.Vault.Management.Converters;

/// <summary>
/// Converts bool winner status to icon color (green for winner, transparent for non-winner).
/// </summary>
public class WinnerIconColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isWinner && isWinner)
            return new SolidColorBrush(Colors.Green);
        return new SolidColorBrush(Colors.Transparent);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts bool winner status to font weight (Bold for winner, Normal for non-winner).
/// </summary>
public class WinnerFontWeightConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isWinner && isWinner)
            return FontWeights.Bold;
        return FontWeights.Normal;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts bool IsPrimary to background brush.
/// </summary>
public class PrimaryContractBackgroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isPrimary && isPrimary)
            return new SolidColorBrush(Color.FromRgb(232, 245, 233)); // Light green
        return new SolidColorBrush(Color.FromRgb(250, 250, 250)); // Light gray
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts bool IsPrimary to border brush.
/// </summary>
public class PrimaryContractBorderConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isPrimary && isPrimary)
            return new SolidColorBrush(Color.FromRgb(102, 187, 106)); // Green
        return new SolidColorBrush(Color.FromRgb(224, 224, 224)); // Gray
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts bool IsPrimary to border thickness.
/// </summary>
public class PrimaryContractBorderThicknessConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isPrimary && isPrimary)
            return new Thickness(2);
        return new Thickness(1);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts contract status to background brush.
/// Uses same colors as PersonStatusToBrushConverter from PersonsView.
/// Active = #10cf85, Future = #6dc0f9, Past = #f8d053
/// </summary>
public class ContractStatusBackgroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string status)
        {
            return status.ToLowerInvariant() switch
            {
                "active" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10cf85")), // Bright green
                "future" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6dc0f9")),  // Bright blue
                "past" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#f8d053")),   // Golden yellow
                _ => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9E9E9E"))       // Gray
            };
        }
        return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9E9E9E"));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts contract status to foreground brush.
/// Uses white text for colored badges to match PersonsView.
/// </summary>
public class ContractStatusForegroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string status)
        {
            return status.ToLowerInvariant() switch
            {
                "active" => new SolidColorBrush(Colors.White),
                "future" => new SolidColorBrush(Colors.White),
                "past" => new SolidColorBrush(Colors.White),
                _ => new SolidColorBrush(Color.FromRgb(97, 97, 97))
            };
        }
        return new SolidColorBrush(Color.FromRgb(97, 97, 97));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts contract status to border brush.
/// Uses same colors as background (no separate border needed).
/// </summary>
public class ContractStatusBorderConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string status)
        {
            return status.ToLowerInvariant() switch
            {
                "active" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10cf85")), // Bright green
                "future" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6dc0f9")),  // Bright blue
                "past" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#f8d053")),   // Golden yellow
                _ => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9E9E9E"))       // Gray
            };
        }
        return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9E9E9E"));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts null/empty end_date to "-" string.
/// </summary>
public class NullEndDateConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // Handle string values (from ContractDetailDto.EndDate)
        if (value is string strValue && !string.IsNullOrWhiteSpace(strValue))
        {
            return strValue;
        }
        // Handle DateTime values
        if (value is DateTime date && date > DateTime.MinValue)
        {
            return date.ToString("yyyy-MM-dd");
        }
        return "-";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
