using HelloID.Vault.Core.Models;
using HelloID.Vault.Services;

namespace HelloID.Vault.Services.Interfaces;

/// <summary>
/// Service for managing user preferences that persist across application sessions.
/// </summary>
public interface IUserPreferencesService
{
    /// <summary>
    /// Gets or sets the last selected tab index in Person Detail view.
    /// 0 = Information, 1 = Contracts, 2 = Contacts
    /// </summary>
    int LastSelectedPersonTabIndex { get; set; }

    /// <summary>
    /// Gets or sets the last used Primary Manager logic during import.
    /// </summary>
    PrimaryManagerLogic LastPrimaryManagerLogic { get; set; }

    /// <summary>
    /// Gets or sets the last selected person ID in the persons list.
    /// </summary>
    string? LastSelectedPersonId { get; set; }

    /// <summary>
    /// Gets or sets the last search text used in the persons view.
    /// </summary>
    string? LastPersonSearchText { get; set; }

    /// <summary>
    /// Gets or sets column visibility settings for Contracts view.
    /// </summary>
    ContractsColumnVisibility? ContractsColumnVisibility { get; set; }

    /// <summary>
    /// Gets or sets whether column visibility has been auto-initialized for Contracts view.
    /// </summary>
    bool ContractsColumnVisibilityInitialized { get; set; }

    /// <summary>
    /// Gets or sets column display order for Contracts view.
    /// </summary>
    List<string>? ContractsColumnOrder { get; set; }

    /// <summary>
    /// Gets or sets column widths for Contracts view. Maps column SortMemberPath to width in pixels.
    /// </summary>
    Dictionary<string, double>? ContractsColumnWidths { get; set; }

    /// <summary>
    /// Gets or sets the last selected contract ID in Contracts view.
    /// </summary>
    int? LastSelectedContractId { get; set; }

    /// <summary>
    /// Gets or sets the last search text used in Contracts view.
    /// </summary>
    string? LastContractSearchText { get; set; }

    /// <summary>
    /// Gets or sets the last status filter in Contracts view ("All", "Past", "Active", "Future").
    /// </summary>
    string? LastContractStatusFilter { get; set; }

    /// <summary>
    /// Gets or sets the last advanced search filters in Contracts view.
    /// </summary>
    List<ContractFilterDto>? LastContractAdvancedFilters { get; set; }

    // Reference Data - Departments
    /// <summary>
    /// Gets or sets the last selected department code.
    /// </summary>
    string? LastSelectedDepartmentCode { get; set; }

    /// <summary>
    /// Gets or sets the last search text used in Departments view.
    /// </summary>
    string? LastDepartmentSearchText { get; set; }

    /// <summary>
    /// Gets or sets column display order for Departments view.
    /// </summary>
    List<string>? DepartmentsColumnOrder { get; set; }

    /// <summary>
    /// Gets or sets column widths for Departments view. Maps column SortMemberPath to width in pixels.
    /// </summary>
    Dictionary<string, double>? DepartmentsColumnWidths { get; set; }

    // Reference Data - Locations
    /// <summary>
    /// Gets or sets the last selected location code.
    /// </summary>
    string? LastSelectedLocationCode { get; set; }

    /// <summary>
    /// Gets or sets the last search text used in Locations view.
    /// </summary>
    string? LastLocationSearchText { get; set; }

    /// <summary>
    /// Gets or sets column display order for Locations view.
    /// </summary>
    List<string>? LocationsColumnOrder { get; set; }

    /// <summary>
    /// Gets or sets column widths for Locations view. Maps column SortMemberPath to width in pixels.
    /// </summary>
    Dictionary<string, double>? LocationsColumnWidths { get; set; }

    // Reference Data - Divisions
    /// <summary>
    /// Gets or sets the last selected division code.
    /// </summary>
    string? LastSelectedDivisionCode { get; set; }

    /// <summary>
    /// Gets or sets the last search text used in Divisions view.
    /// </summary>
    string? LastDivisionSearchText { get; set; }

    /// <summary>
    /// Gets or sets column display order for Divisions view.
    /// </summary>
    List<string>? DivisionsColumnOrder { get; set; }

    /// <summary>
    /// Gets or sets column widths for Divisions view. Maps column SortMemberPath to width in pixels.
    /// </summary>
    Dictionary<string, double>? DivisionsColumnWidths { get; set; }

    // Reference Data - Organizations
    /// <summary>
    /// Gets or sets the last selected organization code.
    /// </summary>
    string? LastSelectedOrganizationCode { get; set; }

    /// <summary>
    /// Gets or sets the last search text used in Organizations view.
    /// </summary>
    string? LastOrganizationSearchText { get; set; }

    /// <summary>
    /// Gets or sets column display order for Organizations view.
    /// </summary>
    List<string>? OrganizationsColumnOrder { get; set; }

    /// <summary>
    /// Gets or sets column widths for Organizations view. Maps column SortMemberPath to width in pixels.
    /// </summary>
    Dictionary<string, double>? OrganizationsColumnWidths { get; set; }

    // Reference Data - SourceSystems
    /// <summary>
    /// Gets or sets the last selected source system ID.
    /// </summary>
    string? LastSelectedSourceSystemId { get; set; }

    /// <summary>
    /// Gets or sets the last search text used in SourceSystems view.
    /// </summary>
    string? LastSourceSystemSearchText { get; set; }

    /// <summary>
    /// Gets or sets column display order for SourceSystems view.
    /// </summary>
    List<string>? SourceSystemsColumnOrder { get; set; }

    /// <summary>
    /// Gets or sets column widths for SourceSystems view. Maps column SortMemberPath to width in pixels.
    /// </summary>
    Dictionary<string, double>? SourceSystemsColumnWidths { get; set; }

    // Reference Data - CustomFields (uses FieldKey as identifier)
    /// <summary>
    /// Gets or sets the last selected custom field key.
    /// </summary>
    string? LastSelectedCustomFieldKey { get; set; }

    /// <summary>
    /// Gets or sets the last search text used in CustomFields view.
    /// </summary>
    string? LastCustomFieldSearchText { get; set; }

    /// <summary>
    /// Gets or sets column display order for CustomFields view.
    /// </summary>
    List<string>? CustomFieldsColumnOrder { get; set; }

    /// <summary>
    /// Gets or sets column widths for CustomFields view. Maps column SortMemberPath to width in pixels.
    /// </summary>
    Dictionary<string, double>? CustomFieldsColumnWidths { get; set; }

    // Contacts - separate from persons
    /// <summary>
    /// Gets or sets the last selected contact ID.
    /// </summary>
    int? LastSelectedContactId { get; set; }

    /// <summary>
    /// Gets or sets the last search text used in Contacts view.
    /// </summary>
    string? LastContactSearchText { get; set; }

    /// <summary>
    /// Gets or sets column display order for Contacts view.
    /// </summary>
    List<string>? ContactsColumnOrder { get; set; }

    /// <summary>
    /// Gets or sets column widths for Contacts view. Maps column SortMemberPath to width in pixels.
    /// </summary>
    Dictionary<string, double>? ContactsColumnWidths { get; set; }

    // Reference Data - Titles
    /// <summary>
    /// Gets or sets the last selected title code.
    /// </summary>
    string? LastSelectedTitleCode { get; set; }

    /// <summary>
    /// Gets or sets the last search text used in Titles view.
    /// </summary>
    string? LastTitleSearchText { get; set; }

    /// <summary>
    /// Gets or sets column display order for Titles view.
    /// </summary>
    List<string>? TitlesColumnOrder { get; set; }

    /// <summary>
    /// Gets or sets column widths for Titles view. Maps column SortMemberPath to width in pixels.
    /// </summary>
    Dictionary<string, double>? TitlesColumnWidths { get; set; }

    // Reference Data - Teams
    /// <summary>
    /// Gets or sets the last selected team code.
    /// </summary>
    string? LastSelectedTeamCode { get; set; }

    /// <summary>
    /// Gets or sets the last search text used in Teams view.
    /// </summary>
    string? LastTeamSearchText { get; set; }

    /// <summary>
    /// Gets or sets column display order for Teams view.
    /// </summary>
    List<string>? TeamsColumnOrder { get; set; }

    /// <summary>
    /// Gets or sets column widths for Teams view. Maps column SortMemberPath to width in pixels.
    /// </summary>
    Dictionary<string, double>? TeamsColumnWidths { get; set; }

    // Reference Data - Employers
    /// <summary>
    /// Gets or sets the last selected employer code.
    /// </summary>
    string? LastSelectedEmployerCode { get; set; }

    /// <summary>
    /// Gets or sets the last search text used in Employers view.
    /// </summary>
    string? LastEmployerSearchText { get; set; }

    /// <summary>
    /// Gets or sets column display order for Employers view.
    /// </summary>
    List<string>? EmployersColumnOrder { get; set; }

    /// <summary>
    /// Gets or sets column widths for Employers view. Maps column SortMemberPath to width in pixels.
    /// </summary>
    Dictionary<string, double>? EmployersColumnWidths { get; set; }

    // Reference Data - CostCenters
    /// <summary>
    /// Gets or sets the last selected cost center code.
    /// </summary>
    string? LastSelectedCostCenterCode { get; set; }

    /// <summary>
    /// Gets or sets the last search text used in CostCenters view.
    /// </summary>
    string? LastCostCenterSearchText { get; set; }

    /// <summary>
    /// Gets or sets column display order for CostCenters view.
    /// </summary>
    List<string>? CostCentersColumnOrder { get; set; }

    /// <summary>
    /// Gets or sets column widths for CostCenters view. Maps column SortMemberPath to width in pixels.
    /// </summary>
    Dictionary<string, double>? CostCentersColumnWidths { get; set; }

    // Reference Data - CostBearers
    /// <summary>
    /// Gets or sets the last selected cost bearer code.
    /// </summary>
    string? LastSelectedCostBearerCode { get; set; }

    /// <summary>
    /// Gets or sets the last search text used in CostBearers view.
    /// </summary>
    string? LastCostBearerSearchText { get; set; }

    /// <summary>
    /// Gets or sets column display order for CostBearers view.
    /// </summary>
    List<string>? CostBearersColumnOrder { get; set; }

    /// <summary>
    /// Gets or sets column widths for CostBearers view. Maps column SortMemberPath to width in pixels.
    /// </summary>
    Dictionary<string, double>? CostBearersColumnWidths { get; set; }

    /// <summary>
    /// Saves preferences to disk asynchronously.
    /// </summary>
    Task SaveAsync();

    /// <summary>
    /// Loads preferences from disk asynchronously.
    /// </summary>
    Task LoadAsync();
}
