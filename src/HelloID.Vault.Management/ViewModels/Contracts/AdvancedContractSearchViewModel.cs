using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HelloID.Vault.Core.Models.DTOs;

namespace HelloID.Vault.Management.ViewModels.Contracts;

/// <summary>
/// ViewModel for Advanced Contract Search dialog.
/// </summary>
public partial class AdvancedContractSearchViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<FilterCriteriaDto> _filters = new();

    /// <summary>
    /// Available field names for filtering with display names.
    /// </summary>
    public List<FieldOption> AvailableFields { get; } = new()
    {
        new("ContractId", "Contract ID"),
        new("ExternalId", "External ID"),
        new("PersonId", "Person ID"),
        new("PersonName", "Person Name"),
        new("PersonExternalId", "Person External ID"),
        new("StartDate", "Start Date"),
        new("EndDate", "End Date"),
        new("TypeCode", "Type Code"),
        new("TypeDescription", "Type Description"),
        new("Fte", "FTE"),
        new("HoursPerWeek", "Hours Per Week"),
        new("Percentage", "Percentage"),
        new("Sequence", "Sequence"),
        new("ManagerPersonExternalId", "Manager Person External ID"),
        new("ManagerPersonName", "Manager Person Name"),
        new("LocationExternalId", "Location External ID"),
        new("LocationName", "Location Name"),
        new("CostCenterExternalId", "Cost Center External ID"),
        new("CostCenterName", "Cost Center Name"),
        new("CostBearerExternalId", "Cost Bearer External ID"),
        new("CostBearerName", "Cost Bearer Name"),
        new("EmployerExternalId", "Employer External ID"),
        new("EmployerName", "Employer Name"),
        new("TeamExternalId", "Team External ID"),
        new("TeamName", "Team Name"),
        new("DepartmentExternalId", "Department External ID"),
        new("DepartmentName", "Department Name"),
        new("DepartmentCode", "Department Code"),
        new("DepartmentParentExternalId", "Department Parent External ID"),
        new("DepartmentManagerPersonId", "Department Manager Person ID"),
        new("DepartmentManagerName", "Department Manager Name"),
        new("DepartmentParentDepartmentName", "Department Parent Department Name"),
        new("DivisionExternalId", "Division External ID"),
        new("DivisionName", "Division Name"),
        new("TitleExternalId", "Title External ID"),
        new("TitleName", "Title Name"),
        new("OrganizationExternalId", "Organization External ID"),
        new("OrganizationName", "Organization Name"),
        new("ContractStatus", "Contract Status"),
        new("ContractDateRange", "Contract Date Range")
    };

    /// <summary>
    /// Available operators for filtering.
    /// </summary>
    public List<string> AvailableOperators { get; } = new()
    {
        "Contains",
        "Equals",
        "Starts With",
        "Ends With",
        "Greater Than",
        "Less Than",
        "Is Empty",
        "Is Not Empty"
    };

    /// <summary>
    /// Event fired when filters are applied.
    /// </summary>
    public event EventHandler<List<FilterCriteriaDto>>? FiltersApplied;

    /// <summary>
    /// Event fired when filters are cleared.
    /// </summary>
    public event EventHandler? FiltersCleared;

    public AdvancedContractSearchViewModel()
    {
        // Start with one empty filter
        AddFilter();
    }

    /// <summary>
    /// Adds a new blank filter row.
    /// </summary>
    [RelayCommand]
    private void AddFilter()
    {
        var firstField = AvailableFields.FirstOrDefault();
        Filters.Add(new FilterCriteriaDto
        {
            FieldName = firstField?.FieldName ?? "PersonName",
            FieldDisplayName = firstField?.DisplayName ?? "Person Name",
            Operator = "Contains",
            Value = string.Empty
        });
    }

    /// <summary>
    /// Removes a specific filter row.
    /// </summary>
    [RelayCommand]
    private void RemoveFilter(FilterCriteriaDto filter)
    {
        Filters.Remove(filter);

        // Ensure at least one filter exists
        if (Filters.Count == 0)
        {
            AddFilter();
        }
    }

    /// <summary>
    /// Clears all filters from dialog and clears applied filters from main grid.
    /// </summary>
    [RelayCommand]
    private void ClearAll()
    {
        Filters.Clear();
        AddFilter();

        // Notify parent to clear applied filters
        FiltersCleared?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Applies filters without closing dialog.
    /// </summary>
    [RelayCommand]
    private void ApplyFilters()
    {
        // Get filters with values
        var validFilters = Filters.Where(f => !string.IsNullOrWhiteSpace(f.Value)).ToList();

        if (validFilters.Count == 0)
        {
            MessageBox.Show("Please enter at least one filter value.", "No Filters", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // Fire event to notify parent
        FiltersApplied?.Invoke(this, validFilters);
    }

    /// <summary>
    /// Closes dialog.
    /// </summary>
    [RelayCommand]
    private void Close(Window window)
    {
        window?.Close();
    }

    /// <summary>
    /// Represents a field option with internal name and display name.
    /// </summary>
    public class FieldOption
    {
        public string FieldName { get; set; }
        public string DisplayName { get; set; }

        public FieldOption(string fieldName, string displayName)
        {
            FieldName = fieldName;
            DisplayName = displayName;
        }
    }
}
