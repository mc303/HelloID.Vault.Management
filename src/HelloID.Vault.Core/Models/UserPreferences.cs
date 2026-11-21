using HelloID.Vault.Core.Models.DTOs;

namespace HelloID.Vault.Core.Models;

/// <summary>
/// Model for user preferences stored in JSON.
/// </summary>
public class UserPreferences
{
    /// <summary>
    /// Last selected tab index in Person Detail view (0=Info, 1=Contracts, 2=Contacts)
    /// </summary>
    public int LastSelectedPersonTabIndex { get; set; } = 0;

    /// <summary>
    /// Last used Primary Manager logic during import.
    /// </summary>
    public PrimaryManagerLogic LastPrimaryManagerLogic { get; set; } = PrimaryManagerLogic.DepartmentBased;

    /// <summary>
    /// Last selected person ID in the persons list.
    /// </summary>
    public string? LastSelectedPersonId { get; set; }

    /// <summary>
    /// Last search text used in the persons view.
    /// </summary>
    public string? LastPersonSearchText { get; set; }

    /// <summary>
    /// Column visibility settings for Contracts view.
    /// </summary>
    public ContractsColumnVisibility? ContractsColumnVisibility { get; set; }

    /// <summary>
    /// Tracks whether column visibility has been auto-initialized for Contracts view.
    /// Set to false after import, then true after first Contracts view load.
    /// </summary>
    public bool ContractsColumnVisibilityInitialized { get; set; } = false;

    /// <summary>
    /// Column display order for Contracts view (column names in display order).
    /// </summary>
    public List<string>? ContractsColumnOrder { get; set; }

    /// <summary>
    /// Column widths for Contracts view. Maps column SortMemberPath to width in pixels.
    /// </summary>
    public Dictionary<string, double>? ContractsColumnWidths { get; set; }

    /// <summary>
    /// Last selected contract ID in Contracts view.
    /// </summary>
    public int? LastSelectedContractId { get; set; }

    /// <summary>
    /// Last search text used in Contracts view.
    /// </summary>
    public string? LastContractSearchText { get; set; }

    /// <summary>
    /// Last status filter in Contracts view ("All", "Past", "Active", "Future").
    /// </summary>
    public string? LastContractStatusFilter { get; set; }

    /// <summary>
    /// Last advanced search filters in Contracts view.
    /// </summary>
    public List<ContractFilterDto>? LastContractAdvancedFilters { get; set; }


    /// <summary>
    /// Delimiter used for CSV table export ("," ";" or tab). Null = ask on first export.
    /// </summary>
    public string? ExportDelimiter { get; set; }

    // Reference Data - Departments
    public string? LastSelectedDepartmentCode { get; set; }
    public string? LastDepartmentSearchText { get; set; }
    public List<string>? DepartmentsColumnOrder { get; set; }
    public Dictionary<string, double>? DepartmentsColumnWidths { get; set; }

    // Reference Data - Locations
    public string? LastSelectedLocationCode { get; set; }
    public string? LastLocationSearchText { get; set; }
    public List<string>? LocationsColumnOrder { get; set; }
    public Dictionary<string, double>? LocationsColumnWidths { get; set; }

    // Reference Data - CostBearers
    public string? LastSelectedCostBearerCode { get; set; }
    public string? LastCostBearerSearchText { get; set; }
    public List<string>? CostBearersColumnOrder { get; set; }
    public Dictionary<string, double>? CostBearersColumnWidths { get; set; }

    // Reference Data - Titles
    public string? LastSelectedTitleCode { get; set; }
    public string? LastTitleSearchText { get; set; }
    public List<string>? TitlesColumnOrder { get; set; }
    public Dictionary<string, double>? TitlesColumnWidths { get; set; }

    // Reference Data - CostCenters
    public string? LastSelectedCostCenterCode { get; set; }
    public string? LastCostCenterSearchText { get; set; }
    public List<string>? CostCentersColumnOrder { get; set; }
    public Dictionary<string, double>? CostCentersColumnWidths { get; set; }

    // Reference Data - Employers
    public string? LastSelectedEmployerCode { get; set; }
    public string? LastEmployerSearchText { get; set; }
    public List<string>? EmployersColumnOrder { get; set; }
    public Dictionary<string, double>? EmployersColumnWidths { get; set; }

    // Reference Data - Teams
    public string? LastSelectedTeamCode { get; set; }
    public string? LastTeamSearchText { get; set; }
    public List<string>? TeamsColumnOrder { get; set; }
    public Dictionary<string, double>? TeamsColumnWidths { get; set; }

    // Reference Data - Divisions
    public string? LastSelectedDivisionCode { get; set; }
    public string? LastDivisionSearchText { get; set; }
    public List<string>? DivisionsColumnOrder { get; set; }
    public Dictionary<string, double>? DivisionsColumnWidths { get; set; }

    // Reference Data - Organizations
    public string? LastSelectedOrganizationCode { get; set; }
    public string? LastOrganizationSearchText { get; set; }
    public List<string>? OrganizationsColumnOrder { get; set; }
    public Dictionary<string, double>? OrganizationsColumnWidths { get; set; }

    // Reference Data - SourceSystems
    public string? LastSelectedSourceSystemId { get; set; }
    public string? LastSourceSystemSearchText { get; set; }
    public List<string>? SourceSystemsColumnOrder { get; set; }
    public Dictionary<string, double>? SourceSystemsColumnWidths { get; set; }

    // Reference Data - CustomFields
    public string? LastSelectedCustomFieldKey { get; set; }
    public string? LastCustomFieldSearchText { get; set; }
    public List<string>? CustomFieldsColumnOrder { get; set; }
    public Dictionary<string, double>? CustomFieldsColumnWidths { get; set; }

    // Contacts (separate from persons)
    public int? LastSelectedContactId { get; set; }
    public string? LastContactSearchText { get; set; }
    public List<string>? ContactsColumnOrder { get; set; }
    public Dictionary<string, double>? ContactsColumnWidths { get; set; }

    /// <summary>
    /// Custom database path. If null or empty, uses default path.
    /// </summary>
    public string? DatabasePath { get; set; }

    /// <summary>
    /// Database type to use (SQLite or PostgreSQL/Supabase).
    /// </summary>
    public string DatabaseType { get; set; } = "Sqlite";

    /// <summary>
    /// Supabase URL (for reference only - not used for direct connection).
    /// </summary>
    public string? SupabaseUrl { get; set; }

    /// <summary>
    /// Encrypted Supabase connection string (PostgreSQL connection string).
    /// Format: Host=db.xxx.supabase.co;Port=5432;Database=postgres;Username=postgres;Password=...
    /// </summary>
    public string? SupabaseConnectionStringEncrypted { get; set; }

    /// <summary>
    /// Encrypted standard PostgreSQL connection string (self-hosted or local server).
    /// Format: Host=localhost;Port=5432;Database=helloid_vault;Username=postgres;Password=...
    /// </summary>
    public string? PostgreSqlConnectionStringEncrypted { get; set; }

    // Turso Database Settings
    /// <summary>
    /// Turso database URL (e.g., libsql://database-org.turso.io).
    /// </summary>
    public string? TursoDatabaseUrl { get; set; }

    /// <summary>
    /// Encrypted Turso authentication token.
    /// </summary>
    public string? TursoAuthTokenEncrypted { get; set; }

    // AD Correlation Settings
    public string? AdLdapHost { get; set; }
    public int AdLdapPort { get; set; } = 636;
    public bool AdUseLdaps { get; set; } = true;
    public string AdSearchBase { get; set; } = string.Empty;
    public string? AdUsername { get; set; }
    public string? AdEncryptedPassword { get; set; }
    public string AdCorrelationAttribute { get; set; } = "employeeID";
    public string AdVaultField { get; set; } = "external_id";
    public bool AdActiveContractOnly { get; set; } = true;
    public int AdFutureContractDays { get; set; } = 30;
    public string? AdMatchFieldsJson { get; set; }
    public int AdMinScore { get; set; } = 50;
    public int AdMaxCandidates { get; set; } = 3;
    public int AdAcceptAllThreshold { get; set; } = 95;

    /// <summary>
    /// Turso organization slug (for Platform API token refresh).
    /// </summary>
    public string? TursoOrganizationSlug { get; set; }

    /// <summary>
    /// Encrypted Turso Platform API token for database creation.
    /// </summary>
    public string? TursoPlatformApiTokenEncrypted { get; set; }

    /// <summary>
    /// Turso database name for auto-creation.
    /// </summary>
    public string? TursoDatabaseName { get; set; }
}
