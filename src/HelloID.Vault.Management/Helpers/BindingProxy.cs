using System.ComponentModel;
using System.Windows;
using System.Windows.Data;

namespace HelloID.Vault.Management.Helpers;

/// <summary>
/// Proxy class to enable DataGridColumn binding to properties outside the visual tree.
/// DataGridColumn is not in the visual tree, so it cannot use RelativeSource bindings.
/// This Freezable class can be placed in Resources and bridges the gap.
/// Usage:
///   <Resources>
///     <helpers:BindingProxy x:Key="ColumnVisibilityProxy" Data="{Binding ColumnVisibility}"/>
///   </Resources>
///   <DataGridTextColumn Visibility="{Binding Data.ShowContractId, Source={StaticResource ColumnVisibilityProxy}, Converter={StaticResource BooleanToVisibilityConverter}}"/>
/// </summary>
public class BindingProxy : Freezable, INotifyPropertyChanged
{
    protected override Freezable CreateInstanceCore()
    {
        return new BindingProxy();
    }

    public object Data
    {
        get => GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    public static readonly DependencyProperty DataProperty =
        DependencyProperty.Register(nameof(Data), typeof(object), typeof(BindingProxy), new UIPropertyMetadata(null, OnDataChanged));

    private static void OnDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is BindingProxy proxy)
        {
            // Unsubscribe from old object
            if (e.OldValue is INotifyPropertyChanged oldNotifier)
            {
                oldNotifier.PropertyChanged -= proxy.OnDataPropertyChanged;
            }

            // Subscribe to new object
            if (e.NewValue is INotifyPropertyChanged newNotifier)
            {
                newNotifier.PropertyChanged += proxy.OnDataPropertyChanged;
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnDataPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Forward property change events from the Data object with the "Data." prefix
        // This ensures bindings like {Binding Data.ShowContractId} update when ShowContractId changes
        if (!string.IsNullOrEmpty(e.PropertyName))
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Data." + e.PropertyName));
        }
    }
}
