using HelloID.Vault.Core.Models;
using HelloID.Vault.Services.Interfaces;
using System.Windows.Controls;
using System.Threading;

namespace HelloID.Vault.Management.Services;

/// <summary>
/// Service for managing DataGrid column persistence (visibility, order, widths).
/// Extracts column management logic from ViewModels for better separation of concerns.
/// </summary>
public class ColumnLayoutManager : IColumnLayoutManager
{
    private readonly IUserPreferencesService _userPreferencesService;

    public DataGrid? TargetDataGrid { get; set; }

    /// <summary>
    /// Gets the column visibility model for this manager.
    /// </summary>
    public ContractsColumnVisibility ColumnVisibility { get; }

    public ColumnLayoutManager(IUserPreferencesService userPreferencesService)
    {
        _userPreferencesService = userPreferencesService ?? throw new ArgumentNullException(nameof(userPreferencesService));
        ColumnVisibility = new ContractsColumnVisibility();
        System.Diagnostics.Debug.WriteLine($"[ColumnLayoutManager] Constructor called, created ColumnVisibility instance");
    }

    /// <summary>
    /// Initializes column visibility from preferences or defaults.
    /// Uses the saved instance directly if available.
    /// </summary>
    public void InitializeColumnVisibility()
    {
        var saved = _userPreferencesService.ContractsColumnVisibility;
        if (saved != null)
        {
            // Copy saved visibility to our instance
            // Use DisplayName (second element) as GetColumnVisibility expects display name
            foreach (var (_, displayName, propertyName) in ContractsColumnVisibility.AllColumns)
            {
                var savedVisibility = saved.GetColumnVisibility(displayName);
                ColumnVisibility.SetColumnVisibilityByProperty(propertyName, savedVisibility);
            }
        }
    }

    /// <summary>
    /// Detects and hides columns that have no data in the provided collection.
    /// Called after data import to automatically hide empty columns.
    /// </summary>
    public void UpdateEmptyColumnVisibility<T>(IEnumerable<T> data) where T : class
    {
        var dataList = data.ToList();
        if (dataList.Count == 0)
        {
            return;
        }

        foreach (var (columnName, _, propertyName) in ContractsColumnVisibility.AllColumns)
        {
            // Check if property exists on the data type
            var property = typeof(T).GetProperty(columnName);
            if (property == null)
            {
                continue;
            }

            // Check if any item has non-null/empty value for this column
            var hasData = dataList.Any(item =>
            {
                var value = property.GetValue(item);
                if (value == null) return false;

                // For strings, check if not whitespace
                if (value is string str)
                    return !string.IsNullOrWhiteSpace(str);

                // For other types, any non-null value counts as data
                return true;
            });

            // Only hide if completely empty - don't auto-show columns that were hidden
            var currentVisibility = ColumnVisibility.GetColumnVisibility(columnName);

            if (!hasData && currentVisibility)
            {
                ColumnVisibility.SetColumnVisibilityByProperty(propertyName, false);
            }
        }

        // Save updated visibility
        _userPreferencesService.ContractsColumnVisibility = ColumnVisibility;
    }

    /// <summary>
    /// Saves the current column order to preferences.
    /// Called from view when columns are reordered.
    /// </summary>
    public void SaveColumnOrder(List<string> columnNames)
    {
        _userPreferencesService.ContractsColumnOrder = columnNames;
    }

    /// <summary>
    /// Gets the saved column order from preferences.
    /// Called from view to restore column order.
    /// </summary>
    public List<string>? GetSavedColumnOrder()
    {
        return _userPreferencesService.ContractsColumnOrder;
    }

    /// <summary>
    /// Resets the saved column order to default (XAML order).
    /// Called from ColumnPickerDialog.
    /// </summary>
    public void ResetColumnOrder()
    {
        _userPreferencesService.ContractsColumnOrder = null;
    }

    /// <summary>
    /// Saves the current column widths to preferences.
    /// Called from view when column widths are changed.
    /// </summary>
    public void SaveColumnWidths(Dictionary<string, double> columnWidths)
    {
        _userPreferencesService.ContractsColumnWidths = columnWidths;
    }

    /// <summary>
    /// Gets the saved column widths from preferences.
    /// Called from view to restore column widths.
    /// </summary>
    public Dictionary<string, double>? GetSavedColumnWidths()
    {
        return _userPreferencesService.ContractsColumnWidths;
    }

    /// <summary>
    /// Resets the saved column widths to default (XAML widths).
    /// </summary>
    public void ResetColumnWidths()
    {
        _userPreferencesService.ContractsColumnWidths = null;
    }

    /// <summary>
    /// Shows all columns.
    /// </summary>
    public async Task ShowAllColumnsAsync()
    {
        System.Diagnostics.Debug.WriteLine("[ColumnLayoutManager] ========== ShowAllColumnsAsync START ==========");
        System.Diagnostics.Debug.WriteLine($"[ColumnLayoutManager] Thread: {System.Threading.Thread.CurrentThread.ManagedThreadId}");
        System.Diagnostics.Debug.WriteLine($"[ColumnLayoutManager] ColumnVisibility type: {ColumnVisibility?.GetType().Name}");
        System.Diagnostics.Debug.WriteLine($"[ColumnLayoutManager] ColumnVisibility is null: {ColumnVisibility == null}");

        // Directly set all properties to true for proper change notification
        ColumnVisibility.ShowContractId = true;
        ColumnVisibility.ShowExternalId = true;
        ColumnVisibility.ShowPersonId = true;
        ColumnVisibility.ShowPersonName = true;
        ColumnVisibility.ShowStartDate = true;
        ColumnVisibility.ShowEndDate = true;
        ColumnVisibility.ShowTypeCode = true;
        ColumnVisibility.ShowTypeDescription = true;
        ColumnVisibility.ShowFte = true;
        ColumnVisibility.ShowHoursPerWeek = true;
        ColumnVisibility.ShowPercentage = true;
        ColumnVisibility.ShowSequence = true;
        ColumnVisibility.ShowContractStatus = true;
        ColumnVisibility.ShowSource = true;
        ColumnVisibility.ShowManagerPersonId = true;
        ColumnVisibility.ShowManagerPersonName = true;
        ColumnVisibility.ShowLocationCode = true;
        ColumnVisibility.ShowLocationName = true;
        ColumnVisibility.ShowCostCenterCode = true;
        ColumnVisibility.ShowCostCenterName = true;
        ColumnVisibility.ShowCostBearerCode = true;
        ColumnVisibility.ShowCostBearerName = true;
        ColumnVisibility.ShowEmployerCode = true;
        ColumnVisibility.ShowEmployerName = true;
        ColumnVisibility.ShowTeamCode = true;
        ColumnVisibility.ShowTeamName = true;
        ColumnVisibility.ShowDepartmentId = true;
        ColumnVisibility.ShowDepartmentName = true;
        ColumnVisibility.ShowDepartmentCode = true;
        ColumnVisibility.ShowDepartmentManagerName = true;
        ColumnVisibility.ShowDepartmentParentDepartmentName = true;
        ColumnVisibility.ShowDivisionCode = true;
        ColumnVisibility.ShowDivisionName = true;
        ColumnVisibility.ShowTitleCode = true;
        ColumnVisibility.ShowTitleName = true;
        ColumnVisibility.ShowOrganizationCode = true;
        ColumnVisibility.ShowOrganizationName = true;

        System.Diagnostics.Debug.WriteLine("[ColumnLayoutManager] All properties set to true");

        _userPreferencesService.ContractsColumnVisibility = ColumnVisibility;
        await _userPreferencesService.SaveAsync();

        System.Diagnostics.Debug.WriteLine("[ColumnLayoutManager] ========== ShowAllColumnsAsync END ==========");
    }

    /// <summary>
    /// Hides columns that have no data (command version for UI binding).
    /// </summary>
    public void HideEmptyColumns<T>(IEnumerable<T> data) where T : class
    {
        UpdateEmptyColumnVisibility(data);
    }
}
