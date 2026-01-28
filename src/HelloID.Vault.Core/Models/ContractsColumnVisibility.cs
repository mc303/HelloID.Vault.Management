using CommunityToolkit.Mvvm.ComponentModel;
using HelloID.Vault.Core.Utilities;
using System.Collections.ObjectModel;

namespace HelloID.Vault.Core.Models;

/// <summary>
/// Represents visibility state for all columns in the Contracts DataGrid.
/// Uses individual boolean properties for reliable DataGridColumn Visibility binding.
/// </summary>
public partial class ContractsColumnVisibility : ObservableObject
{
    /// <summary>
    /// Constructor - subscribes to property changed for debug logging.
    /// </summary>
    public ContractsColumnVisibility()
    {
        PropertyChanged += (s, e) => LogColumnChange(e.PropertyName);
    }
    [ObservableProperty] private bool _showContractId = true;
    [ObservableProperty] private bool _showExternalId = true;
    [ObservableProperty] private bool _showPersonId = true;
    [ObservableProperty] private bool _showPersonName = true;
    [ObservableProperty] private bool _showStartDate = true;
    [ObservableProperty] private bool _showEndDate = true;
    [ObservableProperty] private bool _showTypeCode = true;
    [ObservableProperty] private bool _showTypeDescription = true;
    [ObservableProperty] private bool _showFte = true;
    [ObservableProperty] private bool _showHoursPerWeek = true;
    [ObservableProperty] private bool _showPercentage = true;
    [ObservableProperty] private bool _showSequence = true;
    [ObservableProperty] private bool _showContractStatus = true;
    [ObservableProperty] private bool _showSource = true;
    [ObservableProperty] private bool _showManagerPersonId = true;
    [ObservableProperty] private bool _showManagerPersonName = true;
    [ObservableProperty] private bool _showLocationCode = true;
    [ObservableProperty] private bool _showLocationName = true;
    [ObservableProperty] private bool _showCostCenterCode = true;
    [ObservableProperty] private bool _showCostCenterName = true;
    [ObservableProperty] private bool _showCostBearerCode = true;
    [ObservableProperty] private bool _showCostBearerName = true;
    [ObservableProperty] private bool _showEmployerCode = true;
    [ObservableProperty] private bool _showEmployerName = true;
    [ObservableProperty] private bool _showTeamCode = true;
    [ObservableProperty] private bool _showTeamName = true;
    [ObservableProperty] private bool _showDepartmentId = true;
    [ObservableProperty] private bool _showDepartmentName = true;
    [ObservableProperty] private bool _showDepartmentCode = true;
    [ObservableProperty] private bool _showDepartmentManagerName = true;
    [ObservableProperty] private bool _showDepartmentParentDepartmentName = true;
    [ObservableProperty] private bool _showDivisionCode = true;
    [ObservableProperty] private bool _showDivisionName = true;
    [ObservableProperty] private bool _showTitleCode = true;
    [ObservableProperty] private bool _showTitleName = true;
    [ObservableProperty] private bool _showOrganizationCode = true;
    [ObservableProperty] private bool _showOrganizationName = true;

    /// <summary>
    /// Column name (display name) to visibility property mapping.
    /// Uses centralized DataGridConstants for consistency.
    /// </summary>
    private static readonly Dictionary<string, string> ColumnToPropertyMap = DataGridConstants.Contracts.VisibilityPropertyToDisplayName
        .ToDictionary(kvp => kvp.Value, kvp => kvp.Key, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets all column definitions from centralized DataGridConstants.
    /// </summary>
    public static ReadOnlyCollection<(string ColumnName, string DisplayName, string PropertyName)> AllColumns =>
        DataGridConstants.Contracts.AllColumns;

    /// <summary>
    /// Sets visibility for a specific column by name (display name like "Cost Bearer Code").
    /// </summary>
    public void SetColumnVisibility(string columnName, bool isVisible)
    {
        System.Diagnostics.Debug.WriteLine($"[SetColumnVisibility] Called with columnName={columnName}, isVisible={isVisible}");
        
        if (ColumnToPropertyMap.TryGetValue(columnName, out var propertyName))
        {
            var property = GetType().GetProperty(propertyName);
            System.Diagnostics.Debug.WriteLine($"[SetColumnVisibility] Found property: {propertyName}");
            property?.SetValue(this, isVisible);

            // Log the change
            LogColumnChange(propertyName);

            // Manually raise PropertyChanged since reflection bypasses the generated setter
            System.Diagnostics.Debug.WriteLine($"[SetColumnVisibility] Raising OnPropertyChanged for: {propertyName}");
            base.OnPropertyChanged(propertyName);
            System.Diagnostics.Debug.WriteLine($"[SetColumnVisibility] PropertyChanged raised");
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"[SetColumnVisibility] WARNING: Column '{columnName}' not found in ColumnToPropertyMap!");
        }
    }

    /// <summary>
    /// Sets visibility for a specific column by property name (like "ShowCostBearerCode").
    /// </summary>
    public void SetColumnVisibilityByProperty(string propertyName, bool isVisible)
    {
        System.Diagnostics.Debug.WriteLine($"[SetColumnVisibilityByProperty] Called with propertyName={propertyName}, isVisible={isVisible}");
        
        var property = GetType().GetProperty(propertyName);
        if (property != null)
        {
            property?.SetValue(this, isVisible);

            // Log the change
            LogColumnChange(propertyName);

            // Manually raise PropertyChanged since reflection bypasses the generated setter
            System.Diagnostics.Debug.WriteLine($"[SetColumnVisibilityByProperty] Raising OnPropertyChanged for: {propertyName}");
            base.OnPropertyChanged(propertyName);
            System.Diagnostics.Debug.WriteLine($"[SetColumnVisibilityByProperty] PropertyChanged raised");
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"[SetColumnVisibilityByProperty] WARNING: Property '{propertyName}' not found!");
        }
    }

    /// <summary>
    /// Logs column visibility changes - call this from property changed callbacks.
    /// </summary>
    public void LogColumnChange(string? propertyName)
    {
        if (!string.IsNullOrEmpty(propertyName))
        {
            var column = AllColumns.FirstOrDefault(c => c.PropertyName == propertyName);
            if (column.ColumnName != null)
            {
                var property = GetType().GetProperty(propertyName);
                var isVisible = (bool)(property?.GetValue(this) ?? true);
                System.Diagnostics.Debug.WriteLine($"[ColumnVisibility] {column.DisplayName} ({column.ColumnName}): {(isVisible ? "SHOW" : "HIDE")}");
            }
        }
    }

    /// <summary>
    /// Gets visibility for a specific column by name.
    /// </summary>
    public bool GetColumnVisibility(string columnName)
    {
        if (ColumnToPropertyMap.TryGetValue(columnName, out var propertyName))
        {
            var property = GetType().GetProperty(propertyName);
            return (bool)(property?.GetValue(this) ?? true);
        }
        return true;
    }
}
