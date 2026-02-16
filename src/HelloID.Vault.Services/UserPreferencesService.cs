using System.Text.Json;
using HelloID.Vault.Services.Interfaces;
using HelloID.Vault.Services.Security;
using HelloID.Vault.Core.Models;
using HelloID.Vault.Data.Connection;

namespace HelloID.Vault.Services;

/// <summary>
/// Service for managing user preferences that persist to disk.
/// Stores preferences in the user's AppData folder.
/// </summary>
public class UserPreferencesService : IUserPreferencesService
{
    private readonly string _preferencesFilePath;
    private UserPreferences _preferences;

    public UserPreferencesService()
    {
        // Store in user's AppData folder: C:\Users\{username}\AppData\Roaming\HelloID.Vault.Management
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appFolder = Path.Combine(appDataPath, "HelloID.Vault.Management");
        Directory.CreateDirectory(appFolder);
        _preferencesFilePath = Path.Combine(appFolder, "user-preferences.json");

        _preferences = new UserPreferences();
    }

    public int LastSelectedPersonTabIndex
    {
        get => _preferences.LastSelectedPersonTabIndex;
        set
        {
            if (_preferences.LastSelectedPersonTabIndex != value)
            {
                _preferences.LastSelectedPersonTabIndex = value;
                // Fire and forget - save asynchronously without blocking
                _ = SaveAsync();
            }
        }
    }

    public PrimaryManagerLogic LastPrimaryManagerLogic
    {
        get => _preferences.LastPrimaryManagerLogic;
        set
        {
            if (_preferences.LastPrimaryManagerLogic != value)
            {
                _preferences.LastPrimaryManagerLogic = value;
                _ = SaveAsync();
            }
        }
    }

    public string? LastSelectedPersonId
    {
        get => _preferences.LastSelectedPersonId;
        set
        {
            if (_preferences.LastSelectedPersonId != value)
            {
                _preferences.LastSelectedPersonId = value;
                _ = SaveAsync();
            }
        }
    }

    public string? LastPersonSearchText
    {
        get => _preferences.LastPersonSearchText;
        set
        {
            if (_preferences.LastPersonSearchText != value)
            {
                _preferences.LastPersonSearchText = value;
                _ = SaveAsync();
            }
        }
    }

    public ContractsColumnVisibility? ContractsColumnVisibility
    {
        get => _preferences.ContractsColumnVisibility;
        set
        {
            // Always save when column visibility changes - need deep comparison for reference types
            // Since ContractsColumnVisibility is mutable and properties change via CheckBox bindings,
            // we assign and save to ensure persistence
            System.Diagnostics.Debug.WriteLine($"[UserPreferencesService] ContractsColumnVisibility SET called");
            System.Diagnostics.Debug.WriteLine($"[UserPreferencesService]   New value hash: {value?.GetHashCode()}");
            System.Diagnostics.Debug.WriteLine($"[UserPreferencesService]   ShowContractId: {value?.ShowContractId}, ShowExternalId: {value?.ShowExternalId}");
            _preferences.ContractsColumnVisibility = value;
            System.Diagnostics.Debug.WriteLine($"[UserPreferencesService]   Calling SaveAsync()");
            _ = SaveAsync();
        }
    }

    public bool ContractsColumnVisibilityInitialized
    {
        get => _preferences.ContractsColumnVisibilityInitialized;
        set
        {
            _preferences.ContractsColumnVisibilityInitialized = value;
            _ = SaveAsync();
        }
    }

    public List<string>? ContractsColumnOrder
    {
        get => _preferences.ContractsColumnOrder;
        set
        {
            _preferences.ContractsColumnOrder = value;
            _ = SaveAsync();
        }
    }

    public Dictionary<string, double>? ContractsColumnWidths
    {
        get => _preferences.ContractsColumnWidths;
        set
        {
            _preferences.ContractsColumnWidths = value;
            _ = SaveAsync();
        }
    }

    public int? LastSelectedContractId
    {
        get => _preferences.LastSelectedContractId;
        set
        {
            if (_preferences.LastSelectedContractId != value)
            {
                _preferences.LastSelectedContractId = value;
                _ = SaveAsync();
            }
        }
    }

    public string? LastContractSearchText
    {
        get => _preferences.LastContractSearchText;
        set
        {
            if (_preferences.LastContractSearchText != value)
            {
                _preferences.LastContractSearchText = value;
                _ = SaveAsync();
            }
        }
    }

    public string? LastContractStatusFilter
    {
        get => _preferences.LastContractStatusFilter;
        set
        {
            if (_preferences.LastContractStatusFilter != value)
            {
                _preferences.LastContractStatusFilter = value;
                _ = SaveAsync();
            }
        }
    }

    public List<ContractFilterDto>? LastContractAdvancedFilters
    {
        get => _preferences.LastContractAdvancedFilters;
        set
        {
            _preferences.LastContractAdvancedFilters = value;
            _ = SaveAsync();
        }
    }

    // Reference Data - Departments
    public string? LastSelectedDepartmentCode
    {
        get => _preferences.LastSelectedDepartmentCode;
        set
        {
            if (_preferences.LastSelectedDepartmentCode != value)
            {
                _preferences.LastSelectedDepartmentCode = value;
                _ = SaveAsync();
            }
        }
    }

    public string? LastDepartmentSearchText
    {
        get => _preferences.LastDepartmentSearchText;
        set
        {
            if (_preferences.LastDepartmentSearchText != value)
            {
                _preferences.LastDepartmentSearchText = value;
                _ = SaveAsync();
            }
        }
    }

    public List<string>? DepartmentsColumnOrder
    {
        get => _preferences.DepartmentsColumnOrder;
        set
        {
            _preferences.DepartmentsColumnOrder = value;
            _ = SaveAsync();
        }
    }

    public Dictionary<string, double>? DepartmentsColumnWidths
    {
        get => _preferences.DepartmentsColumnWidths;
        set
        {
            _preferences.DepartmentsColumnWidths = value;
            _ = SaveAsync();
        }
    }

    // Reference Data - Locations
    public string? LastSelectedLocationCode
    {
        get => _preferences.LastSelectedLocationCode;
        set
        {
            if (_preferences.LastSelectedLocationCode != value)
            {
                _preferences.LastSelectedLocationCode = value;
                _ = SaveAsync();
            }
        }
    }

    public string? LastLocationSearchText
    {
        get => _preferences.LastLocationSearchText;
        set
        {
            if (_preferences.LastLocationSearchText != value)
            {
                _preferences.LastLocationSearchText = value;
                _ = SaveAsync();
            }
        }
    }

    public List<string>? LocationsColumnOrder
    {
        get => _preferences.LocationsColumnOrder;
        set
        {
            _preferences.LocationsColumnOrder = value;
            _ = SaveAsync();
        }
    }

    public Dictionary<string, double>? LocationsColumnWidths
    {
        get => _preferences.LocationsColumnWidths;
        set
        {
            _preferences.LocationsColumnWidths = value;
            _ = SaveAsync();
        }
    }

    // Reference Data - CostBearers
    public string? LastSelectedCostBearerCode
    {
        get => _preferences.LastSelectedCostBearerCode;
        set
        {
            if (_preferences.LastSelectedCostBearerCode != value)
            {
                _preferences.LastSelectedCostBearerCode = value;
                _ = SaveAsync();
            }
        }
    }

    public string? LastCostBearerSearchText
    {
        get => _preferences.LastCostBearerSearchText;
        set
        {
            if (_preferences.LastCostBearerSearchText != value)
            {
                _preferences.LastCostBearerSearchText = value;
                _ = SaveAsync();
            }
        }
    }

    public List<string>? CostBearersColumnOrder
    {
        get => _preferences.CostBearersColumnOrder;
        set
        {
            _preferences.CostBearersColumnOrder = value;
            _ = SaveAsync();
        }
    }

    public Dictionary<string, double>? CostBearersColumnWidths
    {
        get => _preferences.CostBearersColumnWidths;
        set
        {
            _preferences.CostBearersColumnWidths = value;
            _ = SaveAsync();
        }
    }

    // Reference Data - Titles
    public string? LastSelectedTitleCode
    {
        get => _preferences.LastSelectedTitleCode;
        set
        {
            if (_preferences.LastSelectedTitleCode != value)
            {
                _preferences.LastSelectedTitleCode = value;
                _ = SaveAsync();
            }
        }
    }

    public string? LastTitleSearchText
    {
        get => _preferences.LastTitleSearchText;
        set
        {
            if (_preferences.LastTitleSearchText != value)
            {
                _preferences.LastTitleSearchText = value;
                _ = SaveAsync();
            }
        }
    }

    public List<string>? TitlesColumnOrder
    {
        get => _preferences.TitlesColumnOrder;
        set
        {
            _preferences.TitlesColumnOrder = value;
            _ = SaveAsync();
        }
    }

    public Dictionary<string, double>? TitlesColumnWidths
    {
        get => _preferences.TitlesColumnWidths;
        set
        {
            _preferences.TitlesColumnWidths = value;
            _ = SaveAsync();
        }
    }

    // Reference Data - CostCenters
    public string? LastSelectedCostCenterCode
    {
        get => _preferences.LastSelectedCostCenterCode;
        set
        {
            if (_preferences.LastSelectedCostCenterCode != value)
            {
                _preferences.LastSelectedCostCenterCode = value;
                _ = SaveAsync();
            }
        }
    }

    public string? LastCostCenterSearchText
    {
        get => _preferences.LastCostCenterSearchText;
        set
        {
            if (_preferences.LastCostCenterSearchText != value)
            {
                _preferences.LastCostCenterSearchText = value;
                _ = SaveAsync();
            }
        }
    }

    public List<string>? CostCentersColumnOrder
    {
        get => _preferences.CostCentersColumnOrder;
        set
        {
            _preferences.CostCentersColumnOrder = value;
            _ = SaveAsync();
        }
    }

    public Dictionary<string, double>? CostCentersColumnWidths
    {
        get => _preferences.CostCentersColumnWidths;
        set
        {
            _preferences.CostCentersColumnWidths = value;
            _ = SaveAsync();
        }
    }

    // Reference Data - Employers
    public string? LastSelectedEmployerCode
    {
        get => _preferences.LastSelectedEmployerCode;
        set
        {
            if (_preferences.LastSelectedEmployerCode != value)
            {
                _preferences.LastSelectedEmployerCode = value;
                _ = SaveAsync();
            }
        }
    }

    public string? LastEmployerSearchText
    {
        get => _preferences.LastEmployerSearchText;
        set
        {
            if (_preferences.LastEmployerSearchText != value)
            {
                _preferences.LastEmployerSearchText = value;
                _ = SaveAsync();
            }
        }
    }

    public List<string>? EmployersColumnOrder
    {
        get => _preferences.EmployersColumnOrder;
        set
        {
            _preferences.EmployersColumnOrder = value;
            _ = SaveAsync();
        }
    }

    public Dictionary<string, double>? EmployersColumnWidths
    {
        get => _preferences.EmployersColumnWidths;
        set
        {
            _preferences.EmployersColumnWidths = value;
            _ = SaveAsync();
        }
    }

    // Reference Data - Teams
    public string? LastSelectedTeamCode
    {
        get => _preferences.LastSelectedTeamCode;
        set
        {
            if (_preferences.LastSelectedTeamCode != value)
            {
                _preferences.LastSelectedTeamCode = value;
                _ = SaveAsync();
            }
        }
    }

    public string? LastTeamSearchText
    {
        get => _preferences.LastTeamSearchText;
        set
        {
            if (_preferences.LastTeamSearchText != value)
            {
                _preferences.LastTeamSearchText = value;
                _ = SaveAsync();
            }
        }
    }

    public List<string>? TeamsColumnOrder
    {
        get => _preferences.TeamsColumnOrder;
        set
        {
            _preferences.TeamsColumnOrder = value;
            _ = SaveAsync();
        }
    }

    public Dictionary<string, double>? TeamsColumnWidths
    {
        get => _preferences.TeamsColumnWidths;
        set
        {
            _preferences.TeamsColumnWidths = value;
            _ = SaveAsync();
        }
    }

    // Reference Data - Divisions
    public string? LastSelectedDivisionCode
    {
        get => _preferences.LastSelectedDivisionCode;
        set
        {
            if (_preferences.LastSelectedDivisionCode != value)
            {
                _preferences.LastSelectedDivisionCode = value;
                _ = SaveAsync();
            }
        }
    }

    public string? LastDivisionSearchText
    {
        get => _preferences.LastDivisionSearchText;
        set
        {
            if (_preferences.LastDivisionSearchText != value)
            {
                _preferences.LastDivisionSearchText = value;
                _ = SaveAsync();
            }
        }
    }

    public List<string>? DivisionsColumnOrder
    {
        get => _preferences.DivisionsColumnOrder;
        set
        {
            _preferences.DivisionsColumnOrder = value;
            _ = SaveAsync();
        }
    }

    public Dictionary<string, double>? DivisionsColumnWidths
    {
        get => _preferences.DivisionsColumnWidths;
        set
        {
            _preferences.DivisionsColumnWidths = value;
            _ = SaveAsync();
        }
    }

    // Reference Data - Organizations
    public string? LastSelectedOrganizationCode
    {
        get => _preferences.LastSelectedOrganizationCode;
        set
        {
            if (_preferences.LastSelectedOrganizationCode != value)
            {
                _preferences.LastSelectedOrganizationCode = value;
                _ = SaveAsync();
            }
        }
    }

    public string? LastOrganizationSearchText
    {
        get => _preferences.LastOrganizationSearchText;
        set
        {
            if (_preferences.LastOrganizationSearchText != value)
            {
                _preferences.LastOrganizationSearchText = value;
                _ = SaveAsync();
            }
        }
    }

    public List<string>? OrganizationsColumnOrder
    {
        get => _preferences.OrganizationsColumnOrder;
        set
        {
            _preferences.OrganizationsColumnOrder = value;
            _ = SaveAsync();
        }
    }

    public Dictionary<string, double>? OrganizationsColumnWidths
    {
        get => _preferences.OrganizationsColumnWidths;
        set
        {
            _preferences.OrganizationsColumnWidths = value;
            _ = SaveAsync();
        }
    }

    // Reference Data - SourceSystems
    public string? LastSelectedSourceSystemId
    {
        get => _preferences.LastSelectedSourceSystemId;
        set
        {
            if (_preferences.LastSelectedSourceSystemId != value)
            {
                _preferences.LastSelectedSourceSystemId = value;
                _ = SaveAsync();
            }
        }
    }

    public string? LastSourceSystemSearchText
    {
        get => _preferences.LastSourceSystemSearchText;
        set
        {
            if (_preferences.LastSourceSystemSearchText != value)
            {
                _preferences.LastSourceSystemSearchText = value;
                _ = SaveAsync();
            }
        }
    }

    public List<string>? SourceSystemsColumnOrder
    {
        get => _preferences.SourceSystemsColumnOrder;
        set
        {
            _preferences.SourceSystemsColumnOrder = value;
            _ = SaveAsync();
        }
    }

    public Dictionary<string, double>? SourceSystemsColumnWidths
    {
        get => _preferences.SourceSystemsColumnWidths;
        set
        {
            _preferences.SourceSystemsColumnWidths = value;
            _ = SaveAsync();
        }
    }

    // Reference Data - CustomFields (uses FieldKey as identifier)
    public string? LastSelectedCustomFieldKey
    {
        get => _preferences.LastSelectedCustomFieldKey;
        set
        {
            if (_preferences.LastSelectedCustomFieldKey != value)
            {
                _preferences.LastSelectedCustomFieldKey = value;
                _ = SaveAsync();
            }
        }
    }

    public string? LastCustomFieldSearchText
    {
        get => _preferences.LastCustomFieldSearchText;
        set
        {
            if (_preferences.LastCustomFieldSearchText != value)
            {
                _preferences.LastCustomFieldSearchText = value;
                _ = SaveAsync();
            }
        }
    }

    public List<string>? CustomFieldsColumnOrder
    {
        get => _preferences.CustomFieldsColumnOrder;
        set
        {
            _preferences.CustomFieldsColumnOrder = value;
            _ = SaveAsync();
        }
    }

    public Dictionary<string, double>? CustomFieldsColumnWidths
    {
        get => _preferences.CustomFieldsColumnWidths;
        set
        {
            _preferences.CustomFieldsColumnWidths = value;
            _ = SaveAsync();
        }
    }

    // Contacts - separate from persons
    public int? LastSelectedContactId
    {
        get => _preferences.LastSelectedContactId;
        set
        {
            if (_preferences.LastSelectedContactId != value)
            {
                _preferences.LastSelectedContactId = value;
                _ = SaveAsync();
            }
        }
    }

    public string? LastContactSearchText
    {
        get => _preferences.LastContactSearchText;
        set
        {
            if (_preferences.LastContactSearchText != value)
            {
                _preferences.LastContactSearchText = value;
                _ = SaveAsync();
            }
        }
    }

    public List<string>? ContactsColumnOrder
    {
        get => _preferences.ContactsColumnOrder;
        set
        {
            _preferences.ContactsColumnOrder = value;
            _ = SaveAsync();
        }
    }

    public Dictionary<string, double>? ContactsColumnWidths
    {
        get => _preferences.ContactsColumnWidths;
        set
        {
            _preferences.ContactsColumnWidths = value;
            _ = SaveAsync();
        }
    }

    public string? DatabasePath
    {
        get => _preferences.DatabasePath;
        set
        {
            if (_preferences.DatabasePath != value)
            {
                _preferences.DatabasePath = value;
                _ = SaveAsync();
            }
        }
    }

    /// <summary>
    /// Gets or sets the database type. Defaults to Sqlite.
    /// </summary>
    public DatabaseType DatabaseType
    {
        get => Enum.TryParse<DatabaseType>(_preferences.DatabaseType, out var dbType) ? dbType : DatabaseType.Sqlite;
        set
        {
            var valueString = value.ToString();
            if (_preferences.DatabaseType != valueString)
            {
                _preferences.DatabaseType = valueString;
                _ = SaveAsync();
            }
        }
    }

    /// <summary>
    /// Gets or sets the Supabase connection string (encrypted).
    /// </summary>
    public string? SupabaseConnectionString
    {
        get
        {
            var encrypted = _preferences.SupabaseConnectionStringEncrypted;
            return string.IsNullOrEmpty(encrypted) ? null : new WindowsDpapiEncryptionService().Decrypt(encrypted);
        }
        set
        {
            var encryptionService = new WindowsDpapiEncryptionService();
            var encrypted = string.IsNullOrEmpty(value) ? null : encryptionService.Encrypt(value);

            if (_preferences.SupabaseConnectionStringEncrypted != encrypted)
            {
                _preferences.SupabaseConnectionStringEncrypted = encrypted;
                _ = SaveAsync();
            }
        }
    }

    /// <summary>
    /// Gets or sets the Supabase project URL.
    /// </summary>
    public string? SupabaseUrl
    {
        get => _preferences.SupabaseUrl;
        set
        {
            if (_preferences.SupabaseUrl != value)
            {
                _preferences.SupabaseUrl = value;
                _ = SaveAsync();
            }
        }
    }

    public async Task LoadAsync()
    {
        System.Diagnostics.Debug.WriteLine($"[UserPreferencesService] LoadAsync START");
        System.Diagnostics.Debug.WriteLine($"[UserPreferencesService]   File exists: {File.Exists(_preferencesFilePath)}");
        try
        {
            if (File.Exists(_preferencesFilePath))
            {
                var json = await File.ReadAllTextAsync(_preferencesFilePath);
                _preferences = JsonSerializer.Deserialize<UserPreferences>(json) ?? new UserPreferences();
                System.Diagnostics.Debug.WriteLine($"[UserPreferencesService]   Loaded successfully");
                System.Diagnostics.Debug.WriteLine($"[UserPreferencesService]   ShowContractId: {_preferences.ContractsColumnVisibility?.ShowContractId}, ShowExternalId: {_preferences.ContractsColumnVisibility?.ShowExternalId}");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[UserPreferencesService]   File not found, using defaults");
            }
        }
        catch (Exception ex)
        {
            // If loading fails, use default preferences
            System.Diagnostics.Debug.WriteLine($"Failed to load user preferences: {ex.Message}");
            _preferences = new UserPreferences();
        }
        System.Diagnostics.Debug.WriteLine($"[UserPreferencesService] LoadAsync END");
    }

    public async Task SaveAsync()
    {
        try
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };
            var json = JsonSerializer.Serialize(_preferences, options);
            System.Diagnostics.Debug.WriteLine($"[UserPreferencesService] SaveAsync START");
            System.Diagnostics.Debug.WriteLine($"[UserPreferencesService]   Saving {json.Length} characters");
            System.Diagnostics.Debug.WriteLine($"[UserPreferencesService]   ContractsColumnVisibility.ShowContractId: {_preferences.ContractsColumnVisibility?.ShowContractId}");
            await File.WriteAllTextAsync(_preferencesFilePath, json);
            System.Diagnostics.Debug.WriteLine($"[UserPreferencesService] SaveAsync END - file written");
        }
        catch (Exception ex)
        {
            // Log error but don't throw - preferences are not critical
            System.Diagnostics.Debug.WriteLine($"Failed to save user preferences: {ex.Message}");
        }
    }
}

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
}

/// <summary>
/// DTO for contract filter persistence.
/// </summary>
public class ContractFilterDto
{
    public string FieldName { get; set; } = string.Empty;
    public string FieldDisplayName { get; set; } = string.Empty;
    public string Operator { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
