using HelloID.Vault.Core.Models;
using System.Windows.Controls;

namespace HelloID.Vault.Management.Services;

/// <summary>
/// Service for managing DataGrid column persistence (visibility, order, widths).
/// Extracts column management logic from ViewModels for better separation of concerns.
/// </summary>
public interface IColumnLayoutManager
{
    /// <summary>
    /// Gets or sets the target DataGrid for column operations.
    /// </summary>
    DataGrid? TargetDataGrid { get; set; }

    /// <summary>
    /// Gets the column visibility model for this manager.
    /// </summary>
    ContractsColumnVisibility ColumnVisibility { get; }

    /// <summary>
    /// Initializes column visibility from preferences or defaults.
    /// </summary>
    void InitializeColumnVisibility();

    /// <summary>
    /// Detects and hides columns that have no data in the provided collection.
    /// </summary>
    /// <typeparam name="T">The type of data items</typeparam>
    /// <param name="data">The data collection to check for empty columns</param>
    void UpdateEmptyColumnVisibility<T>(IEnumerable<T> data) where T : class;

    /// <summary>
    /// Saves the current column order to preferences.
    /// </summary>
    /// <param name="columnNames">Ordered list of column names (SortMemberPath)</param>
    void SaveColumnOrder(List<string> columnNames);

    /// <summary>
    /// Gets the saved column order from preferences.
    /// </summary>
    /// <returns>Ordered list of column names, or null if not saved</returns>
    List<string>? GetSavedColumnOrder();

    /// <summary>
    /// Resets the saved column order to default (XAML order).
    /// </summary>
    void ResetColumnOrder();

    /// <summary>
    /// Saves the current column widths to preferences.
    /// </summary>
    /// <param name="columnWidths">Dictionary mapping column names to widths</param>
    void SaveColumnWidths(Dictionary<string, double> columnWidths);

    /// <summary>
    /// Gets the saved column widths from preferences.
    /// </summary>
    /// <returns>Dictionary mapping column names to widths, or null if not saved</returns>
    Dictionary<string, double>? GetSavedColumnWidths();

    /// <summary>
    /// Resets the saved column widths to default (XAML widths).
    /// </summary>
    void ResetColumnWidths();

    /// <summary>
    /// Shows all columns by setting visibility to true for all columns.
    /// </summary>
    Task ShowAllColumnsAsync();

    /// <summary>
    /// Hides columns that have no data.
    /// </summary>
    /// <typeparam name="T">The type of data items</typeparam>
    /// <param name="data">The data collection to check for empty columns</param>
    void HideEmptyColumns<T>(IEnumerable<T> data) where T : class;
}
