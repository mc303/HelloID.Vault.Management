using CommunityToolkit.Mvvm.ComponentModel;

namespace HelloID.Vault.Core.Models;

/// <summary>
/// Represents visibility state for all columns in the Contracts DataGrid.
/// Uses individual boolean properties for reliable DataGridColumn Visibility binding.
/// </summary>
public partial class ContractsColumnVisibility : ObservableObject
{
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
    [ObservableProperty] private bool _showLocationId = true;
    [ObservableProperty] private bool _showLocationName = true;
    [ObservableProperty] private bool _showCostCenterId = true;
    [ObservableProperty] private bool _showCostCenterName = true;
    [ObservableProperty] private bool _showCostBearerId = true;
    [ObservableProperty] private bool _showCostBearerName = true;
    [ObservableProperty] private bool _showEmployerId = true;
    [ObservableProperty] private bool _showEmployerName = true;
    [ObservableProperty] private bool _showTeamId = true;
    [ObservableProperty] private bool _showTeamName = true;
    [ObservableProperty] private bool _showDepartmentId = true;
    [ObservableProperty] private bool _showDepartmentName = true;
    [ObservableProperty] private bool _showDepartmentCode = true;
    [ObservableProperty] private bool _showDepartmentManagerName = true;
    [ObservableProperty] private bool _showDepartmentParentDepartmentName = true;
    [ObservableProperty] private bool _showDivisionId = true;
    [ObservableProperty] private bool _showDivisionName = true;
    [ObservableProperty] private bool _showTitleId = true;
    [ObservableProperty] private bool _showTitleName = true;
    [ObservableProperty] private bool _showOrganizationId = true;
    [ObservableProperty] private bool _showOrganizationName = true;

    /// <summary>
    /// Column name to property mapping for reflection-based operations.
    /// </summary>
    private static readonly Dictionary<string, string> ColumnToPropertyMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { "ContractId", nameof(ShowContractId) },
        { "ExternalId", nameof(ShowExternalId) },
        { "PersonId", nameof(ShowPersonId) },
        { "PersonName", nameof(ShowPersonName) },
        { "StartDate", nameof(ShowStartDate) },
        { "EndDate", nameof(ShowEndDate) },
        { "TypeCode", nameof(ShowTypeCode) },
        { "TypeDescription", nameof(ShowTypeDescription) },
        { "Fte", nameof(ShowFte) },
        { "HoursPerWeek", nameof(ShowHoursPerWeek) },
        { "Percentage", nameof(ShowPercentage) },
        { "Sequence", nameof(ShowSequence) },
        { "ContractStatus", nameof(ShowContractStatus) },
        { "Source", nameof(ShowSource) },
        { "ManagerPersonId", nameof(ShowManagerPersonId) },
        { "ManagerPersonName", nameof(ShowManagerPersonName) },
        { "LocationId", nameof(ShowLocationId) },
        { "LocationName", nameof(ShowLocationName) },
        { "CostCenterId", nameof(ShowCostCenterId) },
        { "CostCenterName", nameof(ShowCostCenterName) },
        { "CostBearerId", nameof(ShowCostBearerId) },
        { "CostBearerName", nameof(ShowCostBearerName) },
        { "EmployerId", nameof(ShowEmployerId) },
        { "EmployerName", nameof(ShowEmployerName) },
        { "TeamId", nameof(ShowTeamId) },
        { "TeamName", nameof(ShowTeamName) },
        { "DepartmentId", nameof(ShowDepartmentId) },
        { "DepartmentName", nameof(ShowDepartmentName) },
        { "DepartmentCode", nameof(ShowDepartmentCode) },
        { "DepartmentManagerName", nameof(ShowDepartmentManagerName) },
        { "DepartmentParentDepartmentName", nameof(ShowDepartmentParentDepartmentName) },
        { "DivisionId", nameof(ShowDivisionId) },
        { "DivisionName", nameof(ShowDivisionName) },
        { "TitleId", nameof(ShowTitleId) },
        { "TitleName", nameof(ShowTitleName) },
        { "OrganizationId", nameof(ShowOrganizationId) },
        { "OrganizationName", nameof(ShowOrganizationName) },
    };

    /// <summary>
    /// Gets all column definitions with display names.
    /// </summary>
    public static List<(string ColumnName, string DisplayName, string PropertyName)> AllColumns { get; } = new()
    {
        ("ContractId", "ID", nameof(ShowContractId)),
        ("ExternalId", "External ID", nameof(ShowExternalId)),
        ("PersonId", "Person ID", nameof(ShowPersonId)),
        ("PersonName", "Person Name", nameof(ShowPersonName)),
        ("StartDate", "Start Date", nameof(ShowStartDate)),
        ("EndDate", "End Date", nameof(ShowEndDate)),
        ("TypeCode", "Type Code", nameof(ShowTypeCode)),
        ("TypeDescription", "Type Description", nameof(ShowTypeDescription)),
        ("Fte", "FTE", nameof(ShowFte)),
        ("HoursPerWeek", "Hours/Week", nameof(ShowHoursPerWeek)),
        ("Percentage", "Percentage", nameof(ShowPercentage)),
        ("Sequence", "Sequence", nameof(ShowSequence)),
        ("ContractStatus", "Status", nameof(ShowContractStatus)),
        ("Source", "Source", nameof(ShowSource)),
        ("ManagerPersonId", "Manager ID", nameof(ShowManagerPersonId)),
        ("ManagerPersonName", "Manager Name", nameof(ShowManagerPersonName)),
        ("LocationId", "Location Code", nameof(ShowLocationId)),
        ("LocationName", "Location Name", nameof(ShowLocationName)),
        ("CostCenterId", "Cost Center Code", nameof(ShowCostCenterId)),
        ("CostCenterName", "Cost Center Name", nameof(ShowCostCenterName)),
        ("CostBearerId", "Cost Bearer Code", nameof(ShowCostBearerId)),
        ("CostBearerName", "Cost Bearer Name", nameof(ShowCostBearerName)),
        ("EmployerId", "Employer Code", nameof(ShowEmployerId)),
        ("EmployerName", "Employer Name", nameof(ShowEmployerName)),
        ("TeamId", "Team Code", nameof(ShowTeamId)),
        ("TeamName", "Team Name", nameof(ShowTeamName)),
        ("DepartmentId", "Department ID", nameof(ShowDepartmentId)),
        ("DepartmentName", "Department Name", nameof(ShowDepartmentName)),
        ("DepartmentCode", "Department Code", nameof(ShowDepartmentCode)),
        ("DepartmentManagerName", "Dept Manager", nameof(ShowDepartmentManagerName)),
        ("DepartmentParentDepartmentName", "Parent Dept", nameof(ShowDepartmentParentDepartmentName)),
        ("DivisionId", "Division Code", nameof(ShowDivisionId)),
        ("DivisionName", "Division Name", nameof(ShowDivisionName)),
        ("TitleId", "Title Code", nameof(ShowTitleId)),
        ("TitleName", "Title Name", nameof(ShowTitleName)),
        ("OrganizationId", "Organization Code", nameof(ShowOrganizationId)),
        ("OrganizationName", "Organization Name", nameof(ShowOrganizationName)),
    };

    /// <summary>
    /// Sets visibility for a specific column by name.
    /// </summary>
    public void SetColumnVisibility(string columnName, bool isVisible)
    {
        if (ColumnToPropertyMap.TryGetValue(columnName, out var propertyName))
        {
            var property = GetType().GetProperty(propertyName);
            property?.SetValue(this, isVisible);
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
