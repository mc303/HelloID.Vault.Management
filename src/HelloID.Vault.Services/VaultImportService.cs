using System.Diagnostics;
using System.Text.Json;
using Dapper;
using HelloID.Vault.Core.Models.Entities;
using HelloID.Vault.Core.Models.Json;
using HelloID.Vault.Core.Utilities;
using HelloID.Vault.Data;
using HelloID.Vault.Data.Connection;
using HelloID.Vault.Data.Repositories.Interfaces;
using HelloID.Vault.Services.Interfaces;
using Microsoft.Data.Sqlite;

namespace HelloID.Vault.Services;


/// <summary>
/// Service implementation for importing vault.json files.
/// </summary>
public class VaultImportService : IVaultImportService
{
    private readonly ISqliteConnectionFactory _connectionFactory;
    private readonly DatabaseInitializer _databaseInitializer;
    private readonly IPersonRepository _personRepository;
    private readonly IContractRepository _contractRepository;
    private readonly IContactRepository _contactRepository;
    private readonly IDepartmentRepository _departmentRepository;
    private readonly ICustomFieldRepository _customFieldRepository;
    private readonly IPrimaryManagerService _primaryManagerService;
    private readonly IUserPreferencesService _userPreferencesService;
    private readonly IReferenceDataService _referenceDataService;
    private int _contractDebugCounter = 0;

    public VaultImportService(
        ISqliteConnectionFactory connectionFactory,
        DatabaseInitializer databaseInitializer,
        IPersonRepository personRepository,
        IContractRepository contractRepository,
        IContactRepository contactRepository,
        IDepartmentRepository departmentRepository,
        ICustomFieldRepository customFieldRepository,
        IPrimaryManagerService primaryManagerService,
        IUserPreferencesService userPreferencesService,
        IReferenceDataService referenceDataService)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _databaseInitializer = databaseInitializer ?? throw new ArgumentNullException(nameof(databaseInitializer));
        _personRepository = personRepository ?? throw new ArgumentNullException(nameof(personRepository));
        _contractRepository = contractRepository ?? throw new ArgumentNullException(nameof(contractRepository));
        _contactRepository = contactRepository ?? throw new ArgumentNullException(nameof(contactRepository));
        _departmentRepository = departmentRepository ?? throw new ArgumentNullException(nameof(departmentRepository));
        _customFieldRepository = customFieldRepository ?? throw new ArgumentNullException(nameof(customFieldRepository));
        _primaryManagerService = primaryManagerService ?? throw new ArgumentNullException(nameof(primaryManagerService));
        _userPreferencesService = userPreferencesService ?? throw new ArgumentNullException(nameof(userPreferencesService));
        _referenceDataService = referenceDataService ?? throw new ArgumentNullException(nameof(referenceDataService));
    }

    public async Task<bool> HasDataAsync()
    {
        try
        {
            using var connection = _connectionFactory.CreateConnection();

            // Check if any of the main tables have data
            var tables = new[] { "persons", "contracts", "departments", "contacts", "custom_field_schemas" };

            foreach (var table in tables)
            {
                var count = await connection.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM {table}");
                if (count > 0)
                {
                    return true;
                }
            }

            return false;
        }
        catch
        {
            // If database doesn't exist or tables don't exist, consider it empty
            return false;
        }
    }

    public async Task DeleteDatabaseAsync()
    {
        string? dbPath = null;

        try
        {
            // Get the database file path
            using (var connection = _connectionFactory.CreateConnection(enforceForeignKeys: false))
            {
                var sqliteConnection = connection as SqliteConnection;
                if (sqliteConnection == null)
                {
                    throw new Exception("Connection is not a SQLite connection");
                }

                dbPath = sqliteConnection.DataSource;

                if (string.IsNullOrEmpty(dbPath) || !File.Exists(dbPath))
                {
                    // Database doesn't exist, nothing to delete
                    return;
                }

                // Close connection explicitly
                connection.Close();
            } // Dispose connection here

            // Clear all connections from the pool to release file locks
            SqliteConnection.ClearAllPools();

            // Wait for file locks to be fully released
            await Task.Delay(500);

            // Delete database files
            File.Delete(dbPath);

            var walPath = dbPath + "-wal";
            var shmPath = dbPath + "-shm";

            if (File.Exists(walPath))
            {
                File.Delete(walPath);
            }

            if (File.Exists(shmPath))
            {
                File.Delete(shmPath);
            }

            // Recreate the database schema after deletion
            await _databaseInitializer.InitializeAsync();
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to delete database: {ex.Message}", ex);
        }
    }

    public async Task<ImportResult> ImportAsync(string filePath, PrimaryManagerLogic primaryManagerLogic, bool createBackup = false, IProgress<ImportProgress>? progress = null)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new ImportResult { Success = true };

        try
        {
            // Step 0: Backup database if requested
            if (createBackup)
            {
                progress?.Report(new ImportProgress { CurrentOperation = "Creating database backup..." });
                await CreateDatabaseBackupAsync();

                // Clear pools again to ensure fresh connections after schema recreation
                SqliteConnection.ClearAllPools();
                await Task.Delay(200);
            }
            else
            {
                // Check if database was just deleted (by DeleteDatabaseAsync call from ViewModel)
                // If so, ensure schema exists and clear pools
                bool databaseExists = false;
                try
                {
                    using var connection = _connectionFactory.CreateConnection();
                    var sqliteConnection = connection as SqliteConnection;
                    var dbPath = sqliteConnection?.DataSource;
                    databaseExists = !string.IsNullOrEmpty(dbPath) && File.Exists(dbPath);
                }
                catch { }

                if (!databaseExists)
                {
                    // Database was deleted, ensure schema is initialized
                    progress?.Report(new ImportProgress { CurrentOperation = "Initializing database schema..." });
                    await _databaseInitializer.InitializeAsync();

                    // Clear pools to ensure fresh connections
                    SqliteConnection.ClearAllPools();
                    await Task.Delay(200);
                }
            }

            // Verify that database tables exist before starting import
            progress?.Report(new ImportProgress { CurrentOperation = "Verifying database schema..." });
            bool tablesExist = false;
            try
            {
                using var testConnection = _connectionFactory.CreateConnection();
                var tableCount = await testConnection.ExecuteScalarAsync<int>(
                    "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='employers'");
                tablesExist = tableCount > 0;
            }
            catch { }

            if (!tablesExist)
            {
                progress?.Report(new ImportProgress { CurrentOperation = "Database schema not found, reinitializing..." });

                // Clear any stale connections first
                SqliteConnection.ClearAllPools();
                await Task.Delay(500);

                // Force schema recreation
                await _databaseInitializer.InitializeAsync();

                // Clear pools again
                SqliteConnection.ClearAllPools();
                await Task.Delay(500);
            }

            // Step 1: Load and parse JSON
            progress?.Report(new ImportProgress { CurrentOperation = "Loading vault.json file..." });

            var json = await File.ReadAllTextAsync(filePath);
            var vaultData = JsonSerializer.Deserialize<VaultRoot>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true
            });

            if (vaultData == null || vaultData.Persons == null)
            {
                result.Success = false;
                result.ErrorMessage = "Invalid vault.json file or no persons found.";
                return result;
            }

            var totalPersons = vaultData.Persons.Count;
            progress?.Report(new ImportProgress
            {
                CurrentOperation = $"Found {totalPersons} persons to import",
                TotalItems = totalPersons
            });

            // Step 2: Collect all source systems from the vault data
            progress?.Report(new ImportProgress { CurrentOperation = "Collecting source system data..." });

            var sourceSystems = new Dictionary<string, (string DisplayName, string IdentificationKey)>();

            // Collect sources from persons
            foreach (var vaultPerson in vaultData.Persons)
            {
                if (vaultPerson.Source?.SystemId != null && !string.IsNullOrWhiteSpace(vaultPerson.Source.SystemId))
                {
                    if (!sourceSystems.ContainsKey(vaultPerson.Source.SystemId))
                    {
                        sourceSystems[vaultPerson.Source.SystemId] = (
                            vaultPerson.Source.DisplayName ?? "Unknown",
                            vaultPerson.Source.IdentificationKey ?? vaultPerson.Source.SystemId
                        );
                    }
                }
            }

            // Collect sources from contracts
            foreach (var vaultPerson in vaultData.Persons)
            {
                foreach (var contract in vaultPerson.Contracts)
                {
                    if (contract.Source?.SystemId != null && !string.IsNullOrWhiteSpace(contract.Source.SystemId))
                    {
                        if (!sourceSystems.ContainsKey(contract.Source.SystemId))
                        {
                            sourceSystems[contract.Source.SystemId] = (
                                contract.Source.DisplayName ?? "Unknown",
                                contract.Source.IdentificationKey ?? contract.Source.SystemId
                            );
                        }
                    }
                }
            }

            // Collect sources from departments
            if (vaultData.Departments != null)
            {
                foreach (var dept in vaultData.Departments)
                {
                    if (dept.Source?.SystemId != null && !string.IsNullOrWhiteSpace(dept.Source.SystemId))
                    {
                        if (!sourceSystems.ContainsKey(dept.Source.SystemId))
                        {
                            sourceSystems[dept.Source.SystemId] = (
                                dept.Source.DisplayName ?? "Unknown",
                                dept.Source.IdentificationKey ?? dept.Source.SystemId
                            );
                        }
                    }
                }
            }

            // Step 2.5: Import source systems first
            progress?.Report(new ImportProgress { CurrentOperation = $"Importing {sourceSystems.Count} source systems..." });

            using (var connection = _connectionFactory.CreateConnection(enforceForeignKeys: false))
            {
                foreach (var (systemId, (displayName, identificationKey)) in sourceSystems)
                {
                    var rowsAffected = await connection.ExecuteAsync(
                        "INSERT OR IGNORE INTO source_system (system_id, display_name, identification_key) VALUES (@SystemId, @DisplayName, @IdentificationKey)",
                        new { SystemId = systemId, DisplayName = displayName, IdentificationKey = identificationKey });
                    result.SourceSystemsImported += rowsAffected;
                }
            }

            // Create source lookup dictionary for mapping
            var sourceLookup = sourceSystems.Keys.ToDictionary(id => id, id => id);

            // Step 3: Collect all lookup tables from contracts
            progress?.Report(new ImportProgress { CurrentOperation = "Collecting lookup table data..." });

            var organizations = new HashSet<VaultReference>(new ReferenceComparer());
            var locations = new HashSet<VaultReference>(new ReferenceComparer());
            var employers = new HashSet<VaultReference>(new ReferenceComparer());
            var costCenters = new HashSet<VaultReference>(new ReferenceComparer());
            var costBearers = new HashSet<VaultReference>(new ReferenceComparer());
            var teams = new HashSet<VaultReference>(new ReferenceComparer());
            var divisions = new HashSet<VaultReference>(new ReferenceComparer());
            var titles = new HashSet<VaultReference>(new ReferenceComparer());
            var departments = new HashSet<Department>(new DepartmentComparer());

            // Track source for each reference entity by external_id
            var organizationSources = new Dictionary<string, string?>();
            var locationSources = new Dictionary<string, string?>();
            var employerSources = new Dictionary<string, string?>();
            var employerNameToSourceMap = new Dictionary<string, string?>(); // ExternalId|Name -> Source
            var costCenterSources = new Dictionary<string, string?>();
            var costBearerSources = new Dictionary<string, string?>();
            var teamSources = new Dictionary<string, string?>();
            var divisionSources = new Dictionary<string, string?>();
            var titleSources = new Dictionary<string, string?>();

            // Track seen names to enforce uniqueness (for entities without external_id)
            var seenOrganizations = new Dictionary<string, VaultReference>();
            var seenLocations = new Dictionary<string, VaultReference>();
            var seenEmployers = new Dictionary<string, VaultReference>();
            var seenCostCenters = new Dictionary<string, VaultReference>();
            var seenCostBearers = new Dictionary<string, VaultReference>();
            var seenTeams = new Dictionary<string, VaultReference>();
            var seenDivisions = new Dictionary<string, VaultReference>();
            var seenTitles = new Dictionary<string, VaultReference>();

            // Extract departments from root-level Departments array (full data with hierarchy)
            if (vaultData.Departments != null && vaultData.Departments.Any())
            {
                progress?.Report(new ImportProgress { CurrentOperation = $"Extracting {vaultData.Departments.Count} departments from root array..." });
                foreach (var deptRef in vaultData.Departments)
                {
                    if (!string.IsNullOrWhiteSpace(deptRef.ExternalId))
                    {
                        departments.Add(MapDepartment(deptRef, sourceLookup));
                    }
                }
            }
            else
            {
                // Fallback: If no root-level Departments array, extract from contracts (references only)
                // This maintains backward compatibility with older vault.json formats
                Console.WriteLine("WARNING: No root-level Departments array found in vault.json");
                Console.WriteLine("         Extracting departments from contract references (Code, ParentExternalId, Manager will be NULL)");

                foreach (var vaultPerson in vaultData.Persons)
                {
                    foreach (var contract in vaultPerson.Contracts)
                    {
                        if (contract.Department?.ExternalId != null)
                        {
                            departments.Add(MapDepartment(contract.Department, sourceLookup));
                        }
                    }
                }
            }

            // Extract other lookup tables from contracts with source tracking
            Debug.WriteLine($"[Import] Starting employer collection from {vaultData.Persons.Count} persons");
            int totalContractsProcessed = 0;
            foreach (var vaultPerson in vaultData.Persons)
            {
                foreach (var contract in vaultPerson.Contracts)
                {
                    totalContractsProcessed++;
                    // Get contract source for inheritance
                    string? contractSource = null;
                    if (contract.Source?.SystemId != null && sourceLookup.TryGetValue(contract.Source.SystemId, out var sourceId))
                    {
                        contractSource = sourceId;
                    }

                    // Debug: Sample employer references (first 10 from each source)
                    if (totalContractsProcessed <= 10 || (totalContractsProcessed % 1000 == 0 && totalContractsProcessed <= 20))
                    {
                        if (contract.Employer != null)
                        {
                            Debug.WriteLine($"[Import] Contract {totalContractsProcessed}: Employer {contract.Employer.ExternalId} ({contract.Employer.Name}) source: {contractSource}");
                        }
                    }

                    if (contract.Organization != null && !string.IsNullOrWhiteSpace(contract.Organization.Name))
                    {
                        // Create unique key for deduplication: name|source
                        var nameKey = $"{contract.Organization.Name}|{contractSource ?? "default"}";

                        // Skip if we've already seen this name+source combination
                        if (!seenOrganizations.ContainsKey(nameKey))
                        {
                            string orgExternalId;
                            if (!string.IsNullOrWhiteSpace(contract.Organization.ExternalId))
                            {
                                // Has external_id: apply hash transformation for namespace isolation
                                orgExternalId = contract.Organization.ExternalId;
                            }
                            else
                            {
                                // Missing external_id: generate random GUID
                                orgExternalId = Guid.NewGuid().ToString();
                            }

                            var transformedOrganization = new VaultReference
                            {
                                ExternalId = orgExternalId,
                                Code = contract.Organization.Code,
                                Name = contract.Organization.Name
                            };
                            seenOrganizations[nameKey] = transformedOrganization;
                            organizations.Add(transformedOrganization);
                            if (!organizationSources.ContainsKey(orgExternalId))
                            {
                                organizationSources[orgExternalId] = contractSource;
                            }
                        }
                    }
                    if (contract.Location != null && !string.IsNullOrWhiteSpace(contract.Location.Name))
                    {
                        // Create unique key for deduplication: name|source
                        var nameKey = $"{contract.Location.Name}|{contractSource ?? "default"}";

                        // Skip if we've already seen this name+source combination
                        if (!seenLocations.ContainsKey(nameKey))
                        {
                            string locationExternalId;
                            if (!string.IsNullOrWhiteSpace(contract.Location.ExternalId))
                            {
                                // Has external_id: apply hash transformation for namespace isolation
                                locationExternalId = contract.Location.ExternalId;
                            }
                            else
                            {
                                // Missing external_id: generate random GUID
                                locationExternalId = Guid.NewGuid().ToString();
                            }

                            var transformedLocation = new VaultReference
                            {
                                ExternalId = locationExternalId,
                                Code = contract.Location.Code,
                                Name = contract.Location.Name
                            };
                            seenLocations[nameKey] = transformedLocation;
                            locations.Add(transformedLocation);
                            if (!locationSources.ContainsKey(locationExternalId))
                            {
                                locationSources[locationExternalId] = contractSource;
                            }
                        }
                    }
                    if (contract.Employer != null && !string.IsNullOrWhiteSpace(contract.Employer.Name))
                    {
                        // Create unique key for deduplication: name|source
                        var nameKey = $"{contract.Employer.Name}|{contractSource ?? "default"}";

                        // Skip if we've already seen this name+source combination
                        if (!seenEmployers.ContainsKey(nameKey))
                        {
                            string employerExternalId;
                            if (!string.IsNullOrWhiteSpace(contract.Employer.ExternalId))
                            {
                                // Has external_id: use it directly (no hash transformation)
                                employerExternalId = contract.Employer.ExternalId;
                            }
                            else
                            {
                                // Missing external_id: generate random GUID
                                employerExternalId = Guid.NewGuid().ToString();
                            }

                            // Create transformed employer entity
                            var transformedEmployer = new VaultReference
                            {
                                ExternalId = employerExternalId,
                                Code = contract.Employer.Code,
                                Name = contract.Employer.Name
                            };

                            seenEmployers[nameKey] = transformedEmployer;
                            employers.Add(transformedEmployer);

                            // Track source mapping for the transformed ExternalId
                            var employerKey = $"{employerExternalId}|{contractSource}";
                            if (!employerSources.ContainsKey(employerKey))
                            {
                                employerSources[employerKey] = contractSource;
                                if (!string.IsNullOrWhiteSpace(contract.Employer.ExternalId))
                                {
                                    Debug.WriteLine($"[Import] Collecting employer {contract.Employer.ExternalId}→{employerExternalId} ({contract.Employer.Code}) {contract.Employer.Name} from contract, inherited source: {contractSource}");
                                }

                                // Special debug for Baalderborg Groep
                                if (contract.Employer.Name?.Contains("Baalderborg") == true)
                                {
                                    Debug.WriteLine($"[Import] *** FOUND BAALDERBORG *** Contract {totalContractsProcessed}: {contract.Employer.ExternalId}→{employerExternalId} ({contract.Employer.Name}) source: {contractSource}");
                                }
                            }

                            // Also track employer name to source mapping for proper insertion later
                            var employerNameKey = $"{employerExternalId}|{contract.Employer.Name}";
                            if (!employerNameToSourceMap.ContainsKey(employerNameKey))
                            {
                                employerNameToSourceMap[employerNameKey] = contractSource;
                            }
                            else if (contract.Employer.Name?.Contains("Baalderborg") == true)
                            {
                                Debug.WriteLine($"[Import] *** BAALDERBORG ALREADY COLLECTED *** Contract {totalContractsProcessed}: {contract.Employer.ExternalId}→{employerExternalId} ({contract.Employer.Name}) source: {contractSource}");
                            }
                        }
                    }
                    if (contract.CostCenter != null && !string.IsNullOrWhiteSpace(contract.CostCenter.Name))
                    {
                        // Create unique key for deduplication: name|source
                        var nameKey = $"{contract.CostCenter.Name}|{contractSource ?? "default"}";

                        // Skip if we've already seen this name+source combination
                        if (!seenCostCenters.ContainsKey(nameKey))
                        {
                            string costCenterExternalId;
                            if (!string.IsNullOrWhiteSpace(contract.CostCenter.ExternalId))
                            {
                                // Has external_id: apply hash transformation for namespace isolation
                                costCenterExternalId = contract.CostCenter.ExternalId;
                            }
                            else
                            {
                                // Missing external_id: generate random GUID
                                costCenterExternalId = Guid.NewGuid().ToString();
                            }

                            var transformedCostCenter = new VaultReference
                            {
                                ExternalId = costCenterExternalId,
                                Code = contract.CostCenter.Code,
                                Name = contract.CostCenter.Name
                            };
                            seenCostCenters[nameKey] = transformedCostCenter;
                            costCenters.Add(transformedCostCenter);
                            if (!costCenterSources.ContainsKey(costCenterExternalId))
                            {
                                costCenterSources[costCenterExternalId] = contractSource;
                            }
                        }
                    }
                    if (contract.CostBearer != null && !string.IsNullOrWhiteSpace(contract.CostBearer.Name))
                    {
                        // Create unique key for deduplication: name|source
                        var nameKey = $"{contract.CostBearer.Name}|{contractSource ?? "default"}";

                        // Skip if we've already seen this name+source combination
                        if (!seenCostBearers.ContainsKey(nameKey))
                        {
                            string costBearerExternalId;
                            if (!string.IsNullOrWhiteSpace(contract.CostBearer.ExternalId))
                            {
                                // Has external_id: apply hash transformation for namespace isolation
                                costBearerExternalId = contract.CostBearer.ExternalId;
                            }
                            else
                            {
                                // Missing external_id: generate random GUID
                                costBearerExternalId = Guid.NewGuid().ToString();
                            }

                            var transformedCostBearer = new VaultReference
                            {
                                ExternalId = costBearerExternalId,
                                Code = contract.CostBearer.Code,
                                Name = contract.CostBearer.Name
                            };
                            seenCostBearers[nameKey] = transformedCostBearer;
                            costBearers.Add(transformedCostBearer);
                            if (!costBearerSources.ContainsKey(costBearerExternalId))
                            {
                                costBearerSources[costBearerExternalId] = contractSource;
                            }
                        }
                    }
                    if (contract.Team != null && !string.IsNullOrWhiteSpace(contract.Team.Name))
                    {
                        // Create unique key for deduplication: name|source
                        var nameKey = $"{contract.Team.Name}|{contractSource ?? "default"}";

                        // Skip if we've already seen this name+source combination
                        if (!seenTeams.ContainsKey(nameKey))
                        {
                            string teamExternalId;
                            if (!string.IsNullOrWhiteSpace(contract.Team.ExternalId))
                            {
                                // Has external_id: apply hash transformation for namespace isolation
                                teamExternalId = contract.Team.ExternalId;
                            }
                            else
                            {
                                // Missing external_id: generate random GUID
                                teamExternalId = Guid.NewGuid().ToString();
                            }

                            var transformedTeam = new VaultReference
                            {
                                ExternalId = teamExternalId,
                                Code = contract.Team.Code,
                                Name = contract.Team.Name
                            };
                            seenTeams[nameKey] = transformedTeam;
                            teams.Add(transformedTeam);
                            if (!teamSources.ContainsKey(teamExternalId))
                            {
                                teamSources[teamExternalId] = contractSource;
                            }
                        }
                    }
                    if (contract.Division != null && !string.IsNullOrWhiteSpace(contract.Division.Name))
                    {
                        // Create unique key for deduplication: name|source
                        var nameKey = $"{contract.Division.Name}|{contractSource ?? "default"}";

                        // Skip if we've already seen this name+source combination
                        if (!seenDivisions.ContainsKey(nameKey))
                        {
                            string divisionExternalId;
                            if (!string.IsNullOrWhiteSpace(contract.Division.ExternalId))
                            {
                                // Has external_id: use original external_id (no hash transformation)
                                divisionExternalId = contract.Division.ExternalId;
                            }
                            else
                            {
                                // Missing external_id: generate random GUID
                                divisionExternalId = Guid.NewGuid().ToString();
                            }

                            var transformedDivision = new VaultReference
                            {
                                ExternalId = divisionExternalId,
                                Code = contract.Division.Code,
                                Name = contract.Division.Name
                            };
                            seenDivisions[nameKey] = transformedDivision;
                            divisions.Add(transformedDivision);
                            if (!divisionSources.ContainsKey(divisionExternalId))
                            {
                                divisionSources[divisionExternalId] = contractSource;
                            }
                        }
                    }
                    if (contract.Title != null && !string.IsNullOrWhiteSpace(contract.Title.Name))
                    {
                        // Create unique key for deduplication: name|source
                        var nameKey = $"{contract.Title.Name}|{contractSource ?? "default"}";

                        // Skip if we've already seen this name+source combination
                        if (!seenTitles.ContainsKey(nameKey))
                        {
                            string titleExternalId;
                            if (!string.IsNullOrWhiteSpace(contract.Title.ExternalId))
                            {
                                // Has external_id: apply hash transformation for namespace isolation
                                titleExternalId = contract.Title.ExternalId;
                            }
                            else
                            {
                                // Missing external_id: generate random GUID
                                titleExternalId = Guid.NewGuid().ToString();
                            }

                            var transformedTitle = new VaultReference
                            {
                                ExternalId = titleExternalId,
                                Code = contract.Title.Code,
                                Name = contract.Title.Name
                            };
                            seenTitles[nameKey] = transformedTitle;
                            titles.Add(transformedTitle);
                            if (!titleSources.ContainsKey(titleExternalId))
                            {
                                titleSources[titleExternalId] = contractSource;
                            }
                        }
                    }
                }
            }
            Debug.WriteLine($"[Import] Processed {totalContractsProcessed} total contracts, collected {employers.Count} unique employers");

            // Step 3: Import lookup tables (no dependencies)
            progress?.Report(new ImportProgress { CurrentOperation = "Importing lookup tables..." });

            using (var connection = _connectionFactory.CreateConnection(enforceForeignKeys: false))
            {
                // Insert organizations
                foreach (var org in organizations)
                {
                    var source = organizationSources.TryGetValue(org.ExternalId ?? string.Empty, out var orgSource) ? orgSource : null;
                    var rowsAffected = await connection.ExecuteAsync(
                        "INSERT OR IGNORE INTO organizations (external_id, code, name, source) VALUES (@ExternalId, @Code, @Name, @Source)",
                        new { org.ExternalId, org.Code, org.Name, Source = source });
                    result.OrganizationsImported += rowsAffected;
                }

                // Insert locations
                foreach (var loc in locations)
                {
                    var source = locationSources.TryGetValue(loc.ExternalId ?? string.Empty, out var locSource) ? locSource : null;
                    var rowsAffected = await connection.ExecuteAsync(
                        "INSERT OR IGNORE INTO locations (external_id, code, name, source) VALUES (@ExternalId, @Code, @Name, @Source)",
                        new { loc.ExternalId, loc.Code, loc.Name, Source = source });
                    result.LocationsImported += rowsAffected;
                }

                // Insert employers
                Debug.WriteLine($"[Import] Normal Import - About to insert {employers.Count} employers:");
                Debug.WriteLine($"[Import] employerSources contains {employerSources.Count} entries:");
                foreach (var kvp in employerSources)
                {
                    Debug.WriteLine($"[Import]   Key: '{kvp.Key}' -> Source: {kvp.Value}");
                }

                foreach (var emp in employers)
                {
                    // Find the correct source for this employer using the name-to-source mapping
                    var employerNameKey = $"{emp.ExternalId ?? string.Empty}|{emp.Name ?? string.Empty}";
                    employerNameToSourceMap.TryGetValue(employerNameKey, out var source);

                    Debug.WriteLine($"[Import] Normal Import - Inserting employer: {emp.ExternalId} ({emp.Code}) {emp.Name} - Source: {source}");
                    var rowsAffected = await connection.ExecuteAsync(
                        "INSERT OR IGNORE INTO employers (external_id, code, name, source) VALUES (@ExternalId, @Code, @Name, @Source)",
                        new { emp.ExternalId, emp.Code, emp.Name, Source = source });
                    result.EmployersImported += rowsAffected;
                    Debug.WriteLine($"[Import] Normal Import - Employer insert result: {rowsAffected} rows affected");
                }
                Debug.WriteLine($"[Import] Normal Import - Total employers imported: {result.EmployersImported}");

                // Insert cost centers
                foreach (var cc in costCenters)
                {
                    var source = costCenterSources.TryGetValue(cc.ExternalId ?? string.Empty, out var ccSource) ? ccSource : null;
                    var rowsAffected = await connection.ExecuteAsync(
                        "INSERT OR IGNORE INTO cost_centers (external_id, code, name, source) VALUES (@ExternalId, @Code, @Name, @Source)",
                        new { cc.ExternalId, cc.Code, cc.Name, Source = source });
                    result.CostCentersImported += rowsAffected;
                }

                // Insert cost bearers
                foreach (var cb in costBearers)
                {
                    var source = costBearerSources.TryGetValue(cb.ExternalId ?? string.Empty, out var cbSource) ? cbSource : null;
                    var rowsAffected = await connection.ExecuteAsync(
                        "INSERT OR IGNORE INTO cost_bearers (external_id, code, name, source) VALUES (@ExternalId, @Code, @Name, @Source)",
                        new { cb.ExternalId, cb.Code, cb.Name, Source = source });
                    result.CostBearersImported += rowsAffected;
                }

                // Insert teams
                foreach (var team in teams)
                {
                    var source = teamSources.TryGetValue(team.ExternalId ?? string.Empty, out var teamSource) ? teamSource : null;
                    var rowsAffected = await connection.ExecuteAsync(
                        "INSERT OR IGNORE INTO teams (external_id, code, name, source) VALUES (@ExternalId, @Code, @Name, @Source)",
                        new { team.ExternalId, team.Code, team.Name, Source = source });
                    result.TeamsImported += rowsAffected;
                }

                // Insert divisions
                foreach (var div in divisions)
                {
                    var source = divisionSources.TryGetValue(div.ExternalId ?? string.Empty, out var divSource) ? divSource : null;
                    var rowsAffected = await connection.ExecuteAsync(
                        "INSERT OR IGNORE INTO divisions (external_id, code, name, source) VALUES (@ExternalId, @Code, @Name, @Source)",
                        new { div.ExternalId, div.Code, div.Name, Source = source });
                    result.DivisionsImported += rowsAffected;
                }

                // Insert titles
                foreach (var title in titles)
                {
                    var source = titleSources.TryGetValue(title.ExternalId ?? string.Empty, out var titleSource) ? titleSource : null;
                    var rowsAffected = await connection.ExecuteAsync(
                        "INSERT OR IGNORE INTO titles (external_id, code, name, source) VALUES (@ExternalId, @Code, @Name, @Source)",
                        new { title.ExternalId, title.Code, title.Name, Source = source });
                    result.TitlesImported += rowsAffected;
                }
            }

            // Step 4: Import persons (no FK dependencies)
            progress?.Report(new ImportProgress { CurrentOperation = "Importing persons..." });

            // Import persons in transaction for atomicity
            using (var connection = _connectionFactory.CreateConnection(enforceForeignKeys: false))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        int processedCount = 0;
                        const int progressBatchSize = 100; // Report progress every 100 persons

                        foreach (var vaultPerson in vaultData.Persons)
                        {
                            processedCount++;

                            // Map and insert person
                            var person = MapPerson(vaultPerson, sourceLookup, primaryManagerLogic);
                            await _personRepository.InsertAsync(person, connection, transaction);
                            result.PersonsImported++;

                            // Report progress in batches to avoid UI flooding
                            if (processedCount % progressBatchSize == 0 || processedCount == totalPersons)
                            {
                                progress?.Report(new ImportProgress
                                {
                                    CurrentOperation = $"Importing persons... ({processedCount}/{totalPersons})",
                                    TotalItems = totalPersons,
                                    ProcessedItems = processedCount
                                });
                                // Yield to UI thread
                                await Task.Yield();
                            }
                        }

                        transaction.Commit();
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }

            // Step 5: Import departments (depends on persons for manager_person_id)
            progress?.Report(new ImportProgress { CurrentOperation = "Importing departments..." });

            // Perform topological sort to ensure parents are inserted before children
            List<Department> sortedDepartments;
            try
            {
                sortedDepartments = TopologicalSortDepartments(departments.ToList());
            }
            catch (InvalidOperationException ex)
            {
                throw new Exception($"Department hierarchy error: {ex.Message}", ex);
            }

            // Import departments in transaction for atomicity
            using (var connection = _connectionFactory.CreateConnection(enforceForeignKeys: false))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        foreach (var dept in sortedDepartments)
                        {
                            await _departmentRepository.InsertAsync(dept, connection, transaction);
                            result.DepartmentsImported++;
                        }

                        // Validate and auto-fix orphaned departments and invalid manager references
                        progress?.Report(new ImportProgress { CurrentOperation = "Validating department data..." });

                        // Check for orphaned departments (parent doesn't exist)
                        var orphanedDepts = await connection.QueryAsync<(string ExternalId, string DisplayName, string ParentExternalId)>(@"
                            SELECT d.external_id as ExternalId, d.display_name as DisplayName, d.parent_external_id as ParentExternalId
                            FROM departments d
                            LEFT JOIN departments p ON d.parent_external_id = p.external_id
                            WHERE d.parent_external_id IS NOT NULL
                              AND d.parent_external_id != ''
                              AND p.external_id IS NULL", transaction: transaction);

                        if (orphanedDepts.Any())
                        {
                            Console.WriteLine($"WARNING: Found {orphanedDepts.Count()} orphaned departments. Auto-fixing by setting parent to NULL:");
                            foreach (var orphan in orphanedDepts)
                            {
                                Console.WriteLine($"  - {orphan.DisplayName} ({orphan.ExternalId}) has non-existent parent: {orphan.ParentExternalId}");

                                // Auto-fix: Set parent to NULL
                                await connection.ExecuteAsync(@"
                                    UPDATE departments
                                    SET parent_external_id = NULL
                                    WHERE external_id = @ExternalId",
                                    new { ExternalId = orphan.ExternalId },
                                    transaction);
                            }
                        }

                        // Check for invalid manager references
                        var invalidManagers = await connection.QueryAsync<(string ExternalId, string DisplayName, string ManagerPersonId)>(@"
                            SELECT d.external_id as ExternalId, d.display_name as DisplayName, d.manager_person_id as ManagerPersonId
                            FROM departments d
                            LEFT JOIN persons p ON d.manager_person_id = p.person_id
                            WHERE d.manager_person_id IS NOT NULL
                              AND d.manager_person_id != ''
                              AND p.person_id IS NULL", transaction: transaction);

                        if (invalidManagers.Any())
                        {
                            Console.WriteLine($"WARNING: Found {invalidManagers.Count()} departments with invalid manager references. Auto-fixing by setting manager to NULL:");
                            foreach (var invalid in invalidManagers)
                            {
                                Console.WriteLine($"  - {invalid.DisplayName} ({invalid.ExternalId}) has non-existent manager: {invalid.ManagerPersonId}");

                                // Auto-fix: Set manager to NULL
                                await connection.ExecuteAsync(@"
                                    UPDATE departments
                                    SET manager_person_id = NULL
                                    WHERE external_id = @ExternalId",
                                    new { ExternalId = invalid.ExternalId },
                                    transaction);
                            }
                        }

                        transaction.Commit();
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        throw new Exception($"Department import failed and was rolled back. Error: {ex.Message}", ex);
                    }
                }
            }

            // Step 5.5: Auto-create departments referenced by contracts but missing from root Departments array
            progress?.Report(new ImportProgress { CurrentOperation = "Checking for orphaned department references..." });

            // Collect all department references from contracts with proper source inheritance
            var contractDepartmentRefs = new Dictionary<string, (string DisplayName, string Source)>();
            foreach (var vaultPerson in vaultData.Persons)
            {
                foreach (var contract in vaultPerson.Contracts)
                {
                    if (!string.IsNullOrWhiteSpace(contract.Department?.ExternalId))
                    {
                        // Get contract source
                        string? contractSource = null;
                        if (contract.Source?.SystemId != null && sourceLookup.TryGetValue(contract.Source.SystemId, out var mappedSourceId))
                        {
                            contractSource = mappedSourceId;
                        }

                        // Use original external_id (no hash transformation)
                        string transformedDeptId = contract.Department.ExternalId;
                        contractDepartmentRefs[transformedDeptId] = (contract.Department.DisplayName ?? string.Empty, contractSource ?? string.Empty);
                    }
                }
            }

            // Check which departments exist in database
            using (var connection = _connectionFactory.CreateConnection(enforceForeignKeys: false))
            {
                var existingDeptIds = (await connection.QueryAsync<string>("SELECT external_id FROM departments")).ToHashSet();

                // Find missing departments (using hash-transformed IDs)
                var missingDepts = contractDepartmentRefs
                    .Where(kvp => !existingDeptIds.Contains(kvp.Key))
                    .Select(kvp => (ExternalId: kvp.Key, DisplayName: kvp.Value.DisplayName, Source: kvp.Value.Source))
                    .ToList();

                if (missingDepts.Any())
                {
                    Console.WriteLine($"WARNING: Found {missingDepts.Count} department(s) referenced by contracts but missing from root Departments array.");
                    Console.WriteLine("         Auto-creating departments with minimal data (Code, ParentExternalId, Manager will be NULL):");

                    connection.Open();
                    using (var transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            foreach (var (externalId, displayName, source) in missingDepts)
                            {
                                Console.WriteLine($"  - {displayName} ({externalId})");

                                var missingDept = new Department
                                {
                                    ExternalId = externalId, // Already hash-transformed
                                    DisplayName = displayName,
                                    Code = null,
                                    ParentExternalId = null,
                                    ManagerPersonId = null,
                                    Source = source // Proper source inheritance
                                };

                                await _departmentRepository.InsertAsync(missingDept, connection, transaction);
                                result.DepartmentsAutoCreated++;
                            }

                            transaction.Commit();
                        }
                    catch (Exception)
                    {
                        transaction.Rollback();
                        throw;
                    }
                    }
                }
            }

            // Step 6: Import contacts (depends on persons)
            progress?.Report(new ImportProgress { CurrentOperation = "Importing contacts..." });

            using (var connection = _connectionFactory.CreateConnection(enforceForeignKeys: false))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        int processedCount = 0;
                        const int progressBatchSize = 100;

                        foreach (var vaultPerson in vaultData.Persons)
                        {
                            processedCount++;

                            if (vaultPerson.Contact != null)
                            {
                                // Only insert Personal contact if it has at least one field with data
                                if (vaultPerson.Contact.Personal != null && !IsEmptyContact(vaultPerson.Contact.Personal))
                                {
                                    var personalContact = MapContact(vaultPerson.PersonId, "Personal", vaultPerson.Contact.Personal);
                                    await _contactRepository.InsertAsync(personalContact, connection, transaction);
                                    result.ContactsImported++;
                                }

                                // Only insert Business contact if it has at least one field with data
                                if (vaultPerson.Contact.Business != null && !IsEmptyContact(vaultPerson.Contact.Business))
                                {
                                    var businessContact = MapContact(vaultPerson.PersonId, "Business", vaultPerson.Contact.Business);
                                    await _contactRepository.InsertAsync(businessContact, connection, transaction);
                                    result.ContactsImported++;
                                }
                            }

                            // Report progress in batches
                            if (processedCount % progressBatchSize == 0 || processedCount == totalPersons)
                            {
                                progress?.Report(new ImportProgress
                                {
                                    CurrentOperation = $"Importing contacts... ({processedCount}/{totalPersons})",
                                    TotalItems = totalPersons,
                                    ProcessedItems = processedCount
                                });
                                await Task.Yield();
                            }
                        }

                        transaction.Commit();
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }

            // Step 7: Import contracts (depends on persons, departments, and lookup tables)
            progress?.Report(new ImportProgress { CurrentOperation = "Importing contracts..." });

            // Check for duplicate contract external_ids before importing
            var contractExternalIds = new HashSet<string>();
            var duplicateContracts = new List<string>();
            foreach (var vaultPerson in vaultData.Persons)
            {
                foreach (var vaultContract in vaultPerson.Contracts)
                {
                    if (!string.IsNullOrWhiteSpace(vaultContract.ExternalId) && !contractExternalIds.Add(vaultContract.ExternalId))
                    {
                        duplicateContracts.Add(vaultContract.ExternalId);
                    }
                }
            }

            if (duplicateContracts.Count > 0)
            {
                Debug.WriteLine($"[Import] Found {duplicateContracts.Count} duplicate contract external_ids:");
                foreach (var dup in duplicateContracts.Distinct().Take(10))
                {
                    Debug.WriteLine($"  - {dup}");
                }
                Debug.WriteLine($"[Import] WARNING: {duplicateContracts.Count} duplicate contracts will be skipped");
            }

            using (var connection = _connectionFactory.CreateConnection(enforceForeignKeys: false))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        int processedCount = 0;
                        const int progressBatchSize = 100;

                        foreach (var vaultPerson in vaultData.Persons)
                        {
                            processedCount++;

                            foreach (var vaultContract in vaultPerson.Contracts)
                            {
                                // Skip duplicates
                                if (string.IsNullOrWhiteSpace(vaultContract.ExternalId) || !contractExternalIds.Contains(vaultContract.ExternalId))
                                {
                                    continue;
                                }
                                contractExternalIds.Remove(vaultContract.ExternalId); // Mark as processed

                                var contract = MapContract(vaultPerson.PersonId, vaultContract, sourceLookup, result,
                                    seenLocations, seenEmployers, seenCostCenters,
                                    seenCostBearers, seenTeams, seenDivisions, seenTitles, seenOrganizations);

                                // Debug: Log first few insert attempts
                                if (result.ContractsImported < 10)
                                {
                                    Debug.WriteLine($"[Insert Contract #{result.ContractsImported + 1}] ExternalId={contract.ExternalId}, PersonId={contract.PersonId}");
                                }

                                await _contractRepository.InsertAsync(contract, connection, transaction);
                                result.ContractsImported++;
                            }

                            // Report progress in batches
                            if (processedCount % progressBatchSize == 0 || processedCount == totalPersons)
                            {
                                progress?.Report(new ImportProgress
                                {
                                    CurrentOperation = $"Importing contracts... ({processedCount}/{totalPersons})",
                                    TotalItems = totalPersons,
                                    ProcessedItems = processedCount
                                });
                                await Task.Yield();
                            }
                        }

                        transaction.Commit();
                    }
                    catch (Exception)
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }

            // Step 7.5: Validate contract references (Phase 2: Source-aware validation)
            progress?.Report(new ImportProgress { CurrentOperation = "Validating contract references..." });
            await ValidateContractReferencesAsync(result);

            // Step 7.6: Calculate Primary Managers for ContractBased/DepartmentBased logic
            if (primaryManagerLogic == PrimaryManagerLogic.ContractBased || primaryManagerLogic == PrimaryManagerLogic.DepartmentBased)
            {
                progress?.Report(new ImportProgress { CurrentOperation = $"Calculating Primary Managers ({primaryManagerLogic})..." });
                int updatedCount = await _primaryManagerService.RefreshAllPrimaryManagersAsync(primaryManagerLogic);
                Console.WriteLine($"Updated primary managers for {updatedCount} persons using {primaryManagerLogic} logic.");
            }
            // Step 7.7: Auto-detect Primary Manager logic from imported data (From JSON)
            else if (primaryManagerLogic == PrimaryManagerLogic.FromJson)
            {
                progress?.Report(new ImportProgress { CurrentOperation = "Detecting Primary Manager logic from imported data..." });
                var detectedLogic = await DetectPrimaryManagerLogicAsync();
                if (detectedLogic != null)
                {
                    Console.WriteLine($"Auto-detected Primary Manager logic: {detectedLogic}");
                }
            }

            // Step 8: Collect and create custom field schemas
            progress?.Report(new ImportProgress { CurrentOperation = "Creating custom field schemas..." });

            var customFieldSchemas = new Dictionary<(string TableName, string FieldKey), (string DataType, HashSet<object> SampleValues)>();

            // Collect all custom fields and sample values for data type detection
            foreach (var vaultPerson in vaultData.Persons)
            {
                // Person custom fields
                if (vaultPerson.Custom != null)
                {
                    foreach (var (key, value) in vaultPerson.Custom)
                    {
                        var schemaKey = ("persons", key);
                        if (!customFieldSchemas.ContainsKey(schemaKey))
                        {
                            customFieldSchemas[schemaKey] = ("text", new HashSet<object>());
                        }
                        // Only add non-null values for data type detection
                        if (value != null)
                        {
                            customFieldSchemas[schemaKey].SampleValues.Add(value);
                        }
                    }
                }

                // Contract custom fields
                foreach (var vaultContract in vaultPerson.Contracts)
                {
                    if (vaultContract.Custom != null)
                    {
                        foreach (var (key, value) in vaultContract.Custom)
                        {
                            var schemaKey = ("contracts", key);
                            if (!customFieldSchemas.ContainsKey(schemaKey))
                            {
                                customFieldSchemas[schemaKey] = ("text", new HashSet<object>());
                            }
                            // Only add non-null values for data type detection
                            if (value != null)
                            {
                                customFieldSchemas[schemaKey].SampleValues.Add(value);
                            }
                        }
                    }
                }
            }

            // Insert custom field schemas
            using (var connection = _connectionFactory.CreateConnection(enforceForeignKeys: false))
            {
                int sortOrder = 1;
                foreach (var ((tableName, fieldKey), (_, sampleValues)) in customFieldSchemas)
                {
                    var dataType = DetectDataType(sampleValues);
                    var displayName = FormatDisplayName(fieldKey);

                    var rowsAffected = await connection.ExecuteAsync(@"
                        INSERT OR IGNORE INTO custom_field_schemas (table_name, field_key, display_name, sort_order)
                        VALUES (@TableName, @FieldKey, @DisplayName, @SortOrder)",
                        new
                        {
                            TableName = tableName,
                            FieldKey = fieldKey,
                            DisplayName = displayName,
                            SortOrder = sortOrder++
                        });

                    // Track separately by table name
                    if (tableName == "persons")
                    {
                        result.CustomFieldPersonsImported += rowsAffected;
                    }
                    else if (tableName == "contracts")
                    {
                        result.CustomFieldContractsImported += rowsAffected;
                    }
                }
            }

            // Step 9: Import custom field values (depends on persons.external_id and contracts.external_id)
            progress?.Report(new ImportProgress { CurrentOperation = "Importing custom field values..." });

            foreach (var vaultPerson in vaultData.Persons)
            {
                // Person custom fields - use external_id as entity_id (per schema triggers)
                if (vaultPerson.Custom != null && !string.IsNullOrWhiteSpace(vaultPerson.ExternalId))
                {
                    foreach (var (key, value) in vaultPerson.Custom)
                    {
                        var textValue = value?.ToString() ?? "null";
                        var customFieldValue = new CustomFieldValue
                        {
                            EntityId = vaultPerson.ExternalId, // Use external_id, not person_id!
                            TableName = "persons",
                            FieldKey = key,
                            TextValue = textValue
                        };
                        await _customFieldRepository.UpsertValueAsync(customFieldValue);
                    }
                }

                // Contract custom fields - use external_id as entity_id
                foreach (var vaultContract in vaultPerson.Contracts)
                {
                    if (vaultContract.Custom != null && !string.IsNullOrWhiteSpace(vaultContract.ExternalId))
                    {
                        foreach (var (key, value) in vaultContract.Custom)
                        {
                            var textValue = value?.ToString() ?? "null";
                            var customFieldValue = new CustomFieldValue
                            {
                                EntityId = vaultContract.ExternalId, // Use external_id!
                                TableName = "contracts",
                                FieldKey = key,
                                TextValue = textValue
                            };
                            await _customFieldRepository.UpsertValueAsync(customFieldValue);
                        }
                    }
                }
            }

            progress?.Report(new ImportProgress
            {
                CurrentOperation = "Import completed successfully!",
                TotalItems = totalPersons,
                ProcessedItems = totalPersons
            });
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = $"Import failed: {ex.Message}";
        }
        finally
        {
            // Re-enable foreign key constraints
            try
            {
                using var connection = _connectionFactory.CreateConnection();
                await connection.ExecuteAsync("PRAGMA foreign_keys = ON;");
            }
            catch
            {
                // Ignore errors when re-enabling foreign keys
            }

            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;
        }

        // Clear reference data cache so UI shows fresh data
        _referenceDataService.ClearCache();

        // Log data quality summary
        System.Diagnostics.Debug.WriteLine($"[Import] Data Quality Summary:");
        System.Diagnostics.Debug.WriteLine($"  Empty manager GUIDs replaced: {result.EmptyManagerGuidsReplaced}");
        if (result.EmptyManagerGuidsReplaced > 0)
        {
            System.Diagnostics.Debug.WriteLine($"  → These contracts will have NULL manager instead of invalid GUID");
        }

        return result;
    }

    public async Task<ImportResult> ImportCompanyOnlyAsync(string filePath, PrimaryManagerLogic primaryManagerLogic, bool createBackup = false, IProgress<ImportProgress>? progress = null)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new ImportResult { Success = true };

        try
        {
            // Step 0: Backup database if requested
            if (createBackup)
            {
                progress?.Report(new ImportProgress { CurrentOperation = "Creating database backup..." });
                await CreateDatabaseBackupAsync();

                // Clear pools again to ensure fresh connections after schema recreation
                SqliteConnection.ClearAllPools();
                await Task.Delay(200);
            }
            else
            {
                // Check if database was just deleted (by DeleteDatabaseAsync call from ViewModel)
                // If so, ensure schema exists and clear pools
                bool databaseExists = false;
                try
                {
                    using var connection = _connectionFactory.CreateConnection();
                    var sqliteConnection = connection as SqliteConnection;
                    var dbPath = sqliteConnection?.DataSource;
                    databaseExists = !string.IsNullOrEmpty(dbPath) && File.Exists(dbPath);
                }
                catch { }

                if (!databaseExists)
                {
                    // Database was deleted, ensure schema is initialized
                    progress?.Report(new ImportProgress { CurrentOperation = "Initializing database schema..." });
                    await _databaseInitializer.InitializeAsync();

                    // Clear pools to ensure fresh connections
                    SqliteConnection.ClearAllPools();
                    await Task.Delay(200);
                }
            }

            // Verify that database tables exist before starting import
            progress?.Report(new ImportProgress { CurrentOperation = "Verifying database schema..." });
            bool tablesExist = false;
            try
            {
                using var testConnection = _connectionFactory.CreateConnection();
                var tableCount = await testConnection.ExecuteScalarAsync<int>(
                    "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='employers'");
                tablesExist = tableCount > 0;
            }
            catch { }

            if (!tablesExist)
            {
                progress?.Report(new ImportProgress { CurrentOperation = "Database schema not found, reinitializing..." });

                // Clear any stale connections first
                SqliteConnection.ClearAllPools();
                await Task.Delay(500);

                // Force schema recreation
                await _databaseInitializer.InitializeAsync();

                // Clear pools again
                SqliteConnection.ClearAllPools();
                await Task.Delay(500);
            }

            // Step 1: Load and parse JSON
            progress?.Report(new ImportProgress { CurrentOperation = "Loading vault.json file..." });

            var json = await File.ReadAllTextAsync(filePath);
            var vaultData = JsonSerializer.Deserialize<VaultRoot>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true
            });

            if (vaultData == null)
            {
                result.Success = false;
                result.ErrorMessage = "Invalid vault.json file.";
                return result;
            }

            // Step 2: Collect all source systems from the vault data
            progress?.Report(new ImportProgress { CurrentOperation = "Collecting source system data..." });

            var sourceSystems = new Dictionary<string, (string DisplayName, string IdentificationKey)>();

            // Collect sources from departments
            if (vaultData.Departments != null)
            {
                foreach (var dept in vaultData.Departments)
                {
                    if (dept.Source?.SystemId != null && !string.IsNullOrWhiteSpace(dept.Source.SystemId))
                    {
                        if (!sourceSystems.ContainsKey(dept.Source.SystemId))
                        {
                            sourceSystems[dept.Source.SystemId] = (
                                dept.Source.DisplayName ?? "Unknown",
                                dept.Source.IdentificationKey ?? dept.Source.SystemId
                            );
                        }
                    }
                }
            }

            // Step 2.5: Import source systems first
            progress?.Report(new ImportProgress { CurrentOperation = $"Importing {sourceSystems.Count} source systems..." });

            using (var connection = _connectionFactory.CreateConnection(enforceForeignKeys: false))
            {
                foreach (var (systemId, (displayName, identificationKey)) in sourceSystems)
                {
                    var rowsAffected = await connection.ExecuteAsync(
                        "INSERT OR IGNORE INTO source_system (system_id, display_name, identification_key) VALUES (@SystemId, @DisplayName, @IdentificationKey)",
                        new { SystemId = systemId, DisplayName = displayName, IdentificationKey = identificationKey });
                    result.SourceSystemsImported += rowsAffected;
                }
            }

            // Create source lookup dictionary for mapping
            var sourceLookup = sourceSystems.Keys.ToDictionary(id => id, id => id);

            // Step 3: Collect all lookup tables from contracts
            progress?.Report(new ImportProgress { CurrentOperation = "Collecting company data..." });

            var organizations = new HashSet<VaultReference>(new ReferenceComparer());
            var locations = new HashSet<VaultReference>(new ReferenceComparer());
            var employers = new HashSet<VaultReference>(new ReferenceComparer());
            var costCenters = new HashSet<VaultReference>(new ReferenceComparer());
            var costBearers = new HashSet<VaultReference>(new ReferenceComparer());
            var teams = new HashSet<VaultReference>(new ReferenceComparer());
            var divisions = new HashSet<VaultReference>(new ReferenceComparer());
            var titles = new HashSet<VaultReference>(new ReferenceComparer());
            var departments = new HashSet<Department>(new DepartmentComparer());

            // Track source for each reference entity by external_id
            var organizationSources = new Dictionary<string, string?>();
            var locationSources = new Dictionary<string, string?>();
            var employerSources = new Dictionary<string, string?>();
            var costCenterSources = new Dictionary<string, string?>();
            var costBearerSources = new Dictionary<string, string?>();
            var teamSources = new Dictionary<string, string?>();
            var divisionSources = new Dictionary<string, string?>();
            var titleSources = new Dictionary<string, string?>();

            // Track seen names to enforce uniqueness (for entities without external_id)
            var seenOrganizations = new Dictionary<string, VaultReference>();
            var seenLocations = new Dictionary<string, VaultReference>();
            var seenEmployers = new Dictionary<string, VaultReference>();
            var seenCostCenters = new Dictionary<string, VaultReference>();
            var seenCostBearers = new Dictionary<string, VaultReference>();
            var seenTeams = new Dictionary<string, VaultReference>();
            var seenDivisions = new Dictionary<string, VaultReference>();
            var seenTitles = new Dictionary<string, VaultReference>();

            // Extract departments from root-level Departments array (full data with hierarchy)
            if (vaultData.Departments != null && vaultData.Departments.Any())
            {
                progress?.Report(new ImportProgress { CurrentOperation = $"Extracting {vaultData.Departments.Count} departments..." });
                foreach (var deptRef in vaultData.Departments)
                {
                    if (!string.IsNullOrWhiteSpace(deptRef.ExternalId))
                    {
                        departments.Add(MapDepartment(deptRef, sourceLookup));
                    }
                }
            }

            // Extract other lookup tables from contracts with source tracking
            if (vaultData.Persons != null)
            {
                foreach (var vaultPerson in vaultData.Persons)
                {
                    foreach (var contract in vaultPerson.Contracts)
                    {
                        // Get contract source for inheritance
                        string? contractSource = null;
                        if (contract.Source?.SystemId != null && sourceLookup.TryGetValue(contract.Source.SystemId, out var sourceId))
                        {
                            contractSource = sourceId;
                        }

                        if (contract.Organization != null && !string.IsNullOrWhiteSpace(contract.Organization.Name))
                        {
                            // Create unique key for deduplication: name|source
                            var nameKey = $"{contract.Organization.Name}|{contractSource ?? "default"}";

                            // Skip if we've already seen this name+source combination
                            if (!seenOrganizations.ContainsKey(nameKey))
                            {
                                string orgExternalId;
                                if (!string.IsNullOrWhiteSpace(contract.Organization.ExternalId))
                                {
                                    // Has external_id: use it directly (no hash transformation)
                                    orgExternalId = contract.Organization.ExternalId;
                                }
                                else
                                {
                                    // Missing external_id: generate random GUID
                                    orgExternalId = Guid.NewGuid().ToString();
                                }

                                var transformedOrganization = new VaultReference
                                {
                                    ExternalId = orgExternalId,
                                    Code = contract.Organization.Code,
                                    Name = contract.Organization.Name
                                };
                                seenOrganizations[nameKey] = transformedOrganization;
                                organizations.Add(transformedOrganization);
                                if (!organizationSources.ContainsKey(orgExternalId))
                                {
                                    organizationSources[orgExternalId] = contractSource;
                                }
                            }
                        }
                        if (contract.Location != null && !string.IsNullOrWhiteSpace(contract.Location.Name))
                        {
                            // Create unique key for deduplication: name|source
                            var nameKey = $"{contract.Location.Name}|{contractSource ?? "default"}";

                            // Skip if we've already seen this name+source combination
                            if (!seenLocations.ContainsKey(nameKey))
                            {
                                string locationExternalId;
                                if (!string.IsNullOrWhiteSpace(contract.Location.ExternalId))
                                {
                                    // Has external_id: use it directly (no hash transformation)
                                    locationExternalId = contract.Location.ExternalId;
                                }
                                else
                                {
                                    // Missing external_id: generate random GUID
                                    locationExternalId = Guid.NewGuid().ToString();
                                }

                                var transformedLocation = new VaultReference
                                {
                                    ExternalId = locationExternalId,
                                    Code = contract.Location.Code,
                                    Name = contract.Location.Name
                                };
                                seenLocations[nameKey] = transformedLocation;
                                locations.Add(transformedLocation);
                                if (!locationSources.ContainsKey(locationExternalId))
                                {
                                    locationSources[locationExternalId] = contractSource;
                                }
                            }
                        }
                        if (contract.Employer != null && !string.IsNullOrWhiteSpace(contract.Employer.Name))
                        {
                            // Create unique key for deduplication: name|source
                            var nameKey = $"{contract.Employer.Name}|{contractSource ?? "default"}";

                            // Skip if we've already seen this name+source combination
                            if (!seenEmployers.ContainsKey(nameKey))
                            {
                                string employerExternalId;
                                if (!string.IsNullOrWhiteSpace(contract.Employer.ExternalId))
                                {
                                    // Has external_id: use it directly (no hash transformation)
                                    employerExternalId = contract.Employer.ExternalId;
                                }
                                else
                                {
                                    // Missing external_id: generate random GUID
                                    employerExternalId = Guid.NewGuid().ToString();
                                }

                                var transformedEmployer = new VaultReference
                                {
                                    ExternalId = employerExternalId,
                                    Code = contract.Employer.Code,
                                    Name = contract.Employer.Name
                                };
                                seenEmployers[nameKey] = transformedEmployer;
                                employers.Add(transformedEmployer);
                                if (!employerSources.ContainsKey(employerExternalId))
                                {
                                    employerSources[employerExternalId] = contractSource;
                                    if (!string.IsNullOrWhiteSpace(contract.Employer.ExternalId))
                                    {
                                        Debug.WriteLine($"[Import CompanyOnly] Collecting employer {contract.Employer.ExternalId}→{employerExternalId} ({contract.Employer.Code}) {contract.Employer.Name} from contract, inherited source: {contractSource}");
                                    }
                                }
                            }
                        }
                        if (contract.CostCenter != null && !string.IsNullOrWhiteSpace(contract.CostCenter.Name))
                        {
                            // Create unique key for deduplication: name|source
                            var nameKey = $"{contract.CostCenter.Name}|{contractSource ?? "default"}";

                            // Skip if we've already seen this name+source combination
                            if (!seenCostCenters.ContainsKey(nameKey))
                            {
                                string costCenterExternalId;
                                if (!string.IsNullOrWhiteSpace(contract.CostCenter.ExternalId))
                                {
                                    // Has external_id: use it directly (no hash transformation)
                                    costCenterExternalId = contract.CostCenter.ExternalId;
                                }
                                else
                                {
                                    // Missing external_id: generate random GUID
                                    costCenterExternalId = Guid.NewGuid().ToString();
                                }

                                var transformedCostCenter = new VaultReference
                                {
                                    ExternalId = costCenterExternalId,
                                    Code = contract.CostCenter.Code,
                                    Name = contract.CostCenter.Name
                                };
                                seenCostCenters[nameKey] = transformedCostCenter;
                                costCenters.Add(transformedCostCenter);
                                if (!costCenterSources.ContainsKey(costCenterExternalId))
                                {
                                    costCenterSources[costCenterExternalId] = contractSource;
                                }
                            }
                        }
                        if (contract.CostBearer != null && !string.IsNullOrWhiteSpace(contract.CostBearer.Name))
                        {
                            // Create unique key for deduplication: name|source
                            var nameKey = $"{contract.CostBearer.Name}|{contractSource ?? "default"}";

                            // Skip if we've already seen this name+source combination
                            if (!seenCostBearers.ContainsKey(nameKey))
                            {
                                string costBearerExternalId;
                                if (!string.IsNullOrWhiteSpace(contract.CostBearer.ExternalId))
                                {
                                    // Has external_id: use it directly (no hash transformation)
                                    costBearerExternalId = contract.CostBearer.ExternalId;
                                }
                                else
                                {
                                    // Missing external_id: generate random GUID
                                    costBearerExternalId = Guid.NewGuid().ToString();
                                }

                                var transformedCostBearer = new VaultReference
                                {
                                    ExternalId = costBearerExternalId,
                                    Code = contract.CostBearer.Code,
                                    Name = contract.CostBearer.Name
                                };
                                seenCostBearers[nameKey] = transformedCostBearer;
                                costBearers.Add(transformedCostBearer);
                                if (!costBearerSources.ContainsKey(costBearerExternalId))
                                {
                                    costBearerSources[costBearerExternalId] = contractSource;
                                }
                            }
                        }
                        if (contract.Team != null && !string.IsNullOrWhiteSpace(contract.Team.Name))
                        {
                            // Create unique key for deduplication: name|source
                            var nameKey = $"{contract.Team.Name}|{contractSource ?? "default"}";

                            // Skip if we've already seen this name+source combination
                            if (!seenTeams.ContainsKey(nameKey))
                            {
                                string teamExternalId;
                                if (!string.IsNullOrWhiteSpace(contract.Team.ExternalId))
                                {
                                    // Has external_id: use it directly (no hash transformation)
                                    teamExternalId = contract.Team.ExternalId;
                                }
                                else
                                {
                                    // Missing external_id: generate random GUID
                                    teamExternalId = Guid.NewGuid().ToString();
                                }

                                var transformedTeam = new VaultReference
                                {
                                    ExternalId = teamExternalId,
                                    Code = contract.Team.Code,
                                    Name = contract.Team.Name
                                };
                                seenTeams[nameKey] = transformedTeam;
                                teams.Add(transformedTeam);
                                if (!teamSources.ContainsKey(teamExternalId))
                                {
                                    teamSources[teamExternalId] = contractSource;
                                }
                            }
                        }
                        if (contract.Division != null && !string.IsNullOrWhiteSpace(contract.Division.Name))
                        {
                            // Create unique key for deduplication: name|source
                            var nameKey = $"{contract.Division.Name}|{contractSource ?? "default"}";

                            // Skip if we've already seen this name+source combination
                            if (!seenDivisions.ContainsKey(nameKey))
                            {
                                string divisionExternalId;
                                if (!string.IsNullOrWhiteSpace(contract.Division.ExternalId))
                                {
                                    // Has external_id: use it directly (no hash transformation)
                                    divisionExternalId = contract.Division.ExternalId;
                                }
                                else
                                {
                                    // Missing external_id: generate random GUID
                                    divisionExternalId = Guid.NewGuid().ToString();
                                }

                                var transformedDivision = new VaultReference
                                {
                                    ExternalId = divisionExternalId,
                                    Code = contract.Division.Code,
                                    Name = contract.Division.Name
                                };
                                seenDivisions[nameKey] = transformedDivision;
                                divisions.Add(transformedDivision);
                                if (!divisionSources.ContainsKey(divisionExternalId))
                                {
                                    divisionSources[divisionExternalId] = contractSource;
                                }
                            }
                        }
                        if (contract.Title != null && !string.IsNullOrWhiteSpace(contract.Title.Name))
                        {
                            // Create unique key for deduplication: name|source
                            var nameKey = $"{contract.Title.Name}|{contractSource ?? "default"}";

                            // Skip if we've already seen this name+source combination
                            if (!seenTitles.ContainsKey(nameKey))
                            {
                                string titleExternalId;
                                if (!string.IsNullOrWhiteSpace(contract.Title.ExternalId))
                                {
                                    // Has external_id: use it directly (no hash transformation)
                                    titleExternalId = contract.Title.ExternalId;
                                }
                                else
                                {
                                    // Missing external_id: generate random GUID
                                    titleExternalId = Guid.NewGuid().ToString();
                                }

                                var transformedTitle = new VaultReference
                                {
                                    ExternalId = titleExternalId,
                                    Code = contract.Title.Code,
                                    Name = contract.Title.Name
                                };
                                seenTitles[nameKey] = transformedTitle;
                                titles.Add(transformedTitle);
                                if (!titleSources.ContainsKey(titleExternalId))
                                {
                                    titleSources[titleExternalId] = contractSource;
                                }
                            }
                        }

                        // Also collect departments from contracts in case they're not in root array
                        if (contract.Department?.ExternalId != null)
                        {
                            departments.Add(MapDepartment(contract.Department, sourceLookup));
                        }
                    }
                }
            }

            // Step 4: Import company data tables
            progress?.Report(new ImportProgress { CurrentOperation = "Importing company data..." });

            using (var connection = _connectionFactory.CreateConnection(enforceForeignKeys: false))
            {
                // Insert organizations
                foreach (var org in organizations)
                {
                    var source = organizationSources.TryGetValue(org.ExternalId ?? string.Empty, out var orgSource) ? orgSource : null;
                    var rowsAffected = await connection.ExecuteAsync(
                        "INSERT OR IGNORE INTO organizations (external_id, code, name, source) VALUES (@ExternalId, @Code, @Name, @Source)",
                        new { org.ExternalId, org.Code, org.Name, Source = source });
                    result.OrganizationsImported += rowsAffected;
                }

                // Insert locations
                foreach (var loc in locations)
                {
                    var source = locationSources.TryGetValue(loc.ExternalId ?? string.Empty, out var locSource) ? locSource : null;
                    var rowsAffected = await connection.ExecuteAsync(
                        "INSERT OR IGNORE INTO locations (external_id, code, name, source) VALUES (@ExternalId, @Code, @Name, @Source)",
                        new { loc.ExternalId, loc.Code, loc.Name, Source = source });
                    result.LocationsImported += rowsAffected;
                }

                // Insert employers
                Debug.WriteLine($"[Import CompanyOnly] About to insert {employers.Count} employers:");
                foreach (var emp in employers)
                {
                    var source = employerSources.TryGetValue(emp.ExternalId ?? string.Empty, out var empSource) ? empSource : null;
                    Debug.WriteLine($"[Import CompanyOnly] Inserting employer: {emp.ExternalId} ({emp.Code}) {emp.Name} - Source: {source}");
                    var rowsAffected = await connection.ExecuteAsync(
                        "INSERT OR IGNORE INTO employers (external_id, code, name, source) VALUES (@ExternalId, @Code, @Name, @Source)",
                        new { emp.ExternalId, emp.Code, emp.Name, Source = source });
                    result.EmployersImported += rowsAffected;
                    Debug.WriteLine($"[Import CompanyOnly] Employer insert result: {rowsAffected} rows affected");
                }
                Debug.WriteLine($"[Import CompanyOnly] Total employers imported: {result.EmployersImported}");

                // Insert cost centers
                foreach (var cc in costCenters)
                {
                    var source = costCenterSources.TryGetValue(cc.ExternalId ?? string.Empty, out var ccSource) ? ccSource : null;
                    var rowsAffected = await connection.ExecuteAsync(
                        "INSERT OR IGNORE INTO cost_centers (external_id, code, name, source) VALUES (@ExternalId, @Code, @Name, @Source)",
                        new { cc.ExternalId, cc.Code, cc.Name, Source = source });
                    result.CostCentersImported += rowsAffected;
                }

                // Insert cost bearers
                foreach (var cb in costBearers)
                {
                    var source = costBearerSources.TryGetValue(cb.ExternalId ?? string.Empty, out var cbSource) ? cbSource : null;
                    var rowsAffected = await connection.ExecuteAsync(
                        "INSERT OR IGNORE INTO cost_bearers (external_id, code, name, source) VALUES (@ExternalId, @Code, @Name, @Source)",
                        new { cb.ExternalId, cb.Code, cb.Name, Source = source });
                    result.CostBearersImported += rowsAffected;
                }

                // Insert teams
                foreach (var team in teams)
                {
                    var source = teamSources.TryGetValue(team.ExternalId ?? string.Empty, out var teamSource) ? teamSource : null;
                    var rowsAffected = await connection.ExecuteAsync(
                        "INSERT OR IGNORE INTO teams (external_id, code, name, source) VALUES (@ExternalId, @Code, @Name, @Source)",
                        new { team.ExternalId, team.Code, team.Name, Source = source });
                    result.TeamsImported += rowsAffected;
                }

                // Insert divisions
                foreach (var div in divisions)
                {
                    var source = divisionSources.TryGetValue(div.ExternalId ?? string.Empty, out var divSource) ? divSource : null;
                    var rowsAffected = await connection.ExecuteAsync(
                        "INSERT OR IGNORE INTO divisions (external_id, code, name, source) VALUES (@ExternalId, @Code, @Name, @Source)",
                        new { div.ExternalId, div.Code, div.Name, Source = source });
                    result.DivisionsImported += rowsAffected;
                }

                // Insert titles
                foreach (var title in titles)
                {
                    var source = titleSources.TryGetValue(title.ExternalId ?? string.Empty, out var titleSource) ? titleSource : null;
                    var rowsAffected = await connection.ExecuteAsync(
                        "INSERT OR IGNORE INTO titles (external_id, code, name, source) VALUES (@ExternalId, @Code, @Name, @Source)",
                        new { title.ExternalId, title.Code, title.Name, Source = source });
                    result.TitlesImported += rowsAffected;
                }
            }

            // Step 5: Import departments (skip persons since we're not importing them)
            progress?.Report(new ImportProgress { CurrentOperation = "Importing departments..." });

            // Perform topological sort to ensure parents are inserted before children
            List<Department> sortedDepartments;
            try
            {
                sortedDepartments = TopologicalSortDepartments(departments.ToList());
            }
            catch (InvalidOperationException ex)
            {
                throw new Exception($"Department hierarchy error: {ex.Message}", ex);
            }

            // Import departments in transaction for atomicity
            using (var connection = _connectionFactory.CreateConnection(enforceForeignKeys: false))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        foreach (var dept in sortedDepartments)
                        {
                            await _departmentRepository.InsertAsync(dept, connection, transaction);
                            result.DepartmentsImported++;
                        }

                        // Validate and auto-fix orphaned departments (manager references will be ignored since no persons)
                        progress?.Report(new ImportProgress { CurrentOperation = "Validating department data..." });

                        // Check for orphaned departments (parent doesn't exist)
                        var orphanedDepts = await connection.QueryAsync<(string ExternalId, string DisplayName, string ParentExternalId)>(@"
                            SELECT d.external_id as ExternalId, d.display_name as DisplayName, d.parent_external_id as ParentExternalId
                            FROM departments d
                            LEFT JOIN departments p ON d.parent_external_id = p.external_id
                            WHERE d.parent_external_id IS NOT NULL
                              AND d.parent_external_id != ''
                              AND p.external_id IS NULL", transaction: transaction);

                        if (orphanedDepts.Any())
                        {
                            Console.WriteLine($"WARNING: Found {orphanedDepts.Count()} orphaned departments. Auto-fixing by setting parent to NULL:");
                            foreach (var orphan in orphanedDepts)
                            {
                                Console.WriteLine($"  - {orphan.DisplayName} ({orphan.ExternalId}) has non-existent parent: {orphan.ParentExternalId}");

                                // Auto-fix: Set parent to NULL
                                await connection.ExecuteAsync(@"
                                    UPDATE departments
                                    SET parent_external_id = NULL
                                    WHERE external_id = @ExternalId",
                                    new { ExternalId = orphan.ExternalId },
                                    transaction);
                            }
                        }

                        // Clear all manager references since we're not importing persons
                        await connection.ExecuteAsync(@"
                            UPDATE departments
                            SET manager_person_id = NULL
                            WHERE manager_person_id IS NOT NULL", transaction: transaction);

                        transaction.Commit();
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        throw new Exception($"Department import failed and was rolled back. Error: {ex.Message}", ex);
                    }
                }
            }

            progress?.Report(new ImportProgress
            {
                CurrentOperation = "Company data import completed successfully!",
                ProcessedItems = 1,
                TotalItems = 1
            });
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = $"Company import failed: {ex.Message}";
        }
        finally
        {
            // Re-enable foreign key constraints
            try
            {
                using var connection = _connectionFactory.CreateConnection();
                await connection.ExecuteAsync("PRAGMA foreign_keys = ON;");
            }
            catch
            {
                // Ignore errors when re-enabling foreign keys
            }

            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;
        }

        // Clear reference data cache so UI shows fresh data
        _referenceDataService.ClearCache();

        // Log data quality summary
        System.Diagnostics.Debug.WriteLine($"[Import] Data Quality Summary:");
        System.Diagnostics.Debug.WriteLine($"  Empty manager GUIDs replaced: {result.EmptyManagerGuidsReplaced}");
        if (result.EmptyManagerGuidsReplaced > 0)
        {
            System.Diagnostics.Debug.WriteLine($"  → These contracts will have NULL manager instead of invalid GUID");
        }

        return result;
    }

    private Person MapPerson(VaultPerson vaultPerson, Dictionary<string, string> sourceLookup, PrimaryManagerLogic primaryManagerLogic)
    {
        // Get source from lookup, or null if not found
        string? sourceId = null;
        if (vaultPerson.Source?.SystemId != null && sourceLookup.TryGetValue(vaultPerson.Source.SystemId, out var mappedSourceId))
        {
            sourceId = mappedSourceId;
        }

        // Handle primary manager for FromJson logic
        string? primaryManagerPersonId = null;
        string? primaryManagerSource = null;
        if (primaryManagerLogic == PrimaryManagerLogic.FromJson)
        {
            primaryManagerPersonId = vaultPerson.PrimaryManager?.PersonId;
            primaryManagerSource = "import";
        }

        return new Person
        {
            PersonId = vaultPerson.PersonId,
            DisplayName = vaultPerson.DisplayName,
            ExternalId = vaultPerson.ExternalId,
            UserName = vaultPerson.UserName,
            Gender = vaultPerson.Details?.Gender,
            BirthDate = vaultPerson.Details?.BirthDate?.ToString("yyyy-MM-dd"),
            BirthLocality = vaultPerson.Details?.BirthLocality,
            Initials = vaultPerson.Name?.Initials,
            GivenName = vaultPerson.Name?.GivenName,
            FamilyName = vaultPerson.Name?.FamilyName,
            FamilyNamePrefix = vaultPerson.Name?.FamilyNamePrefix,
            FamilyNamePartner = vaultPerson.Name?.FamilyNamePartner,
            FamilyNamePartnerPrefix = vaultPerson.Name?.FamilyNamePartnerPrefix,
            Convention = vaultPerson.Name?.Convention,
            HonorificPrefix = vaultPerson.Details?.HonorificPrefix,
            HonorificSuffix = vaultPerson.Details?.HonorificSuffix,
            NickName = vaultPerson.Name?.NickName,
            MaritalStatus = vaultPerson.Details?.MaritalStatus,
            Blocked = vaultPerson.Status?.Blocked ?? false,
            StatusReason = vaultPerson.Status?.Reason,
            Excluded = vaultPerson.Excluded,
            HrExcluded = vaultPerson.ExclusionDetails?.Hr ?? false,
            ManualExcluded = vaultPerson.ExclusionDetails?.Manual ?? false,
            Source = sourceId,
            PrimaryManagerPersonId = primaryManagerPersonId,
            PrimaryManagerUpdatedAt = primaryManagerPersonId != null ? DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") : null
        };
    }

    /// <summary>
    /// Resolves the external_id for a reference entity by looking up the transformed GUID from seenDictionary.
    /// Handles cases where entities without external_id get a generated GUID during collection.
    /// </summary>
    private string? ResolveReferenceExternalId(VaultReference? reference, string? contractSource,
        Dictionary<string, VaultReference> seenDictionary)
    {
        if (reference == null || string.IsNullOrWhiteSpace(reference.Name))
            return null;

        var key = $"{reference.Name}|{contractSource ?? "default"}";
        if (seenDictionary.TryGetValue(key, out var transformed))
            return transformed.ExternalId;

        return null;
    }

    private Contract MapContract(string personId, VaultContract vaultContract, Dictionary<string, string> sourceLookup, ImportResult result,
        Dictionary<string, VaultReference> seenLocations,
        Dictionary<string, VaultReference> seenEmployers,
        Dictionary<string, VaultReference> seenCostCenters,
        Dictionary<string, VaultReference> seenCostBearers,
        Dictionary<string, VaultReference> seenTeams,
        Dictionary<string, VaultReference> seenDivisions,
        Dictionary<string, VaultReference> seenTitles,
        Dictionary<string, VaultReference> seenOrganizations)
    {
        // Get source from lookup, or null if not found
        string? sourceId = null;
        if (vaultContract.Source?.SystemId != null && sourceLookup.TryGetValue(vaultContract.Source.SystemId, out var mappedSourceId))
        {
            sourceId = mappedSourceId;
        }

        // Debug: Log first few contracts to see ExternalId values
        if (_contractDebugCounter++ < 5)
        {
            Debug.WriteLine($"[MapContract] Contract {_contractDebugCounter}: ExternalId={vaultContract.ExternalId}, " +
                         $"Location={vaultContract.Location?.ExternalId ?? "NULL"}, " +
                         $"Department={vaultContract.Department?.ExternalId ?? "NULL"}, " +
                         $"Employer={vaultContract.Employer?.ExternalId ?? "NULL"}, " +
                         $"CostCenter={vaultContract.CostCenter?.ExternalId ?? "NULL"}, " +
                         $"CostBearer={vaultContract.CostBearer?.ExternalId ?? "NULL"}, " +
                         $"Team={vaultContract.Team?.ExternalId ?? "NULL"}, " +
                         $"Division={vaultContract.Division?.ExternalId ?? "NULL"}, " +
                         $"Title={vaultContract.Title?.ExternalId ?? "NULL"}, " +
                         $"Organization={vaultContract.Organization?.ExternalId ?? "NULL"}, " +
                         $"sourceId={sourceId ?? "NULL"}");
        }

        // Check if manager GUID is empty/blank - count and replace with null
        string? managerPersonId = vaultContract.Manager?.PersonId;
        if (managerPersonId == "00000000-0000-0000-0000-000000000000" || string.IsNullOrWhiteSpace(managerPersonId))
        {
            result.EmptyManagerGuidsReplaced++;
            managerPersonId = null;
            System.Diagnostics.Debug.WriteLine($"[MapContract] Empty manager GUID replaced for contract {vaultContract.ExternalId} (person {personId})");
        }

        return new Contract
        {
            PersonId = personId,
            ExternalId = vaultContract.ExternalId,
            StartDate = vaultContract.StartDate?.ToString("yyyy-MM-dd"),
            EndDate = vaultContract.EndDate?.ToString("yyyy-MM-dd"),
            Fte = (double?)vaultContract.Details?.Fte,
            HoursPerWeek = (double?)vaultContract.Details?.HoursPerWeek,
            Percentage = (double?)vaultContract.Details?.Percentage,
            Sequence = vaultContract.Details?.Sequence,
            TypeCode = vaultContract.Type?.Code,
            TypeDescription = vaultContract.Type?.Description,
            LocationExternalId = ResolveReferenceExternalId(vaultContract.Location, sourceId, seenLocations),
            LocationSource = sourceId,
            DepartmentExternalId = !string.IsNullOrWhiteSpace(vaultContract.Department?.ExternalId) && sourceId != null ?
                vaultContract.Department.ExternalId : null,
            DepartmentSource = sourceId,
            CostCenterExternalId = ResolveReferenceExternalId(vaultContract.CostCenter, sourceId, seenCostCenters),
            CostCenterSource = sourceId,
            CostBearerExternalId = ResolveReferenceExternalId(vaultContract.CostBearer, sourceId, seenCostBearers),
            CostBearerSource = sourceId,
            EmployerExternalId = ResolveReferenceExternalId(vaultContract.Employer, sourceId, seenEmployers),
            EmployerSource = sourceId,
            TitleExternalId = ResolveReferenceExternalId(vaultContract.Title, sourceId, seenTitles),
            TitleSource = sourceId,
            TeamExternalId = ResolveReferenceExternalId(vaultContract.Team, sourceId, seenTeams),
            TeamSource = sourceId,
            DivisionExternalId = ResolveReferenceExternalId(vaultContract.Division, sourceId, seenDivisions),
            DivisionSource = sourceId,
            OrganizationExternalId = ResolveReferenceExternalId(vaultContract.Organization, sourceId, seenOrganizations),
            OrganizationSource = sourceId,
            ManagerPersonExternalId = managerPersonId,
            Source = sourceId
        };
    }

    private Contact MapContact(string personId, string type, VaultContactInfo contactInfo)
    {
        return new Contact
        {
            PersonId = personId,
            Type = type,
            Email = contactInfo.Email,
            PhoneMobile = contactInfo.Phone?.Mobile,
            PhoneFixed = contactInfo.Phone?.Fixed,
            AddressStreet = contactInfo.Address?.Street,
            AddressHouseNumber = contactInfo.Address?.HouseNumber,
            AddressPostal = contactInfo.Address?.PostalCode,
            AddressLocality = contactInfo.Address?.Locality,
            AddressCountry = contactInfo.Address?.Country
        };
    }

    /// <summary>
    /// Checks if a contact contains any actual data.
    /// Returns true if ALL fields are null or empty (contact should be skipped).
    /// Returns false if at least ONE field has data (contact should be inserted).
    /// </summary>
    private bool IsEmptyContact(VaultContactInfo contactInfo)
    {
        // Contact is considered empty if ALL fields are null or empty
        return string.IsNullOrWhiteSpace(contactInfo.Email) &&
               string.IsNullOrWhiteSpace(contactInfo.Phone?.Mobile) &&
               string.IsNullOrWhiteSpace(contactInfo.Phone?.Fixed) &&
               string.IsNullOrWhiteSpace(contactInfo.Address?.Street) &&
               string.IsNullOrWhiteSpace(contactInfo.Address?.HouseNumber) &&
               string.IsNullOrWhiteSpace(contactInfo.Address?.PostalCode) &&
               string.IsNullOrWhiteSpace(contactInfo.Address?.Locality) &&
               string.IsNullOrWhiteSpace(contactInfo.Address?.Country);
    }

    /// <summary>
    /// Validates contract references using source-aware lookups.
    /// Detects orphaned references (contract references entity that doesn't exist in master table with matching source).
    /// </summary>
    private async Task ValidateContractReferencesAsync(ImportResult result)
    {
        using var connection = _connectionFactory.CreateConnection();

        // Debug: Check total contracts in database
        var totalContractsInDb = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM contracts");
        Debug.WriteLine($"[Import Validation] Database contains {totalContractsInDb} total contracts");

        // Validate departments
        var orphanedDepts = await connection.QueryAsync<(string ContractId, string DepartmentId, string Source)>(@"
            SELECT
                c.external_id AS ContractId,
                c.department_external_id AS DepartmentId,
                c.source AS Source
            FROM contracts c
            WHERE c.department_external_id IS NOT NULL
            AND NOT EXISTS (
                SELECT 1 FROM departments d
                WHERE d.external_id = c.department_external_id
                AND d.source = c.source
            )");

        if (orphanedDepts.Any())
        {
            result.OrphanedDepartmentsDetected = orphanedDepts.Count();
            Debug.WriteLine($"[Import Validation] Found {result.OrphanedDepartmentsDetected} orphaned department reference(s):");
            foreach (var orphan in orphanedDepts.Take(10)) // Log first 10
            {
                Debug.WriteLine($"  - Contract {orphan.ContractId} → Department {orphan.DepartmentId} (Source: {orphan.Source}) - not in master table");
            }
            if (orphanedDepts.Count() > 10)
            {
                Debug.WriteLine($"  ... and {orphanedDepts.Count() - 10} more");
            }
        }

        // Validate locations
        var orphanedLocs = await connection.QueryAsync<(string ContractId, string LocationId, string Source)>(@"
            SELECT
                c.external_id AS ContractId,
                c.location_external_id AS LocationId,
                c.source AS Source
            FROM contracts c
            WHERE c.location_external_id IS NOT NULL
            AND NOT EXISTS (
                SELECT 1 FROM locations l
                WHERE l.external_id = c.location_external_id
                AND l.source = c.source
            )");

        if (orphanedLocs.Any())
        {
            result.OrphanedLocationsDetected = orphanedLocs.Count();
            Debug.WriteLine($"[Import Validation] Found {result.OrphanedLocationsDetected} orphaned location reference(s):");
            foreach (var orphan in orphanedLocs.Take(10))
            {
                Debug.WriteLine($"  - Contract {orphan.ContractId} → Location {orphan.LocationId} (Source: {orphan.Source}) - not in master table");
            }
            if (orphanedLocs.Count() > 10)
            {
                Debug.WriteLine($"  ... and {orphanedLocs.Count() - 10} more");
            }
        }

        // Validate cost centers
        var orphanedCCs = await connection.QueryAsync<(string ContractId, string CostCenterId, string Source)>(@"
            SELECT
                c.external_id AS ContractId,
                c.cost_center_external_id AS CostCenterId,
                c.source AS Source
            FROM contracts c
            WHERE c.cost_center_external_id IS NOT NULL
            AND NOT EXISTS (
                SELECT 1 FROM cost_centers cc
                WHERE cc.external_id = c.cost_center_external_id
                AND cc.source = c.source
            )");

        if (orphanedCCs.Any())
        {
            result.OrphanedCostCentersDetected = orphanedCCs.Count();
            Debug.WriteLine($"[Import Validation] Found {result.OrphanedCostCentersDetected} orphaned cost center reference(s):");
            foreach (var orphan in orphanedCCs.Take(10))
            {
                Debug.WriteLine($"  - Contract {orphan.ContractId} → Cost Center {orphan.CostCenterId} (Source: {orphan.Source}) - not in master table");
            }
            if (orphanedCCs.Count() > 10)
            {
                Debug.WriteLine($"  ... and {orphanedCCs.Count() - 10} more");
            }
        }

        // Validate cost bearers
        var orphanedCBs = await connection.QueryAsync<(string ContractId, string CostBearerId, string Source)>(@"
            SELECT
                c.external_id AS ContractId,
                c.cost_bearer_external_id AS CostBearerId,
                c.source AS Source
            FROM contracts c
            WHERE c.cost_bearer_external_id IS NOT NULL
            AND NOT EXISTS (
                SELECT 1 FROM cost_bearers cb
                WHERE cb.external_id = c.cost_bearer_external_id
                AND cb.source = c.source
            )");

        if (orphanedCBs.Any())
        {
            result.OrphanedCostBearersDetected = orphanedCBs.Count();
            Debug.WriteLine($"[Import Validation] Found {result.OrphanedCostBearersDetected} orphaned cost bearer reference(s):");
            foreach (var orphan in orphanedCBs.Take(10))
            {
                Debug.WriteLine($"  - Contract {orphan.ContractId} → Cost Bearer {orphan.CostBearerId} (Source: {orphan.Source}) - not in master table");
            }
            if (orphanedCBs.Count() > 10)
            {
                Debug.WriteLine($"  ... and {orphanedCBs.Count() - 10} more");
            }
        }

        // Validate employers
        var orphanedEmps = await connection.QueryAsync<(string ContractId, string EmployerId, string Source)>(@"
            SELECT
                c.external_id AS ContractId,
                c.employer_external_id AS EmployerId,
                c.source AS Source
            FROM contracts c
            WHERE c.employer_external_id IS NOT NULL
            AND NOT EXISTS (
                SELECT 1 FROM employers e
                WHERE e.external_id = c.employer_external_id
                AND e.source = c.source
            )");

        if (orphanedEmps.Any())
        {
            result.OrphanedEmployersDetected = orphanedEmps.Count();
            Debug.WriteLine($"[Import Validation] Found {result.OrphanedEmployersDetected} orphaned employer reference(s):");
            foreach (var orphan in orphanedEmps.Take(10))
            {
                Debug.WriteLine($"  - Contract {orphan.ContractId} → Employer {orphan.EmployerId} (Source: {orphan.Source}) - not in master table");
            }
            if (orphanedEmps.Count() > 10)
            {
                Debug.WriteLine($"  ... and {orphanedEmps.Count() - 10} more");
            }
        }

        // Validate teams
        var orphanedTeams = await connection.QueryAsync<(string ContractId, string TeamId, string Source)>(@"
            SELECT
                c.external_id AS ContractId,
                c.team_external_id AS TeamId,
                c.source AS Source
            FROM contracts c
            WHERE c.team_external_id IS NOT NULL
            AND NOT EXISTS (
                SELECT 1 FROM teams t
                WHERE t.external_id = c.team_external_id
                AND t.source = c.source
            )");

        if (orphanedTeams.Any())
        {
            result.OrphanedTeamsDetected = orphanedTeams.Count();
            Debug.WriteLine($"[Import Validation] Found {result.OrphanedTeamsDetected} orphaned team reference(s):");
            foreach (var orphan in orphanedTeams.Take(10))
            {
                Debug.WriteLine($"  - Contract {orphan.ContractId} → Team {orphan.TeamId} (Source: {orphan.Source}) - not in master table");
            }
            if (orphanedTeams.Count() > 10)
            {
                Debug.WriteLine($"  ... and {orphanedTeams.Count() - 10} more");
            }
        }

        // Validate divisions
        var orphanedDivs = await connection.QueryAsync<(string ContractId, string DivisionId, string Source)>(@"
            SELECT
                c.external_id AS ContractId,
                c.division_external_id AS DivisionId,
                c.source AS Source
            FROM contracts c
            WHERE c.division_external_id IS NOT NULL
            AND NOT EXISTS (
                SELECT 1 FROM divisions d
                WHERE d.external_id = c.division_external_id
                AND d.source = c.source
            )");

        if (orphanedDivs.Any())
        {
            result.OrphanedDivisionsDetected = orphanedDivs.Count();
            Debug.WriteLine($"[Import Validation] Found {result.OrphanedDivisionsDetected} orphaned division reference(s):");
            foreach (var orphan in orphanedDivs.Take(10))
            {
                Debug.WriteLine($"  - Contract {orphan.ContractId} → Division {orphan.DivisionId} (Source: {orphan.Source}) - not in master table");
            }
            if (orphanedDivs.Count() > 10)
            {
                Debug.WriteLine($"  ... and {orphanedDivs.Count() - 10} more");
            }
        }

        // Validate titles
        var orphanedTitles = await connection.QueryAsync<(string ContractId, string TitleId, string Source)>(@"
            SELECT
                c.external_id AS ContractId,
                c.title_external_id AS TitleId,
                c.source AS Source
            FROM contracts c
            WHERE c.title_external_id IS NOT NULL
            AND NOT EXISTS (
                SELECT 1 FROM titles t
                WHERE t.external_id = c.title_external_id
                AND t.source = c.source
            )");

        if (orphanedTitles.Any())
        {
            result.OrphanedTitlesDetected = orphanedTitles.Count();
            Debug.WriteLine($"[Import Validation] Found {result.OrphanedTitlesDetected} orphaned title reference(s):");
            foreach (var orphan in orphanedTitles.Take(10))
            {
                Debug.WriteLine($"  - Contract {orphan.ContractId} → Title {orphan.TitleId} (Source: {orphan.Source}) - not in master table");
            }
            if (orphanedTitles.Count() > 10)
            {
                Debug.WriteLine($"  ... and {orphanedTitles.Count() - 10} more");
            }
        }

        // Validate organizations
        var orphanedOrgs = await connection.QueryAsync<(string ContractId, string OrganizationId, string Source)>(@"
            SELECT
                c.external_id AS ContractId,
                c.organization_external_id AS OrganizationId,
                c.source AS Source
            FROM contracts c
            WHERE c.organization_external_id IS NOT NULL
            AND NOT EXISTS (
                SELECT 1 FROM organizations o
                WHERE o.external_id = c.organization_external_id
                AND o.source = c.source
            )");

        if (orphanedOrgs.Any())
        {
            result.OrphanedOrganizationsDetected = orphanedOrgs.Count();
            Debug.WriteLine($"[Import Validation] Found {result.OrphanedOrganizationsDetected} orphaned organization reference(s):");
            foreach (var orphan in orphanedOrgs.Take(10))
            {
                Debug.WriteLine($"  - Contract {orphan.ContractId} → Organization {orphan.OrganizationId} (Source: {orphan.Source}) - not in master table");
            }
            if (orphanedOrgs.Count() > 10)
            {
                Debug.WriteLine($"  ... and {orphanedOrgs.Count() - 10} more");
            }
        }

        // Summary log
        var totalOrphaned = result.OrphanedDepartmentsDetected + result.OrphanedLocationsDetected +
                           result.OrphanedCostCentersDetected + result.OrphanedCostBearersDetected +
                           result.OrphanedEmployersDetected + result.OrphanedTeamsDetected +
                           result.OrphanedDivisionsDetected + result.OrphanedTitlesDetected +
                           result.OrphanedOrganizationsDetected;

        if (totalOrphaned > 0)
        {
            Debug.WriteLine($"\n[Import Validation] Total orphaned references: {totalOrphaned}");
            Debug.WriteLine("[Import Validation] Orphaned references are contracts that reference entities not in master tables with matching source.");
            Debug.WriteLine("[Import Validation] This is acceptable by design - source is inherited from contract.");
        }
        else
        {
            Debug.WriteLine("[Import Validation] No orphaned references detected - all contract references match master table entries.");
        }
    }

    private Department MapDepartment(VaultDepartmentReference deptRef, Dictionary<string, string> sourceLookup)
    {
        // Convert null UUID to actual NULL
        // Check for: "00000000-0000-0000-0000-000000000000", empty string, or null
        string? managerPersonId = null;
        if (!string.IsNullOrWhiteSpace(deptRef.Manager?.PersonId) &&
            deptRef.Manager.PersonId != "00000000-0000-0000-0000-000000000000")
        {
            managerPersonId = deptRef.Manager.PersonId;
        }

        // Get source from lookup, or null if not found
        string? sourceId = null;
        if (deptRef.Source?.SystemId != null && sourceLookup.TryGetValue(deptRef.Source.SystemId, out var mappedSourceId))
        {
            sourceId = mappedSourceId;
        }

        // Apply hash transformation to ExternalId for namespace isolation
        string transformedExternalId = string.Empty;
        if (!string.IsNullOrWhiteSpace(deptRef.ExternalId) && sourceId != null)
        {
            transformedExternalId = deptRef.ExternalId;
        }

        // Also transform ParentExternalId if it exists
        string transformedParentExternalId = string.Empty;
        if (!string.IsNullOrWhiteSpace(deptRef.ParentExternalId) && sourceId != null)
        {
            transformedParentExternalId = deptRef.ParentExternalId;
        }

        return new Department
        {
            ExternalId = transformedExternalId,
            DisplayName = deptRef.DisplayName ?? string.Empty,
            Code = deptRef.Code,
            ParentExternalId = transformedParentExternalId,
            ManagerPersonId = managerPersonId,
            Source = sourceId
        };
    }

    /// <summary>
    /// Performs topological sort on departments to ensure parents are inserted before children.
    /// Uses depth-first traversal with cycle detection.
    /// </summary>
    private List<Department> TopologicalSortDepartments(List<Department> departments)
    {
        var sorted = new List<Department>();
        var visited = new HashSet<string>();
        var visiting = new HashSet<string>(); // For cycle detection
        var deptLookup = departments.ToDictionary(d => d.ExternalId);

        void Visit(Department dept)
        {
            // Skip if already processed
            if (visited.Contains(dept.ExternalId))
                return;

            // Detect cycles
            if (visiting.Contains(dept.ExternalId))
            {
                throw new InvalidOperationException(
                    $"Circular dependency detected in department hierarchy at: {dept.ExternalId} ({dept.DisplayName})");
            }

            visiting.Add(dept.ExternalId);

            // Visit parent first (if exists in our dataset)
            if (!string.IsNullOrEmpty(dept.ParentExternalId) &&
                deptLookup.TryGetValue(dept.ParentExternalId, out var parent))
            {
                Visit(parent);
            }

            visiting.Remove(dept.ExternalId);
            visited.Add(dept.ExternalId);
            sorted.Add(dept);
        }

        // Visit all departments
        foreach (var dept in departments)
        {
            Visit(dept);
        }

        return sorted;
    }

    private string DetectDataType(HashSet<object> sampleValues)
    {
        // Always return "text" as data type for all custom fields
        // Per user requirement: no auto-detection, all fields should be text type
        return "text";
    }

    private string FormatDisplayName(string fieldKey)
    {
        // Convert snake_case or camelCase to Title Case
        // Examples:
        //   ip_phone_number -> I P Phone Number
        //   mobilePhoneNumber -> Mobile Phone Number

        var words = new List<string>();
        var currentWord = "";

        for (int i = 0; i < fieldKey.Length; i++)
        {
            var c = fieldKey[i];

            if (c == '_' || c == '-')
            {
                if (currentWord.Length > 0)
                {
                    words.Add(currentWord);
                    currentWord = "";
                }
            }
            else if (char.IsUpper(c) && currentWord.Length > 0)
            {
                words.Add(currentWord);
                currentWord = c.ToString();
            }
            else
            {
                currentWord += c;
            }
        }

        if (currentWord.Length > 0)
        {
            words.Add(currentWord);
        }

        // Capitalize each word
        for (int i = 0; i < words.Count; i++)
        {
            if (words[i].Length == 1)
            {
                // Single letter words stay uppercase (like "I" in "IP")
                words[i] = words[i].ToUpper();
            }
            else
            {
                // Regular words: capitalize first letter
                words[i] = char.ToUpper(words[i][0]) + words[i].Substring(1).ToLower();
            }
        }

        return string.Join(" ", words);
    }

    private async Task CreateDatabaseBackupAsync()
    {
        string? dbPath = null;

        try
        {
            // Get the database file path from connection factory
            using (var connection = _connectionFactory.CreateConnection(enforceForeignKeys: false))
            {
                var sqliteConnection = connection as SqliteConnection;
                if (sqliteConnection == null)
                {
                    throw new Exception("Connection is not a SQLite connection");
                }

                dbPath = sqliteConnection.DataSource;

                if (string.IsNullOrEmpty(dbPath) || !File.Exists(dbPath))
                {
                    throw new Exception("Database file not found: " + dbPath);
                }

                // Close connection explicitly
                connection.Close();
            } // Dispose connection here

            // Clear all connections from the pool to release file locks
            SqliteConnection.ClearAllPools();

            // Wait for file locks to be fully released
            await Task.Delay(500);

            // Create backup filename with timestamp: vault_backup_{yyyymmdd}_{hhmmss}.db
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var dbDirectory = Path.GetDirectoryName(dbPath) ?? string.Empty;
            var backupPath = Path.Combine(dbDirectory, $"vault_backup_{timestamp}.db");

            // Copy database file to backup location
            File.Copy(dbPath, backupPath, overwrite: false);

            // Also copy WAL and SHM files if they exist
            var walPath = dbPath + "-wal";
            var shmPath = dbPath + "-shm";

            if (File.Exists(walPath))
            {
                File.Copy(walPath, backupPath + "-wal", overwrite: false);
            }

            if (File.Exists(shmPath))
            {
                File.Copy(shmPath, backupPath + "-shm", overwrite: false);
            }

            // Now delete the original database files to start fresh
            File.Delete(dbPath);

            if (File.Exists(walPath))
            {
                File.Delete(walPath);
            }

            if (File.Exists(shmPath))
            {
                File.Delete(shmPath);
            }

            // Recreate the database schema after deletion
            await _databaseInitializer.InitializeAsync();
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to create database backup: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Auto-detects which Primary Manager logic was used based on imported data.
    /// Samples persons and compares their imported primary manager with calculated values.
    /// </summary>
    private async Task<PrimaryManagerLogic?> DetectPrimaryManagerLogicAsync()
    {
        using var connection = _connectionFactory.CreateConnection();

        // Get persons who have a primary manager set from import
        var personsWithManager = await connection.QueryAsync<PersonWithManagerDto>(@"
            SELECT person_id AS PersonId, primary_manager_person_id AS ImportedManagerId
            FROM persons
            WHERE primary_manager_person_id IS NOT NULL
            LIMIT 100");  // Sample up to 100 persons for performance

        var personsList = personsWithManager.ToList();
        if (!personsList.Any())
        {
            Console.WriteLine("No persons with primary managers found to detect logic.");
            return null;
        }

        int contractBasedMatches = 0;
        int departmentBasedMatches = 0;

        foreach (var person in personsList)
        {
            // Calculate what the primary manager would be using each logic
            var contractBasedManager = await _primaryManagerService.CalculatePrimaryManagerAsync(person.PersonId, PrimaryManagerLogic.ContractBased);
            var departmentBasedManager = await _primaryManagerService.CalculatePrimaryManagerAsync(person.PersonId, PrimaryManagerLogic.DepartmentBased);

            // Count matches
            if (contractBasedManager == person.ImportedManagerId)
                contractBasedMatches++;
            if (departmentBasedManager == person.ImportedManagerId)
                departmentBasedMatches++;
        }

        Console.WriteLine($"Logic detection results: Contract-Based={contractBasedMatches} matches, Department-Based={departmentBasedMatches} matches (out of {personsList.Count} sampled)");

        // Determine which logic matches better
        PrimaryManagerLogic? detectedLogic;
        if (contractBasedMatches > departmentBasedMatches)
        {
            detectedLogic = PrimaryManagerLogic.ContractBased;
        }
        else if (departmentBasedMatches > contractBasedMatches)
        {
            detectedLogic = PrimaryManagerLogic.DepartmentBased;
        }
        else if (contractBasedMatches == 0 && departmentBasedMatches == 0)
        {
            // No matches - couldn't detect
            return null;
        }
        else
        {
            // Equal matches - default to Department-Based
            detectedLogic = PrimaryManagerLogic.DepartmentBased;
        }

        // Save the detected logic to user preferences
        _userPreferencesService.LastPrimaryManagerLogic = detectedLogic.Value;

        return detectedLogic;
    }

    private class PersonWithManagerDto
    {
        public string PersonId { get; set; } = string.Empty;
        public string? ImportedManagerId { get; set; }
    }

    private class DepartmentComparer : IEqualityComparer<Department>
    {
        public bool Equals(Department? x, Department? y)
        {
            if (x == null || y == null) return false;
            return x.ExternalId == y.ExternalId;
        }

        public int GetHashCode(Department obj)
        {
            return obj.ExternalId?.GetHashCode() ?? 0;
        }
    }

    private class ReferenceComparer : IEqualityComparer<VaultReference>
    {
        public bool Equals(VaultReference? x, VaultReference? y)
        {
            if (x == null || y == null) return false;
            return x.ExternalId == y.ExternalId && x.Name == y.Name;
        }

        public int GetHashCode(VaultReference obj)
        {
            var hash = obj.ExternalId?.GetHashCode() ?? 0;
            if (obj.Name != null)
                hash = HashCode.Combine(hash, obj.Name.GetHashCode());
            return hash;
        }
    }
}
