using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace HelloID.Vault.Core.Models;

/// <summary>
/// Represents visibility state for a DataGrid column.
/// </summary>
public class ColumnVisibility : ObservableObject
{
    private bool _isVisible = true;

    /// <summary>
    /// Unique identifier for the column (matches DataGridColumn binding path or x:Name).
    /// </summary>
    public string ColumnName { get; set; } = string.Empty;

    /// <summary>
    /// Display name shown in the column picker UI.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Whether the column is currently visible.
    /// </summary>
    public bool IsVisible
    {
        get => _isVisible;
        set => SetProperty(ref _isVisible, value);
    }
}
