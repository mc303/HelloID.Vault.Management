using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using HelloID.Vault.Core.Models;

namespace HelloID.Vault.Management.Converters;

/// <summary>
/// Converts a column name to Visibility based on ColumnVisibility collection.
/// Usage: Visibility="{Binding ColumnVisibility, Converter={StaticResource ColumnNameToVisibilityConverter}, ConverterParameter='ContractId'}"
/// </summary>
public class ColumnNameToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not System.Collections.ObjectModel.ObservableCollection<ColumnVisibility> columns)
            return Visibility.Visible;

        if (parameter is not string columnName)
            return Visibility.Visible;

        var column = columns.FirstOrDefault(c => c.ColumnName == columnName);
        return column?.IsVisible == true ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
