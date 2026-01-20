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
public class BindingProxy : Freezable
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
        DependencyProperty.Register(nameof(Data), typeof(object), typeof(BindingProxy), new UIPropertyMetadata(null));
}
