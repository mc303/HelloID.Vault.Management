using System.Diagnostics;
using System.Text.Json;
using Dapper;
using HelloID.Vault.Core.Models.Entities;
using HelloID.Vault.Core.Models.Json;
using HelloID.Vault.Data;
using HelloID.Vault.Data.Connection;
using HelloID.Vault.Data.Repositories.Interfaces;
using HelloID.Vault.Services.Database;
using HelloID.Vault.Services.Import.Collectors;
using HelloID.Vault.Services.Import.Mappers;
using HelloID.Vault.Services.Import.Models;
using HelloID.Vault.Services.Import.Utilities;
using HelloID.Vault.Services.Import.Validators;
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
    private readonly IDatabaseManager _databaseManager;
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
        IDatabaseManager databaseManager,
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
        _databaseManager = databaseManager ?? throw new ArgumentNullException(nameof(databaseManager));
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
        return await _databaseManager.HasDataAsync();
    }

    public async Task DeleteDatabaseAsync()
    {
        await _databaseManager.DeleteDatabaseAsync();
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
                await _databaseManager.CreateDatabaseBackupAsync();

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

            var context = ReferenceDataCollector.Collect(vaultData, sourceLookup);
            Debug.WriteLine($"[Import] Collected {context.Employers.Count} unique employers");

            // Step 3: Import lookup tables (no dependencies)
            progress?.Report(new ImportProgress { CurrentOperation = "Importing lookup tables..." });

            using (var connection = _connectionFactory.CreateConnection(enforceForeignKeys: false))
            {
                // Insert organizations
                foreach (var org in context.Organizations)
                {
                    var source = context.OrganizationSources.TryGetValue(org.ExternalId ?? string.Empty, out var orgSource) ? orgSource : null;
                    var rowsAffected = await connection.ExecuteAsync(
                        "INSERT OR IGNORE INTO organizations (external_id, code, name, source) VALUES (@ExternalId, @Code, @Name, @Source)",
                        new { org.ExternalId, org.Code, org.Name, Source = source });
                    result.OrganizationsImported += rowsAffected;
                }

                // Insert locations
                foreach (var loc in context.Locations)
                {
                    var source = context.LocationSources.TryGetValue(loc.ExternalId ?? string.Empty, out var locSource) ? locSource : null;
                    var rowsAffected = await connection.ExecuteAsync(
                        "INSERT OR IGNORE INTO locations (external_id, code, name, source) VALUES (@ExternalId, @Code, @Name, @Source)",
                        new { loc.ExternalId, loc.Code, loc.Name, Source = source });
                    result.LocationsImported += rowsAffected;
                }

                // Insert employers
                Debug.WriteLine($"[Import] Normal Import - About to insert {context.Employers.Count} employers:");
                Debug.WriteLine($"[Import] employerSources contains {context.EmployerSources.Count} entries:");
                foreach (var kvp in context.EmployerSources)
                {
                    Debug.WriteLine($"[Import]   Key: '{kvp.Key}' -> Source: {kvp.Value}");
                }

                foreach (var emp in context.Employers)
                {
                    // Find the correct source for this employer using the name-to-source mapping
                    var employerNameKey = $"{emp.ExternalId ?? string.Empty}|{emp.Name ?? string.Empty}";
                    context.EmployerNameToSourceMap.TryGetValue(employerNameKey, out var source);

                    Debug.WriteLine($"[Import] Normal Import - Inserting employer: {emp.ExternalId} ({emp.Code}) {emp.Name} - Source: {source}");
                    var rowsAffected = await connection.ExecuteAsync(
                        "INSERT OR IGNORE INTO employers (external_id, code, name, source) VALUES (@ExternalId, @Code, @Name, @Source)",
                        new { emp.ExternalId, emp.Code, emp.Name, Source = source });
                    result.EmployersImported += rowsAffected;
                    Debug.WriteLine($"[Import] Normal Import - Employer insert result: {rowsAffected} rows affected");
                }
                Debug.WriteLine($"[Import] Normal Import - Total employers imported: {result.EmployersImported}");

                // Insert cost centers
                foreach (var cc in context.CostCenters)
                {
                    var source = context.CostCenterSources.TryGetValue(cc.ExternalId ?? string.Empty, out var ccSource) ? ccSource : null;
                    var rowsAffected = await connection.ExecuteAsync(
                        "INSERT OR IGNORE INTO cost_centers (external_id, code, name, source) VALUES (@ExternalId, @Code, @Name, @Source)",
                        new { cc.ExternalId, cc.Code, cc.Name, Source = source });
                    result.CostCentersImported += rowsAffected;
                }

                // Insert cost bearers
                foreach (var cb in context.CostBearers)
                {
                    var source = context.CostBearerSources.TryGetValue(cb.ExternalId ?? string.Empty, out var cbSource) ? cbSource : null;
                    var rowsAffected = await connection.ExecuteAsync(
                        "INSERT OR IGNORE INTO cost_bearers (external_id, code, name, source) VALUES (@ExternalId, @Code, @Name, @Source)",
                        new { cb.ExternalId, cb.Code, cb.Name, Source = source });
                    result.CostBearersImported += rowsAffected;
                }

                // Insert teams
                foreach (var team in context.Teams)
                {
                    var source = context.TeamSources.TryGetValue(team.ExternalId ?? string.Empty, out var teamSource) ? teamSource : null;
                    var rowsAffected = await connection.ExecuteAsync(
                        "INSERT OR IGNORE INTO teams (external_id, code, name, source) VALUES (@ExternalId, @Code, @Name, @Source)",
                        new { team.ExternalId, team.Code, team.Name, Source = source });
                    result.TeamsImported += rowsAffected;
                }

                // Insert divisions
                foreach (var div in context.Divisions)
                {
                    var source = context.DivisionSources.TryGetValue(div.ExternalId ?? string.Empty, out var divSource) ? divSource : null;
                    var rowsAffected = await connection.ExecuteAsync(
                        "INSERT OR IGNORE INTO divisions (external_id, code, name, source) VALUES (@ExternalId, @Code, @Name, @Source)",
                        new { div.ExternalId, div.Code, div.Name, Source = source });
                    result.DivisionsImported += rowsAffected;
                }

                // Insert titles
                foreach (var title in context.Titles)
                {
                    var source = context.TitleSources.TryGetValue(title.ExternalId ?? string.Empty, out var titleSource) ? titleSource : null;
                    var rowsAffected = await connection.ExecuteAsync(
                        "INSERT OR IGNORE INTO titles (external_id, code, name, source) VALUES (@ExternalId, @Code, @Name, @Source)",
                        new { title.ExternalId, title.Code, title.Name, Source = source });
                    result.TitlesImported += rowsAffected;
                }
            }

            // Step 4: Import persons (no FK dependencies)
            progress?.Report(new ImportProgress { CurrentOperation = "Importing persons..." });

            // Detect duplicate person external_ids in vault data
            var personExternalIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var duplicatePersons = new List<string>();
            foreach (var vaultPerson in vaultData.Persons)
            {
                if (string.IsNullOrWhiteSpace(vaultPerson.PersonId))
                {
                    continue;
                }
                if (personExternalIds.Contains(vaultPerson.PersonId))
                {
                    duplicatePersons.Add(vaultPerson.PersonId);
                }
                else
                {
                    personExternalIds.Add(vaultPerson.PersonId);
                }
            }

            if (duplicatePersons.Count > 0)
            {
                Debug.WriteLine($"[Import] Found {duplicatePersons.Count} duplicate person external_ids:");
                foreach (var dup in duplicatePersons.Distinct().Take(10))
                {
                    Debug.WriteLine($"  - {dup}");
                }
                Debug.WriteLine($"[Import] WARNING: {duplicatePersons.Count} duplicate persons will be skipped");
            }

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

                            // Skip duplicates within vault data
                            if (string.IsNullOrWhiteSpace(vaultPerson.PersonId) || !personExternalIds.Contains(vaultPerson.PersonId))
                            {
                                continue;
                            }
                            personExternalIds.Remove(vaultPerson.PersonId); // Mark as processed

                            // Log original vault person for debugging
                            Debug.WriteLine($"[Import] Processing vault person: {vaultPerson.PersonId} - {vaultPerson.DisplayName}");

                            // Map and insert person
                            var person = MapPerson(vaultPerson, sourceLookup, primaryManagerLogic);

                            // Log mapped person for debugging
                            Debug.WriteLine($"[Import] Mapped person: {person.ExternalId} - {person.DisplayName} (Source: {person.Source})");

                            await _personRepository.InsertAsync(person, connection, transaction);

                            Debug.WriteLine($"[Import] Person inserted: {person.ExternalId} - {person.DisplayName}");
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
                sortedDepartments = TopologicalSortDepartments(context.Departments.ToList());
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

                                // Create contract mapping context
                                var contractContext = new ContractMappingContext
                                {
                                    SourceLookup = sourceLookup,
                                    Result = result,
                                    SeenLocations = context.SeenLocations,
                                    SeenEmployers = context.SeenEmployers,
                                    SeenCostCenters = context.SeenCostCenters,
                                    SeenCostBearers = context.SeenCostBearers,
                                    SeenTeams = context.SeenTeams,
                                    SeenDivisions = context.SeenDivisions,
                                    SeenTitles = context.SeenTitles,
                                    SeenOrganizations = context.SeenOrganizations
                                };

                                var contract = MapContract(vaultPerson.PersonId, vaultContract, contractContext);

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
        System.Diagnostics.Debug.WriteLine($"  Persons imported: {result.PersonsImported}");
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
                await _databaseManager.CreateDatabaseBackupAsync();

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

            // Collect sources from contracts (needed for departments referenced in contracts)
            if (vaultData.Persons != null)
            {
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

            var context = ReferenceDataCollector.Collect(vaultData, sourceLookup);

            // Step 4: Import company data tables
            progress?.Report(new ImportProgress { CurrentOperation = "Importing company data..." });

            using (var connection = _connectionFactory.CreateConnection(enforceForeignKeys: false))
            {
                // Insert organizations
                foreach (var org in context.Organizations)
                {
                    var source = context.OrganizationSources.TryGetValue(org.ExternalId ?? string.Empty, out var orgSource) ? orgSource : null;
                    var rowsAffected = await connection.ExecuteAsync(
                        "INSERT OR IGNORE INTO organizations (external_id, code, name, source) VALUES (@ExternalId, @Code, @Name, @Source)",
                        new { org.ExternalId, org.Code, org.Name, Source = source });
                    result.OrganizationsImported += rowsAffected;
                }

                // Insert locations
                foreach (var loc in context.Locations)
                {
                    var source = context.LocationSources.TryGetValue(loc.ExternalId ?? string.Empty, out var locSource) ? locSource : null;
                    var rowsAffected = await connection.ExecuteAsync(
                        "INSERT OR IGNORE INTO locations (external_id, code, name, source) VALUES (@ExternalId, @Code, @Name, @Source)",
                        new { loc.ExternalId, loc.Code, loc.Name, Source = source });
                    result.LocationsImported += rowsAffected;
                }

                // Insert employers
                Debug.WriteLine($"[Import CompanyOnly] About to insert {context.Employers.Count} employers:");
                foreach (var emp in context.Employers)
                {
                    var source = context.EmployerSources.TryGetValue(emp.ExternalId ?? string.Empty, out var empSource) ? empSource : null;
                    Debug.WriteLine($"[Import CompanyOnly] Inserting employer: {emp.ExternalId} ({emp.Code}) {emp.Name} - Source: {source}");
                    var rowsAffected = await connection.ExecuteAsync(
                        "INSERT OR IGNORE INTO employers (external_id, code, name, source) VALUES (@ExternalId, @Code, @Name, @Source)",
                        new { emp.ExternalId, emp.Code, emp.Name, Source = source });
                    result.EmployersImported += rowsAffected;
                    Debug.WriteLine($"[Import CompanyOnly] Employer insert result: {rowsAffected} rows affected");
                }
                Debug.WriteLine($"[Import CompanyOnly] Total employers imported: {result.EmployersImported}");

                // Insert cost centers
                foreach (var cc in context.CostCenters)
                {
                    var source = context.CostCenterSources.TryGetValue(cc.ExternalId ?? string.Empty, out var ccSource) ? ccSource : null;
                    var rowsAffected = await connection.ExecuteAsync(
                        "INSERT OR IGNORE INTO cost_centers (external_id, code, name, source) VALUES (@ExternalId, @Code, @Name, @Source)",
                        new { cc.ExternalId, cc.Code, cc.Name, Source = source });
                    result.CostCentersImported += rowsAffected;
                }

                // Insert cost bearers
                foreach (var cb in context.CostBearers)
                {
                    var source = context.CostBearerSources.TryGetValue(cb.ExternalId ?? string.Empty, out var cbSource) ? cbSource : null;
                    var rowsAffected = await connection.ExecuteAsync(
                        "INSERT OR IGNORE INTO cost_bearers (external_id, code, name, source) VALUES (@ExternalId, @Code, @Name, @Source)",
                        new { cb.ExternalId, cb.Code, cb.Name, Source = source });
                    result.CostBearersImported += rowsAffected;
                }

                // Insert teams
                foreach (var team in context.Teams)
                {
                    var source = context.TeamSources.TryGetValue(team.ExternalId ?? string.Empty, out var teamSource) ? teamSource : null;
                    var rowsAffected = await connection.ExecuteAsync(
                        "INSERT OR IGNORE INTO teams (external_id, code, name, source) VALUES (@ExternalId, @Code, @Name, @Source)",
                        new { team.ExternalId, team.Code, team.Name, Source = source });
                    result.TeamsImported += rowsAffected;
                }

                // Insert divisions
                foreach (var div in context.Divisions)
                {
                    var source = context.DivisionSources.TryGetValue(div.ExternalId ?? string.Empty, out var divSource) ? divSource : null;
                    var rowsAffected = await connection.ExecuteAsync(
                        "INSERT OR IGNORE INTO divisions (external_id, code, name, source) VALUES (@ExternalId, @Code, @Name, @Source)",
                        new { div.ExternalId, div.Code, div.Name, Source = source });
                    result.DivisionsImported += rowsAffected;
                }

                // Insert titles
                foreach (var title in context.Titles)
                {
                    var source = context.TitleSources.TryGetValue(title.ExternalId ?? string.Empty, out var titleSource) ? titleSource : null;
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
                sortedDepartments = TopologicalSortDepartments(context.Departments.ToList());
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
        return PersonMapper.Map(vaultPerson, sourceLookup, primaryManagerLogic);
    }

    /// <summary>
    /// Resolves the external_id for a reference entity by looking up the transformed GUID from seenDictionary.
    /// Handles cases where entities without external_id get a generated GUID during collection.
    /// </summary>
    private string? ResolveReferenceExternalId(VaultReference? reference, string? contractSource,
        Dictionary<string, VaultReference> seenDictionary)
    {
        return ReferenceResolver.ResolveReferenceExternalId(reference, contractSource, seenDictionary);
    }

    private Contract MapContract(string personId, VaultContract vaultContract, ContractMappingContext context)
    {
        // Get source from lookup, or null if not found
        string? sourceId = null;
        if (vaultContract.Source?.SystemId != null && context.SourceLookup.TryGetValue(vaultContract.Source.SystemId, out var mappedSourceId))
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

        // Resolve manager reference
        var managerPersonId = ResolveManagerReference(vaultContract.Manager?.PersonId, context, vaultContract.ExternalId, personId);

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
            LocationExternalId = ResolveReferenceExternalId(vaultContract.Location, sourceId, context.SeenLocations),
            LocationSource = sourceId,
            DepartmentExternalId = !string.IsNullOrWhiteSpace(vaultContract.Department?.ExternalId) && sourceId != null ?
                vaultContract.Department.ExternalId : null,
            DepartmentSource = sourceId,
            CostCenterExternalId = ResolveReferenceExternalId(vaultContract.CostCenter, sourceId, context.SeenCostCenters),
            CostCenterSource = sourceId,
            CostBearerExternalId = ResolveReferenceExternalId(vaultContract.CostBearer, sourceId, context.SeenCostBearers),
            CostBearerSource = sourceId,
            EmployerExternalId = ResolveReferenceExternalId(vaultContract.Employer, sourceId, context.SeenEmployers),
            EmployerSource = sourceId,
            TitleExternalId = ResolveReferenceExternalId(vaultContract.Title, sourceId, context.SeenTitles),
            TitleSource = sourceId,
            TeamExternalId = ResolveReferenceExternalId(vaultContract.Team, sourceId, context.SeenTeams),
            TeamSource = sourceId,
            DivisionExternalId = ResolveReferenceExternalId(vaultContract.Division, sourceId, context.SeenDivisions),
            DivisionSource = sourceId,
            OrganizationExternalId = ResolveReferenceExternalId(vaultContract.Organization, sourceId, context.SeenOrganizations),
            OrganizationSource = sourceId,
            ManagerPersonExternalId = managerPersonId,
            Source = sourceId
        };
    }

    /// <summary>
    /// Resolves the manager reference for a contract, handling empty GUIDs.
    /// </summary>
    private string? ResolveManagerReference(string? managerPersonId, ContractMappingContext context, string contractExternalId, string personId)
    {
        // Check if manager GUID is empty/blank - count and replace with null
        if (managerPersonId == "00000000-0000-0000-0000-000000000000" || string.IsNullOrWhiteSpace(managerPersonId))
        {
            context.Result.EmptyManagerGuidsReplaced++;
            System.Diagnostics.Debug.WriteLine($"[MapContract] Empty manager GUID replaced for contract {contractExternalId} (person {personId})");
            return null;
        }

        return managerPersonId;
    }

    private Contact MapContact(string personId, string type, VaultContactInfo contactInfo)
    {
        return ContactMapper.Map(personId, type, contactInfo);
    }

    /// <summary>
    /// Checks if a contact contains any actual data.
    /// Returns true if ALL fields are null or empty (contact should be skipped).
    /// Returns false if at least ONE field has data (contact should be inserted).
    /// </summary>
    private bool IsEmptyContact(VaultContactInfo contactInfo)
    {
        return ContactMapper.IsEmpty(contactInfo);
    }

    /// <summary>
    /// Validates contract references using source-aware lookups.
    /// Detects orphaned references (contract references entity that doesn't exist in master table with matching source).
    /// </summary>
    private async Task ValidateContractReferencesAsync(ImportResult result)
    {
        await ContractReferenceValidator.ValidateAsync(_connectionFactory, result);
    }

    /// <summary>
    /// Performs topological sort on departments to ensure parents are inserted before children.
    /// Uses depth-first traversal with cycle detection.
    /// </summary>
    private List<Department> TopologicalSortDepartments(List<Department> departments)
    {
        return DepartmentSorter.TopologicalSort(departments);
    }

    private string DetectDataType(HashSet<object> sampleValues)
    {
        // Always return "text" as data type for all custom fields
        // Per user requirement: no auto-detection, all fields should be text type
        return "text";
    }

    private string FormatDisplayName(string fieldKey)
    {
        return StringFormatter.FormatDisplayName(fieldKey);
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
