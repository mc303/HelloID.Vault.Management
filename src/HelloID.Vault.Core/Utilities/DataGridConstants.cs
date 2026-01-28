using System.Collections.ObjectModel;

namespace HelloID.Vault.Core.Utilities;

/// <summary>
/// Shared constants and mappings for DataGrid column definitions.
/// Centralizes column metadata to avoid duplication across ViewModels and Views.
/// </summary>
public static class DataGridConstants
{
    /// <summary>
    /// Contract column definitions with property names, display names, and visibility property names.
    /// Used by both ContractsView.xaml.cs and ContractsColumnVisibility model.
    /// </summary>
    public static class Contracts
    {
        /// <summary>
        /// All column definitions for the Contracts DataGrid.
        /// Each tuple contains: (PropertyName, DisplayName, VisibilityPropertyName)
        /// </summary>
        public static ReadOnlyCollection<(string PropertyName, string DisplayName, string VisibilityPropertyName)> AllColumns { get; } = new([
            ("ContractId", "ID", "ShowContractId"),
            ("ExternalId", "External ID", "ShowExternalId"),
            ("PersonId", "Person ID", "ShowPersonId"),
            ("PersonName", "Person Name", "ShowPersonName"),
            ("StartDate", "Start Date", "ShowStartDate"),
            ("EndDate", "End Date", "ShowEndDate"),
            ("TypeCode", "Type Code", "ShowTypeCode"),
            ("TypeDescription", "Type Description", "ShowTypeDescription"),
            ("Fte", "FTE", "ShowFte"),
            ("HoursPerWeek", "Hours/Week", "ShowHoursPerWeek"),
            ("Percentage", "Percentage", "ShowPercentage"),
            ("Sequence", "Sequence", "ShowSequence"),
            ("ContractStatus", "Status", "ShowContractStatus"),
            ("Source", "Source", "ShowSource"),
            ("ManagerPersonExternalId", "Manager ExternalID", "ShowManagerPersonId"),
            ("ManagerPersonName", "Manager Name", "ShowManagerPersonName"),
            ("LocationCode", "Location Code", "ShowLocationCode"),
            ("LocationName", "Location Name", "ShowLocationName"),
            ("CostCenterCode", "Cost Center Code", "ShowCostCenterCode"),
            ("CostCenterName", "Cost Center Name", "ShowCostCenterName"),
            ("CostBearerCode", "Cost Bearer Code", "ShowCostBearerCode"),
            ("CostBearerName", "Cost Bearer Name", "ShowCostBearerName"),
            ("EmployerCode", "Employer Code", "ShowEmployerCode"),
            ("EmployerName", "Employer Name", "ShowEmployerName"),
            ("TeamCode", "Team Code", "ShowTeamCode"),
            ("TeamName", "Team Name", "ShowTeamName"),
            ("DepartmentExternalId", "Department ID", "ShowDepartmentId"),
            ("DepartmentName", "Department Name", "ShowDepartmentName"),
            ("DepartmentCode", "Department Code", "ShowDepartmentCode"),
            ("DepartmentManagerName", "Dept Manager", "ShowDepartmentManagerName"),
            ("DepartmentParentDepartmentName", "Parent Dept", "ShowDepartmentParentDepartmentName"),
            ("DivisionCode", "Division Code", "ShowDivisionCode"),
            ("DivisionName", "Division Name", "ShowDivisionName"),
            ("TitleCode", "Title Code", "ShowTitleCode"),
            ("TitleName", "Title Name", "ShowTitleName"),
            ("OrganizationCode", "Organization Code", "ShowOrganizationCode"),
            ("OrganizationName", "Organization Name", "ShowOrganizationName"),
        ]);

        /// <summary>
        /// Maps property names to visibility property names for ContractsColumnVisibility.
        /// </summary>
        public static readonly Dictionary<string, string> PropertyToVisibilityMap = new(StringComparer.OrdinalIgnoreCase)
        {
            { "ContractId", "ShowContractId" },
            { "ExternalId", "ShowExternalId" },
            { "PersonId", "ShowPersonId" },
            { "PersonName", "ShowPersonName" },
            { "StartDate", "ShowStartDate" },
            { "EndDate", "ShowEndDate" },
            { "TypeCode", "ShowTypeCode" },
            { "TypeDescription", "ShowTypeDescription" },
            { "Fte", "ShowFte" },
            { "HoursPerWeek", "ShowHoursPerWeek" },
            { "Percentage", "ShowPercentage" },
            { "Sequence", "ShowSequence" },
            { "ContractStatus", "ShowContractStatus" },
            { "Source", "ShowSource" },
            { "ManagerPersonExternalId", "ShowManagerPersonId" },
            { "ManagerPersonName", "ShowManagerPersonName" },
            { "LocationCode", "ShowLocationCode" },
            { "LocationName", "ShowLocationName" },
            { "CostCenterCode", "ShowCostCenterCode" },
            { "CostCenterName", "ShowCostCenterName" },
            { "CostBearerCode", "ShowCostBearerCode" },
            { "CostBearerName", "ShowCostBearerName" },
            { "EmployerCode", "ShowEmployerCode" },
            { "EmployerName", "ShowEmployerName" },
            { "TeamCode", "ShowTeamCode" },
            { "TeamName", "ShowTeamName" },
            { "DepartmentExternalId", "ShowDepartmentId" },
            { "DepartmentName", "ShowDepartmentName" },
            { "DepartmentCode", "ShowDepartmentCode" },
            { "DepartmentManagerName", "ShowDepartmentManagerName" },
            { "DepartmentParentDepartmentName", "ShowDepartmentParentDepartmentName" },
            { "DivisionCode", "ShowDivisionCode" },
            { "DivisionName", "ShowDivisionName" },
            { "TitleCode", "ShowTitleCode" },
            { "TitleName", "ShowTitleName" },
            { "OrganizationCode", "ShowOrganizationCode" },
            { "OrganizationName", "ShowOrganizationName" },
        };

        /// <summary>
        /// Maps visibility property names to display names (column headers).
        /// </summary>
        public static readonly Dictionary<string, string> VisibilityPropertyToDisplayName = new(StringComparer.OrdinalIgnoreCase)
        {
            { "ShowContractId", "ID" },
            { "ShowExternalId", "External ID" },
            { "ShowPersonId", "Person ID" },
            { "ShowPersonName", "Person Name" },
            { "ShowStartDate", "Start Date" },
            { "ShowEndDate", "End Date" },
            { "ShowTypeCode", "Type Code" },
            { "ShowTypeDescription", "Type Description" },
            { "ShowFte", "FTE" },
            { "ShowHoursPerWeek", "Hours/Week" },
            { "ShowPercentage", "Percentage" },
            { "ShowSequence", "Sequence" },
            { "ShowContractStatus", "Status" },
            { "ShowSource", "Source" },
            { "ShowManagerPersonId", "Manager ExternalID" },
            { "ShowManagerPersonName", "Manager Name" },
            { "ShowLocationCode", "Location Code" },
            { "ShowLocationName", "Location Name" },
            { "ShowCostCenterCode", "Cost Center Code" },
            { "ShowCostCenterName", "Cost Center Name" },
            { "ShowCostBearerCode", "Cost Bearer Code" },
            { "ShowCostBearerName", "Cost Bearer Name" },
            { "ShowEmployerCode", "Employer Code" },
            { "ShowEmployerName", "Employer Name" },
            { "ShowTeamCode", "Team Code" },
            { "ShowTeamName", "Team Name" },
            { "ShowDepartmentId", "Department ID" },
            { "ShowDepartmentName", "Department Name" },
            { "ShowDepartmentCode", "Department Code" },
            { "ShowDepartmentManagerName", "Dept Manager" },
            { "ShowDepartmentParentDepartmentName", "Parent Dept" },
            { "ShowDivisionCode", "Division Code" },
            { "ShowDivisionName", "Division Name" },
            { "ShowTitleCode", "Title Code" },
            { "ShowTitleName", "Title Name" },
            { "ShowOrganizationCode", "Organization Code" },
            { "ShowOrganizationName", "Organization Name" },
        };

        /// <summary>
        /// Properties that contain date values requiring special formatting.
        /// </summary>
        public static readonly string[] DateProperties = { "StartDate", "EndDate" };

        /// <summary>
        /// Default column widths in pixels.
        /// </summary>
        public static readonly Dictionary<string, double> DefaultColumnWidths = new(StringComparer.OrdinalIgnoreCase)
        {
            { "ContractId", 80 },
            { "ExternalId", 120 },
            { "PersonId", 150 },
            { "PersonName", 180 },
            { "StartDate", 100 },
            { "EndDate", 100 },
            { "TypeCode", 90 },
            { "TypeDescription", 150 },
            { "Fte", 70 },
            { "HoursPerWeek", 90 },
            { "Percentage", 90 },
            { "Sequence", 80 },
            { "ContractStatus", 90 },
            { "Source", 150 },
            { "ManagerPersonExternalId", 150 },
            { "ManagerPersonName", 180 },
            { "LocationCode", 120 },
            { "LocationName", 180 },
            { "CostCenterCode", 120 },
            { "CostCenterName", 180 },
            { "CostBearerCode", 120 },
            { "CostBearerName", 180 },
            { "EmployerCode", 120 },
            { "EmployerName", 180 },
            { "TeamCode", 120 },
            { "TeamName", 180 },
            { "DepartmentExternalId", 150 },
            { "DepartmentName", 180 },
            { "DepartmentCode", 120 },
            { "DepartmentManagerName", 180 },
            { "DepartmentParentDepartmentName", 180 },
            { "DivisionCode", 120 },
            { "DivisionName", 180 },
            { "TitleCode", 120 },
            { "TitleName", 180 },
            { "OrganizationCode", 120 },
            { "OrganizationName", 180 },
        };
    }
}
