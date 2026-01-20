namespace HelloID.Vault.Services.Interfaces;

/// <summary>
/// Service for importing vault.json files into the database.
/// </summary>
public interface IVaultImportService
{
    /// <summary>
    /// Checks if the database contains any data.
    /// </summary>
    /// <returns>True if any table contains data, false otherwise.</returns>
    Task<bool> HasDataAsync();

    /// <summary>
    /// Deletes the database without creating a backup.
    /// </summary>
    Task DeleteDatabaseAsync();

    /// <summary>
    /// Imports a vault.json file into the database with progress tracking.
    /// </summary>
    /// <param name="filePath">Path to the vault.json file.</param>
    /// <param name="primaryManagerLogic">Logic for determining Primary Manager.</param>
    /// <param name="createBackup">Whether to backup the existing database before import.</param>
    /// <param name="progress">Progress reporter for tracking import status.</param>
    /// <returns>Import statistics.</returns>
    Task<ImportResult> ImportAsync(string filePath, PrimaryManagerLogic primaryManagerLogic, bool createBackup = false, IProgress<ImportProgress>? progress = null);

    /// <summary>
    /// Imports only company reference data from a vault.json file into the database with progress tracking.
    /// This includes: Departments, Locations, Cost_Centers, Cost_Bearers, Employers, Teams, Divisions, Titles, Organizations.
    /// </summary>
    /// <param name="filePath">Path to the vault.json file.</param>
    /// <param name="primaryManagerLogic">Logic for determining Primary Manager (ignored for company-only import).</param>
    /// <param name="createBackup">Whether to backup the existing database before import.</param>
    /// <param name="progress">Progress reporter for tracking import status.</param>
    /// <returns>Import statistics for company data only.</returns>
    Task<ImportResult> ImportCompanyOnlyAsync(string filePath, PrimaryManagerLogic primaryManagerLogic, bool createBackup = false, IProgress<ImportProgress>? progress = null);
}

/// <summary>
/// Progress information during import.
/// </summary>
public class ImportProgress
{
    public string CurrentOperation { get; set; } = string.Empty;
    public int TotalItems { get; set; }
    public int ProcessedItems { get; set; }
    public int Percentage => TotalItems > 0 ? (int)((double)ProcessedItems / TotalItems * 100) : 0;
}

/// <summary>
/// Result of the import operation.
/// </summary>
public class ImportResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public int PersonsImported { get; set; }
    public int ContractsImported { get; set; }
    public int ContactsImported { get; set; }
    public int DepartmentsImported { get; set; }
    public int DepartmentsAutoCreated { get; set; }
    public int OrganizationsImported { get; set; }
    public int LocationsImported { get; set; }
    public int SourceSystemsImported { get; set; }
    public int EmployersImported { get; set; }
    public int CostCentersImported { get; set; }
    public int CostBearersImported { get; set; }
    public int TeamsImported { get; set; }
    public int DivisionsImported { get; set; }
    public int TitlesImported { get; set; }
    public int CustomFieldPersonsImported { get; set; }
    public int CustomFieldContractsImported { get; set; }
    public TimeSpan Duration { get; set; }

    // Phase 2: Source-aware validation tracking
    public int OrphanedDepartmentsDetected { get; set; }
    public int OrphanedLocationsDetected { get; set; }
    public int OrphanedCostCentersDetected { get; set; }
    public int OrphanedCostBearersDetected { get; set; }
    public int OrphanedEmployersDetected { get; set; }
    public int OrphanedTeamsDetected { get; set; }
    public int OrphanedDivisionsDetected { get; set; }
    public int OrphanedTitlesDetected { get; set; }
    public int OrphanedOrganizationsDetected { get; set; }

    // Data quality tracking
    public int EmptyManagerGuidsReplaced { get; set; }
}
