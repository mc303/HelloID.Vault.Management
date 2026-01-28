using System.Globalization;
using System.Windows.Data;

namespace HelloID.Vault.Management.Converters;

/// <summary>
/// Converts date strings to formatted date strings for display.
/// Handles null and empty values gracefully.
/// </summary>
public class DateTimeFormatConverter : IValueConverter
{
    /// <summary>
    /// Gets or sets the format string to use for date formatting.
    /// Default is "yyyy-MM-dd".
    /// </summary>
    public string Format { get; set; } = "yyyy-MM-dd";

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // Handle null or empty values
        if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
            return string.Empty;

        // If it's already a DateTime, format it directly
        if (value is DateTime dateTime)
            return dateTime.ToString(Format, culture);

        // Try to parse the string as a date
        if (DateTime.TryParse(value.ToString(), culture, DateTimeStyles.None, out var parsedDate))
            return parsedDate.ToString(Format, culture);

        // Return original value if parsing fails
        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // For edit scenarios, convert back to the original format
        if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
            return string.Empty;

        if (DateTime.TryParse(value.ToString(), culture, DateTimeStyles.None, out var parsedDate))
            return parsedDate.ToString(Format, culture);

        return value;
    }
}
