using System.Data;
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
using HelloID.Vault.Services.Import.Detection;
using HelloID.Vault.Services.Import.Mappers;
using HelloID.Vault.Services.Import.Models;
using HelloID.Vault.Services.Import.Strategies;
using HelloID.Vault.Services.Import.Utilities;
using HelloID.Vault.Services.Import.Validators;
using HelloID.Vault.Services.Interfaces;
using Microsoft.Data.Sqlite;
using Npgsql;

namespace HelloID.Vault.Services;


/// <summary>
/// Service implementation for importing vault.json files.
/// </summary>
public class VaultImportService : IVaultImportService
{
    private readonly IDatabaseConnectionFactory _connectionFactory;
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
    private readonly PrimaryManagerDetector _primaryManagerDetector;
    private readonly ITursoClient? _tursoClient;
    private readonly string? _sqliteSchemaPath;

    public VaultImportService(
        IDatabaseConnectionFactory connectionFactory,
        DatabaseInitializer databaseInitializer,
        IDatabaseManager databaseManager,
        IPersonRepository personRepository,
        IContractRepository contractRepository,
        IContactRepository contactRepository,
        IDepartmentRepository departmentRepository,
        ICustomFieldRepository customFieldRepository,
        IPrimaryManagerService primaryManagerService,
        IUserPreferencesService userPreferencesService,
        IReferenceDataService referenceDataService,
        ITursoClient? tursoClient = null,
        string? sqliteSchemaPath = null)
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
        _tursoClient = tursoClient;
        _sqliteSchemaPath = sqliteSchemaPath;

        _primaryManagerDetector = new PrimaryManagerDetector(
            connectionFactory,
            primaryManagerService,
            userPreferencesService);
    }

    public DatabaseType DatabaseType => _connectionFactory.DatabaseType;

    public async Task<bool> HasDataAsync()
    {
        // For Turso, use the TursoClient to check for data
        if (_connectionFactory.DatabaseType == DatabaseType.Turso && _tursoClient != null)
        {
            try
            {
                // Check if schema is initialized (persons table exists)
                var schemaExists = await _tursoClient.IsSchemaInitializedAsync();
                if (!schemaExists)
                {
                    return false;
                }

                // Check if persons table has data
                var count = await _tursoClient.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM persons");
                return count > 0;
            }
            catch
            {
                // If we can't check, assume no data
                return false;
            }
        }

        return await _databaseManager.HasDataAsync();
    }

    public async Task DeleteDatabaseAsync()
    {
        await _databaseManager.DeleteDatabaseAsync();
    }

    public async Task<ImportResult> ImportAsync(string filePath, PrimaryManagerLogic primaryManagerLogic, bool createBackup = false, IProgress<ImportProgress>? progress = null)
    {
        // Turso requires special handling via temp SQLite + HTTP upload
        if (_connectionFactory.DatabaseType == DatabaseType.Turso)
        {
            return await ImportToTursoAsync(filePath, primaryManagerLogic, progress);
        }

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
                    var sql = GetInsertIgnoreSql("source_system", "system_id, display_name, identification_key", "@SystemId, @DisplayName, @IdentificationKey");
                    var rowsAffected = await connection.ExecuteAsync(sql,
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
                    var sql = GetInsertIgnoreSql("organizations", "external_id, code, name, source", "@ExternalId, @Code, @Name, @Source");
                    var rowsAffected = await connection.ExecuteAsync(sql,
                        new { org.ExternalId, org.Code, org.Name, Source = source });
                    result.OrganizationsImported += rowsAffected;
                }

                // Insert locations
                foreach (var loc in context.Locations)
                {
                    var source = context.LocationSources.TryGetValue(loc.ExternalId ?? string.Empty, out var locSource) ? locSource : null;
                    var sql = GetInsertIgnoreSql("locations", "external_id, code, name, source", "@ExternalId, @Code, @Name, @Source");
                    var rowsAffected = await connection.ExecuteAsync(sql,
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
                    var sql = GetInsertIgnoreSql("employers", "external_id, code, name, source", "@ExternalId, @Code, @Name, @Source");
                    var rowsAffected = await connection.ExecuteAsync(sql,
                        new { emp.ExternalId, emp.Code, emp.Name, Source = source });
                    result.EmployersImported += rowsAffected;
                    Debug.WriteLine($"[Import] Normal Import - Employer insert result: {rowsAffected} rows affected");
                }
                Debug.WriteLine($"[Import] Normal Import - Total employers imported: {result.EmployersImported}");

                // Insert cost centers
                foreach (var cc in context.CostCenters)
                {
                    var source = context.CostCenterSources.TryGetValue(cc.ExternalId ?? string.Empty, out var ccSource) ? ccSource : null;
                    var sql = GetInsertIgnoreSql("cost_centers", "external_id, code, name, source", "@ExternalId, @Code, @Name, @Source");
                    var rowsAffected = await connection.ExecuteAsync(sql,
                        new { cc.ExternalId, cc.Code, cc.Name, Source = source });
                    result.CostCentersImported += rowsAffected;
                }

                // Insert cost bearers
                foreach (var cb in context.CostBearers)
                {
                    var source = context.CostBearerSources.TryGetValue(cb.ExternalId ?? string.Empty, out var cbSource) ? cbSource : null;
                    var sql = GetInsertIgnoreSql("cost_bearers", "external_id, code, name, source", "@ExternalId, @Code, @Name, @Source");
                    var rowsAffected = await connection.ExecuteAsync(sql,
                        new { cb.ExternalId, cb.Code, cb.Name, Source = source });
                    result.CostBearersImported += rowsAffected;
                }

                // Insert teams
                foreach (var team in context.Teams)
                {
                    var source = context.TeamSources.TryGetValue(team.ExternalId ?? string.Empty, out var teamSource) ? teamSource : null;
                    var sql = GetInsertIgnoreSql("teams", "external_id, code, name, source", "@ExternalId, @Code, @Name, @Source");
                    var rowsAffected = await connection.ExecuteAsync(sql,
                        new { team.ExternalId, team.Code, team.Name, Source = source });
                    result.TeamsImported += rowsAffected;
                }

                // Insert divisions
                foreach (var div in context.Divisions)
                {
                    var source = context.DivisionSources.TryGetValue(div.ExternalId ?? string.Empty, out var divSource) ? divSource : null;
                    var sql = GetInsertIgnoreSql("divisions", "external_id, code, name, source", "@ExternalId, @Code, @Name, @Source");
                    var rowsAffected = await connection.ExecuteAsync(sql,
                        new { div.ExternalId, div.Code, div.Name, Source = source });
                    result.DivisionsImported += rowsAffected;
                }

                // Insert titles
                foreach (var title in context.Titles)
                {
                    var source = context.TitleSources.TryGetValue(title.ExternalId ?? string.Empty, out var titleSource) ? titleSource : null;
                    var sql = GetInsertIgnoreSql("titles", "external_id, code, name, source", "@ExternalId, @Code, @Name, @Source");
                    var rowsAffected = await connection.ExecuteAsync(sql,
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

            // Create a separate set for manager validation (personExternalIds is modified during import loop)
            var validPersonIds = new HashSet<string>(personExternalIds, StringComparer.OrdinalIgnoreCase);

            // Import persons using strategy pattern
            using (var connection = _connectionFactory.CreateConnection(enforceForeignKeys: false))
            {
                var strategy = await ImportStrategyFactory.CreateAsync(_connectionFactory, connection);
                Debug.WriteLine($"[Import] Using strategy for persons: {strategy.StrategyName}");

                await strategy.PrepareForImportAsync(connection);

                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // Map persons to entities
                        var persons = new List<Person>();
                        foreach (var vaultPerson in vaultData.Persons)
                        {
                            if (string.IsNullOrWhiteSpace(vaultPerson.PersonId) || !personExternalIds.Contains(vaultPerson.PersonId))
                            {
                                continue;
                            }
                            personExternalIds.Remove(vaultPerson.PersonId);
                            persons.Add(MapPerson(vaultPerson, sourceLookup, primaryManagerLogic));
                        }

                        result.PersonsImported = await strategy.ImportPersonsAsync(
                            persons,
                            validPersonIds,
                            _personRepository,
                            connection,
                            transaction,
                            (processed, total) => progress?.Report(new ImportProgress
                            {
                                CurrentOperation = $"Importing persons... ({processed}/{total})",
                                TotalItems = total,
                                ProcessedItems = processed
                            }));

                        // Track invalid manager references from strategy
                        result.InvalidManagerReferences += strategy.InvalidManagerReferences;

                        transaction.Commit();
                    }
                    catch (Exception ex)
                    {
                        try
                        {
                            if (transaction.Connection != null)
                            {
                                transaction.Rollback();
                            }
                        }
                        catch (Exception rollbackEx)
                        {
                            Debug.WriteLine($"[Import] Warning: Could not rollback transaction: {rollbackEx.Message}");
                        }
                        throw;
                    }
                }

                await strategy.CleanupAfterImportAsync(connection);
            }

            // Step 5: Import departments (depends on persons for manager_person_id)
            progress?.Report(new ImportProgress { CurrentOperation = "Importing departments..." });

            // Build set of valid department keys (external_id + source) for parent validation
            var validDepartmentKeys = new HashSet<string>(
                context.Departments.Select(d => $"{d.ExternalId}|{d.Source}"),
                StringComparer.OrdinalIgnoreCase);

            // Import departments using strategy pattern
            using (var connection = _connectionFactory.CreateConnection(enforceForeignKeys: false))
            {
                // Create appropriate strategy based on database type and capabilities
                var strategy = await ImportStrategyFactory.CreateAsync(_connectionFactory, connection);
                Debug.WriteLine($"[Import] Using strategy: {strategy.StrategyName}");

                // Prepare connection for import (disable FK constraints if possible)
                await strategy.PrepareForImportAsync(connection);

                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        result.DepartmentsImported = await strategy.ImportDepartmentsAsync(
                            context.Departments,
                            validDepartmentKeys,
                            _departmentRepository,
                            connection,
                            transaction,
                            count => progress?.Report(new ImportProgress
                            {
                                CurrentOperation = $"Importing departments... ({count}/{context.Departments.Count()})"
                            }));

                        // Track invalid references from strategy
                        result.InvalidDepartmentParents = strategy.InvalidDepartmentParents;
                        result.InvalidManagerReferences = strategy.InvalidManagerReferences;

                        progress?.Report(new ImportProgress { CurrentOperation = "Validating department data..." });

                        transaction.Commit();
                    }
                    catch (Exception ex)
                    {
                        // Safe rollback - check if transaction is still active
                        try
                        {
                            if (transaction.Connection != null)
                            {
                                transaction.Rollback();
                            }
                        }
                        catch (Exception rollbackEx)
                        {
                            Debug.WriteLine($"[Import] Warning: Could not rollback transaction: {rollbackEx.Message}");
                        }
                        throw new Exception($"Department import failed and was rolled back. Error: {ex.Message}", ex);
                    }
                }

                // Cleanup after import (re-enable FK constraints)
                await strategy.CleanupAfterImportAsync(connection);
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

            // Check which departments exist in database (both external_id AND source for FK constraint)
            using (var connection = _connectionFactory.CreateConnection(enforceForeignKeys: false))
            {
                var existingDeptKeys = (await connection.QueryAsync<(string ExternalId, string? Source)>(
                    "SELECT external_id, source FROM departments"))
                    .Select(d => $"{d.ExternalId}|{d.Source ?? ""}")
                    .ToHashSet();

                // Find missing departments (check both external_id AND source)
                var missingDepts = contractDepartmentRefs
                    .Where(kvp => !existingDeptKeys.Contains($"{kvp.Key}|{kvp.Value.Source}"))
                    .Select(kvp => (ExternalId: kvp.Key, DisplayName: kvp.Value.DisplayName, Source: kvp.Value.Source))
                    .ToList();

                if (missingDepts.Any())
                {
                    Console.WriteLine($"WARNING: Found {missingDepts.Count} department(s) referenced by contracts but missing from root Departments array.");
                    Console.WriteLine("         Auto-creating departments with minimal data (Code, ParentExternalId, Manager will be NULL):");

                    var strategy = await ImportStrategyFactory.CreateAsync(_connectionFactory, connection);
                    await strategy.PrepareForImportAsync(connection);

                    using (var transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            foreach (var (externalId, displayName, source) in missingDepts)
                            {
                                Console.WriteLine($"  - {displayName} ({externalId})");

                                var missingDept = new Department
                                {
                                    ExternalId = externalId,
                                    DisplayName = displayName,
                                    Code = null,
                                    ParentExternalId = null,
                                    ManagerPersonId = null,
                                    Source = source
                                };

                                await _departmentRepository.InsertAsync(missingDept, connection, transaction);
                                result.DepartmentsAutoCreated++;
                            }

                            transaction.Commit();
                        }
                        catch
                        {
                            try
                            {
                                if (transaction.Connection != null)
                                {
                                    transaction.Rollback();
                                }
                            }
                            catch (Exception rollbackEx)
                            {
                                Debug.WriteLine($"[Import] Warning: Could not rollback transaction: {rollbackEx.Message}");
                            }
                            throw;
                        }
                    }

                    await strategy.CleanupAfterImportAsync(connection);
                }
            }

            // Step 6: Import contacts (depends on persons)
            progress?.Report(new ImportProgress { CurrentOperation = "Importing contacts..." });

            using (var connection = _connectionFactory.CreateConnection(enforceForeignKeys: false))
            {
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
                var strategy = await ImportStrategyFactory.CreateAsync(_connectionFactory, connection);
                await strategy.PrepareForImportAsync(connection);

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

                                var contract = ContractMapper.Map(vaultPerson.PersonId, vaultContract, contractContext);

                                // Validate FK references and set invalid ones to NULL
                                // This is needed for managed PostgreSQL where FK constraints can't be disabled
                                await ValidateContractFkReferencesAsync(contract, connection, transaction);

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
                    catch
                    {
                        try
                        {
                            if (transaction.Connection != null)
                            {
                                transaction.Rollback();
                            }
                        }
                        catch (Exception rollbackEx)
                        {
                            Debug.WriteLine($"[Import] Warning: Could not rollback transaction: {rollbackEx.Message}");
                        }
                        throw;
                    }
                }

                await strategy.CleanupAfterImportAsync(connection);
            }

            // Step 7.5: Validate contract references (Phase 2: Source-aware validation)
            progress?.Report(new ImportProgress { CurrentOperation = "Validating contract references..." });
            try
            {
                await ValidateContractReferencesAsync(result);
                Debug.WriteLine($"[Import] Contract references validation completed successfully.");
            }
            catch (Exception ex)
            {
                ImportErrorLogger.LogDatabaseException(ex, "ValidateContractReferencesAsync", "Step 7.5 of import process");
                
                result.Success = false;
                result.ErrorMessage = ImportErrorLogger.CreateUserErrorMessage(ex, "Validating contract references");
                
                Debug.WriteLine($"[Import] Failed to validate contract references. Import will be rolled back if possible.");
                throw new Exception($"Contract reference validation failed: {ImportErrorLogger.CreateUserErrorMessage(ex, "Validation")}", ex);
            }

            // Step 7.6: Calculate Primary Managers for ContractBased/DepartmentBased logic
            if (primaryManagerLogic == PrimaryManagerLogic.ContractBased || primaryManagerLogic == PrimaryManagerLogic.DepartmentBased)
            {
                progress?.Report(new ImportProgress { CurrentOperation = $"Calculating Primary Managers ({primaryManagerLogic})..." });
                try
                {
                    int updatedCount = await _primaryManagerService.RefreshAllPrimaryManagersAsync(primaryManagerLogic);
                    Console.WriteLine($"Updated primary managers for {updatedCount} persons using {primaryManagerLogic} logic.");
                    Debug.WriteLine($"[Import] Successfully updated primary managers for {updatedCount} persons using {primaryManagerLogic} logic.");
                }
                catch (Exception ex)
                {
                    ImportErrorLogger.LogDatabaseException(ex, $"RefreshAllPrimaryManagersAsync({primaryManagerLogic})", $"Step 7.6 of import process");
                    
                    result.Success = false;
                    result.ErrorMessage = ImportErrorLogger.CreateUserErrorMessage(ex, $"Calculating Primary Managers ({primaryManagerLogic})");
                    
                    Debug.WriteLine($"[Import] Failed to calculate primary managers. Import will be rolled back if possible.");
                    throw new Exception($"Primary Manager calculation failed: {ImportErrorLogger.CreateUserErrorMessage(ex, $"Primary Manager ({primaryManagerLogic})")}", ex);
                }
            }
            // Step 7.7: Auto-detect Primary Manager logic from imported data (From JSON)
            else if (primaryManagerLogic == PrimaryManagerLogic.FromJson)
            {
                progress?.Report(new ImportProgress { CurrentOperation = "Detecting Primary Manager logic from imported data..." });
                try
                {
                    var detectedLogic = await _primaryManagerDetector.DetectAsync();
                    if (detectedLogic != null)
                    {
                        Console.WriteLine($"Auto-detected Primary Manager logic: {detectedLogic}");
                        Debug.WriteLine($"[Import] Auto-detected Primary Manager logic: {detectedLogic}");
                    }
                }
                catch (Exception ex)
                {
                    ImportErrorLogger.LogDatabaseException(ex, "DetectPrimaryManagerLogicAsync", "Step 7.7 of import process");
                    
                    result.Success = false;
                    result.ErrorMessage = ImportErrorLogger.CreateUserErrorMessage(ex, "Detecting Primary Manager logic from imported data");
                    
                    Debug.WriteLine($"[Import] Failed to detect primary manager logic. This is not a critical error, import will continue.");
                    
                    Console.WriteLine($"WARNING: Could not auto-detect Primary Manager logic: {ex.Message}");
                }
            }

            // Step 8: Collect and create custom field schemas
            progress?.Report(new ImportProgress { CurrentOperation = "Creating custom field schemas..." });
            Debug.WriteLine($"[Import] Step 8: Starting custom field schema creation...");

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

            Debug.WriteLine($"[Import] Found {customFieldSchemas.Count} unique custom field schemas");

            // Insert custom field schemas
            using (var connection = _connectionFactory.CreateConnection(enforceForeignKeys: false))
            {
                int sortOrder = 1;
                int schemaCount = 0;
                foreach (var ((tableName, fieldKey), (_, sampleValues)) in customFieldSchemas)
                {
                    try
                    {
                        var dataType = DetectDataType(sampleValues);
                        var displayName = FormatDisplayName(fieldKey);

                        var sql = GetInsertIgnoreSql("custom_field_schemas", "table_name, field_key, display_name, sort_order", "@TableName, @FieldKey, @DisplayName, @SortOrder");
                        var rowsAffected = await connection.ExecuteAsync(sql,
                            new
                            {
                                TableName = tableName,
                                FieldKey = fieldKey,
                                DisplayName = displayName,
                                SortOrder = sortOrder++
                            });

                        schemaCount++;
                        Debug.WriteLine($"[Import] Custom field schema #{schemaCount}: {tableName}.{fieldKey} ({rowsAffected} rows affected)");

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
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[Import] ERROR inserting custom field schema {tableName}.{fieldKey}: {ex.Message}");
                        Debug.WriteLine($"[Import] Exception Type: {ex.GetType().Name}");
                        throw;
                    }
                }
                Debug.WriteLine($"[Import] Custom field schemas inserted: {schemaCount} total");
            }

            // Step 9: Import custom field values (depends on persons.external_id and contracts.external_id)
            progress?.Report(new ImportProgress { CurrentOperation = "Importing custom field values..." });
            Debug.WriteLine($"[Import] Step 9: Starting custom field value import...");

            int personCustomFields = 0;
            int contractCustomFields = 0;
            int cfPersonCount = 0;
            int cfTotalPersons = vaultData.Persons.Count();

            // Batch custom field imports - collect all fields per entity, then do ONE update per entity
            using (var connection = _connectionFactory.CreateConnection(enforceForeignKeys: false))
            {
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        foreach (var vaultPerson in vaultData.Persons)
                        {
                            cfPersonCount++;

                            // Person custom fields - batch update with entire JSON
                            if (vaultPerson.Custom != null && vaultPerson.Custom.Count > 0 && !string.IsNullOrWhiteSpace(vaultPerson.ExternalId))
                            {
                                var customFieldsJson = System.Text.Json.JsonSerializer.Serialize(vaultPerson.Custom);
                                var sql = _connectionFactory.DatabaseType == DatabaseType.PostgreSql
                                    ? "UPDATE persons SET custom_fields = @Json::jsonb WHERE external_id = @ExternalId"
                                    : "UPDATE persons SET custom_fields = json(@Json) WHERE external_id = @ExternalId";

                                await connection.ExecuteAsync(sql, new { Json = customFieldsJson, ExternalId = vaultPerson.ExternalId }, transaction);
                                personCustomFields += vaultPerson.Custom.Count;
                            }

                            // Contract custom fields - batch update with entire JSON
                            foreach (var vaultContract in vaultPerson.Contracts)
                            {
                                if (vaultContract.Custom != null && vaultContract.Custom.Count > 0 && !string.IsNullOrWhiteSpace(vaultContract.ExternalId))
                                {
                                    var customFieldsJson = System.Text.Json.JsonSerializer.Serialize(vaultContract.Custom);
                                    var sql = _connectionFactory.DatabaseType == DatabaseType.PostgreSql
                                        ? "UPDATE contracts SET custom_fields = @Json::jsonb WHERE external_id = @ExternalId"
                                        : "UPDATE contracts SET custom_fields = json(@Json) WHERE external_id = @ExternalId";

                                    await connection.ExecuteAsync(sql, new { Json = customFieldsJson, ExternalId = vaultContract.ExternalId }, transaction);
                                    contractCustomFields += vaultContract.Custom.Count;
                                }
                            }

                            // Report progress every 100 persons
                            if (cfPersonCount % 100 == 0)
                            {
                                progress?.Report(new ImportProgress
                                {
                                    CurrentOperation = $"Importing custom fields... ({cfPersonCount}/{cfTotalPersons})",
                                    TotalItems = cfTotalPersons,
                                    ProcessedItems = cfPersonCount
                                });
                            }
                        }

                        transaction.Commit();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[Import] ERROR importing custom fields: {ex.Message}");
                        try
                        {
                            if (transaction.Connection != null)
                            {
                                transaction.Rollback();
                            }
                        }
                        catch (Exception rollbackEx)
                        {
                            Debug.WriteLine($"[Import] Warning: Could not rollback transaction: {rollbackEx.Message}");
                        }
                        throw;
                    }
                }
            }

            Debug.WriteLine($"[Import] Custom field values imported: {personCustomFields} person fields, {contractCustomFields} contract fields");

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
            result.ErrorMessage = ImportErrorLogger.CreateUserErrorMessage(ex, "Import process");
            
            ImportErrorLogger.LogDatabaseException(ex, "ImportAsync - Main import loop");
            
            progress?.Report(new ImportProgress
            {
                CurrentOperation = $"Import failed: {ex.Message}",
                ProcessedItems = result.PersonsImported
            });
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
        System.Diagnostics.Debug.WriteLine($"  Invalid manager references set to NULL: {result.InvalidManagerReferences}");
        if (result.InvalidManagerReferences > 0)
        {
            System.Diagnostics.Debug.WriteLine($"  → These persons had manager references to persons not in import data");
        }
        System.Diagnostics.Debug.WriteLine($"  Departments imported: {result.DepartmentsImported}");
        System.Diagnostics.Debug.WriteLine($"  Invalid department parents set to NULL: {result.InvalidDepartmentParents}");
        if (result.InvalidDepartmentParents > 0)
        {
            System.Diagnostics.Debug.WriteLine($"  → These departments had parent references to departments not in import data");
        }
        System.Diagnostics.Debug.WriteLine($"  Empty manager GUIDs replaced: {result.EmptyManagerGuidsReplaced}");
        if (result.EmptyManagerGuidsReplaced > 0)
        {
            System.Diagnostics.Debug.WriteLine($"  → These contracts will have NULL manager instead of invalid GUID");
        }

        return result;
    }

    public async Task<ImportResult> ImportCompanyOnlyAsync(string filePath, PrimaryManagerLogic primaryManagerLogic, bool createBackup = false, IProgress<ImportProgress>? progress = null)
    {
        // Turso requires special handling via temp SQLite + HTTP upload
        if (_connectionFactory.DatabaseType == DatabaseType.Turso)
        {
            return await ImportCompanyOnlyToTursoAsync(filePath, progress);
        }

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
                    var sql = GetInsertIgnoreSql("source_system", "system_id, display_name, identification_key", "@SystemId, @DisplayName, @IdentificationKey");
                    var rowsAffected = await connection.ExecuteAsync(sql,
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
                    var sql = GetInsertIgnoreSql("organizations", "external_id, code, name, source", "@ExternalId, @Code, @Name, @Source");
                    var rowsAffected = await connection.ExecuteAsync(sql,
                        new { org.ExternalId, org.Code, org.Name, Source = source });
                    result.OrganizationsImported += rowsAffected;
                }

                // Insert locations
                foreach (var loc in context.Locations)
                {
                    var source = context.LocationSources.TryGetValue(loc.ExternalId ?? string.Empty, out var locSource) ? locSource : null;
                    var sql = GetInsertIgnoreSql("locations", "external_id, code, name, source", "@ExternalId, @Code, @Name, @Source");
                    var rowsAffected = await connection.ExecuteAsync(sql,
                        new { loc.ExternalId, loc.Code, loc.Name, Source = source });
                    result.LocationsImported += rowsAffected;
                }

                // Insert employers
                Debug.WriteLine($"[Import CompanyOnly] About to insert {context.Employers.Count} employers:");
                foreach (var emp in context.Employers)
                {
                    var source = context.EmployerSources.TryGetValue(emp.ExternalId ?? string.Empty, out var empSource) ? empSource : null;
                    Debug.WriteLine($"[Import CompanyOnly] Inserting employer: {emp.ExternalId} ({emp.Code}) {emp.Name} - Source: {source}");
                    var sql = GetInsertIgnoreSql("employers", "external_id, code, name, source", "@ExternalId, @Code, @Name, @Source");
                    var rowsAffected = await connection.ExecuteAsync(sql,
                        new { emp.ExternalId, emp.Code, emp.Name, Source = source });
                    result.EmployersImported += rowsAffected;
                    Debug.WriteLine($"[Import CompanyOnly] Employer insert result: {rowsAffected} rows affected");
                }
                Debug.WriteLine($"[Import CompanyOnly] Total employers imported: {result.EmployersImported}");

                // Insert cost centers
                foreach (var cc in context.CostCenters)
                {
                    var source = context.CostCenterSources.TryGetValue(cc.ExternalId ?? string.Empty, out var ccSource) ? ccSource : null;
                    var sql = GetInsertIgnoreSql("cost_centers", "external_id, code, name, source", "@ExternalId, @Code, @Name, @Source");
                    var rowsAffected = await connection.ExecuteAsync(sql,
                        new { cc.ExternalId, cc.Code, cc.Name, Source = source });
                    result.CostCentersImported += rowsAffected;
                }

                // Insert cost bearers
                foreach (var cb in context.CostBearers)
                {
                    var source = context.CostBearerSources.TryGetValue(cb.ExternalId ?? string.Empty, out var cbSource) ? cbSource : null;
                    var sql = GetInsertIgnoreSql("cost_bearers", "external_id, code, name, source", "@ExternalId, @Code, @Name, @Source");
                    var rowsAffected = await connection.ExecuteAsync(sql,
                        new { cb.ExternalId, cb.Code, cb.Name, Source = source });
                    result.CostBearersImported += rowsAffected;
                }

                // Insert teams
                foreach (var team in context.Teams)
                {
                    var source = context.TeamSources.TryGetValue(team.ExternalId ?? string.Empty, out var teamSource) ? teamSource : null;
                    var sql = GetInsertIgnoreSql("teams", "external_id, code, name, source", "@ExternalId, @Code, @Name, @Source");
                    var rowsAffected = await connection.ExecuteAsync(sql,
                        new { team.ExternalId, team.Code, team.Name, Source = source });
                    result.TeamsImported += rowsAffected;
                }

                // Insert divisions
                foreach (var div in context.Divisions)
                {
                    var source = context.DivisionSources.TryGetValue(div.ExternalId ?? string.Empty, out var divSource) ? divSource : null;
                    var sql = GetInsertIgnoreSql("divisions", "external_id, code, name, source", "@ExternalId, @Code, @Name, @Source");
                    var rowsAffected = await connection.ExecuteAsync(sql,
                        new { div.ExternalId, div.Code, div.Name, Source = source });
                    result.DivisionsImported += rowsAffected;
                }

                // Insert titles
                foreach (var title in context.Titles)
                {
                    var source = context.TitleSources.TryGetValue(title.ExternalId ?? string.Empty, out var titleSource) ? titleSource : null;
                    var sql = GetInsertIgnoreSql("titles", "external_id, code, name, source", "@ExternalId, @Code, @Name, @Source");
                    var rowsAffected = await connection.ExecuteAsync(sql,
                        new { title.ExternalId, title.Code, title.Name, Source = source });
                    result.TitlesImported += rowsAffected;
                }
            }

            // Step 5: Import departments (skip persons since we're not importing them)
            progress?.Report(new ImportProgress { CurrentOperation = "Importing departments..." });

            // Build set of valid department keys for parent validation
            var validDepartmentKeys = new HashSet<string>(
                context.Departments.Select(d => $"{d.ExternalId}|{d.Source}"),
                StringComparer.OrdinalIgnoreCase);

            // Import departments using strategy pattern
            using (var connection = _connectionFactory.CreateConnection(enforceForeignKeys: false))
            {
                // Create appropriate strategy based on database type and capabilities
                var strategy = await ImportStrategyFactory.CreateAsync(_connectionFactory, connection);
                Debug.WriteLine($"[ImportCompanyOnly] Using strategy: {strategy.StrategyName}");

                // Prepare connection for import (disable FK constraints if possible)
                await strategy.PrepareForImportAsync(connection);

                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        result.DepartmentsImported = await strategy.ImportDepartmentsAsync(
                            context.Departments,
                            validDepartmentKeys,
                            _departmentRepository,
                            connection,
                            transaction,
                            count => progress?.Report(new ImportProgress
                            {
                                CurrentOperation = $"Importing departments... ({count}/{context.Departments.Count()})"
                            }));

                        // Track invalid references from strategy
                        result.InvalidDepartmentParents = strategy.InvalidDepartmentParents;
                        result.InvalidManagerReferences = strategy.InvalidManagerReferences;

                        progress?.Report(new ImportProgress { CurrentOperation = "Validating department data..." });

                        // Clear all manager references since we're not importing persons
                        await connection.ExecuteAsync(@"
                            UPDATE departments
                            SET manager_person_id = NULL
                            WHERE manager_person_id IS NOT NULL", transaction: transaction);

                        transaction.Commit();
                    }
                    catch (Exception ex)
                    {
                        // Safe rollback - check if transaction is still active
                        try
                        {
                            if (transaction.Connection != null)
                            {
                                transaction.Rollback();
                            }
                        }
                        catch (Exception rollbackEx)
                        {
                            Debug.WriteLine($"[ImportCompanyOnly] Warning: Could not rollback transaction: {rollbackEx.Message}");
                        }
                        throw new Exception($"Department import failed and was rolled back. Error: {ex.Message}", ex);
                    }
                }

                // Cleanup after import (re-enable FK constraints)
                await strategy.CleanupAfterImportAsync(connection);
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
    /// Validates and fixes FK references for a contract before insert.
    /// Sets invalid references to NULL to avoid FK constraint violations.
    /// This is needed for managed PostgreSQL where FK constraints can't be disabled.
    /// </summary>
    private async Task ValidateContractFkReferencesAsync(Contract contract, IDbConnection connection, IDbTransaction transaction)
    {
        // Check title reference
        if (!string.IsNullOrWhiteSpace(contract.TitleExternalId))
        {
            var titleExists = await connection.ExecuteScalarAsync<bool>(
                "SELECT EXISTS(SELECT 1 FROM titles WHERE external_id = @ExternalId AND source = @Source)",
                new { ExternalId = contract.TitleExternalId, Source = contract.TitleSource },
                transaction);

            if (!titleExists)
            {
                Debug.WriteLine($"[Import] Contract {contract.ExternalId} has invalid title reference ({contract.TitleExternalId}, {contract.TitleSource}) - setting to NULL");
                contract.TitleExternalId = null;
                contract.TitleSource = null;
            }
        }

        // Check location reference
        if (!string.IsNullOrWhiteSpace(contract.LocationExternalId))
        {
            var locationExists = await connection.ExecuteScalarAsync<bool>(
                "SELECT EXISTS(SELECT 1 FROM locations WHERE external_id = @ExternalId AND source = @Source)",
                new { ExternalId = contract.LocationExternalId, Source = contract.LocationSource },
                transaction);

            if (!locationExists)
            {
                Debug.WriteLine($"[Import] Contract {contract.ExternalId} has invalid location reference ({contract.LocationExternalId}, {contract.LocationSource}) - setting to NULL");
                contract.LocationExternalId = null;
                contract.LocationSource = null;
            }
        }

        // Check employer reference
        if (!string.IsNullOrWhiteSpace(contract.EmployerExternalId))
        {
            var employerExists = await connection.ExecuteScalarAsync<bool>(
                "SELECT EXISTS(SELECT 1 FROM employers WHERE external_id = @ExternalId AND source = @Source)",
                new { ExternalId = contract.EmployerExternalId, Source = contract.EmployerSource },
                transaction);

            if (!employerExists)
            {
                Debug.WriteLine($"[Import] Contract {contract.ExternalId} has invalid employer reference ({contract.EmployerExternalId}, {contract.EmployerSource}) - setting to NULL");
                contract.EmployerExternalId = null;
                contract.EmployerSource = null;
            }
        }

        // Check department reference
        if (!string.IsNullOrWhiteSpace(contract.DepartmentExternalId))
        {
            var deptExists = await connection.ExecuteScalarAsync<bool>(
                "SELECT EXISTS(SELECT 1 FROM departments WHERE external_id = @ExternalId AND source = @Source)",
                new { ExternalId = contract.DepartmentExternalId, Source = contract.DepartmentSource },
                transaction);

            if (!deptExists)
            {
                Debug.WriteLine($"[Import] Contract {contract.ExternalId} has invalid department reference ({contract.DepartmentExternalId}, {contract.DepartmentSource}) - setting to NULL");
                contract.DepartmentExternalId = null;
                contract.DepartmentSource = null;
            }
        }

        // Check cost center reference
        if (!string.IsNullOrWhiteSpace(contract.CostCenterExternalId))
        {
            var ccExists = await connection.ExecuteScalarAsync<bool>(
                "SELECT EXISTS(SELECT 1 FROM cost_centers WHERE external_id = @ExternalId AND source = @Source)",
                new { ExternalId = contract.CostCenterExternalId, Source = contract.CostCenterSource },
                transaction);

            if (!ccExists)
            {
                Debug.WriteLine($"[Import] Contract {contract.ExternalId} has invalid cost center reference ({contract.CostCenterExternalId}, {contract.CostCenterSource}) - setting to NULL");
                contract.CostCenterExternalId = null;
                contract.CostCenterSource = null;
            }
        }

        // Check cost bearer reference
        if (!string.IsNullOrWhiteSpace(contract.CostBearerExternalId))
        {
            var cbExists = await connection.ExecuteScalarAsync<bool>(
                "SELECT EXISTS(SELECT 1 FROM cost_bearers WHERE external_id = @ExternalId AND source = @Source)",
                new { ExternalId = contract.CostBearerExternalId, Source = contract.CostBearerSource },
                transaction);

            if (!cbExists)
            {
                Debug.WriteLine($"[Import] Contract {contract.ExternalId} has invalid cost bearer reference ({contract.CostBearerExternalId}, {contract.CostBearerSource}) - setting to NULL");
                contract.CostBearerExternalId = null;
                contract.CostBearerSource = null;
            }
        }

        // Check team reference
        if (!string.IsNullOrWhiteSpace(contract.TeamExternalId))
        {
            var teamExists = await connection.ExecuteScalarAsync<bool>(
                "SELECT EXISTS(SELECT 1 FROM teams WHERE external_id = @ExternalId AND source = @Source)",
                new { ExternalId = contract.TeamExternalId, Source = contract.TeamSource },
                transaction);

            if (!teamExists)
            {
                Debug.WriteLine($"[Import] Contract {contract.ExternalId} has invalid team reference ({contract.TeamExternalId}, {contract.TeamSource}) - setting to NULL");
                contract.TeamExternalId = null;
                contract.TeamSource = null;
            }
        }

        // Check division reference
        if (!string.IsNullOrWhiteSpace(contract.DivisionExternalId))
        {
            var divExists = await connection.ExecuteScalarAsync<bool>(
                "SELECT EXISTS(SELECT 1 FROM divisions WHERE external_id = @ExternalId AND source = @Source)",
                new { ExternalId = contract.DivisionExternalId, Source = contract.DivisionSource },
                transaction);

            if (!divExists)
            {
                Debug.WriteLine($"[Import] Contract {contract.ExternalId} has invalid division reference ({contract.DivisionExternalId}, {contract.DivisionSource}) - setting to NULL");
                contract.DivisionExternalId = null;
                contract.DivisionSource = null;
            }
        }

        // Check organization reference
        if (!string.IsNullOrWhiteSpace(contract.OrganizationExternalId))
        {
            var orgExists = await connection.ExecuteScalarAsync<bool>(
                "SELECT EXISTS(SELECT 1 FROM organizations WHERE external_id = @ExternalId AND source = @Source)",
                new { ExternalId = contract.OrganizationExternalId, Source = contract.OrganizationSource },
                transaction);

            if (!orgExists)
            {
                Debug.WriteLine($"[Import] Contract {contract.ExternalId} has invalid organization reference ({contract.OrganizationExternalId}, {contract.OrganizationSource}) - setting to NULL");
                contract.OrganizationExternalId = null;
                contract.OrganizationSource = null;
            }
        }
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
    /// Generates database-agnostic INSERT IGNORE SQL.
    /// SQLite uses INSERT OR IGNORE, PostgreSQL uses INSERT ... ON CONFLICT DO NOTHING.
    /// For PostgreSQL, uses ON CONFLICT DO NOTHING without specifying columns (implicitly uses primary key).
    /// </summary>
    private string GetInsertIgnoreSql(string tableName, string columns, string valuesPlaceholder)
    {
        if (_connectionFactory.DatabaseType == DatabaseType.PostgreSql)
        {
            return $"INSERT INTO {tableName} ({columns}) VALUES ({valuesPlaceholder}) ON CONFLICT DO NOTHING";
        }
        else
        {
            return $"INSERT OR IGNORE INTO {tableName} ({columns}) VALUES ({valuesPlaceholder})";
        }
    }

    /// <summary>
    /// Imports data to Turso by creating a temp SQLite database, importing, and uploading.
    /// </summary>
    private async Task<ImportResult> ImportToTursoAsync(string filePath, PrimaryManagerLogic primaryManagerLogic, IProgress<ImportProgress>? progress = null)
    {
        if (_tursoClient == null)
        {
            return new ImportResult
            {
                Success = false,
                ErrorMessage = "Turso client is not configured. Please check your Turso settings."
            };
        }

        if (string.IsNullOrEmpty(_sqliteSchemaPath) || !File.Exists(_sqliteSchemaPath))
        {
            Debug.WriteLine($"[TursoImport] ERROR: SQLite schema file not found at: {_sqliteSchemaPath}");
            return new ImportResult
            {
                Success = false,
                ErrorMessage = "SQLite schema file not found. Cannot create temp database."
            };
        }

        Debug.WriteLine($"[TursoImport] Starting import to Turso");
        Debug.WriteLine($"[TursoImport] Schema path: {_sqliteSchemaPath}");
        Debug.WriteLine($"[TursoImport] Input file: {filePath}");

        var stopwatch = Stopwatch.StartNew();
        var result = new ImportResult { Success = true };
        string? tempDbPath = null;

        try
        {
            // Create temp database path
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "HelloID.Vault.Management", "db");
            Directory.CreateDirectory(appDataPath);
            tempDbPath = Path.Combine(appDataPath, "tempvault.db");

            Debug.WriteLine($"[TursoImport] Temp database path: {tempDbPath}");

            // Delete existing temp file if present
            if (File.Exists(tempDbPath))
            {
                Debug.WriteLine($"[TursoImport] Deleting existing temp file");
                File.Delete(tempDbPath);
            }

            progress?.Report(new ImportProgress { CurrentOperation = "Creating temporary SQLite database..." });

            // Create temp SQLite connection factory
            var tempConnectionFactory = new SqliteConnectionFactory(tempDbPath);

            // Initialize schema
            Debug.WriteLine($"[TursoImport] Reading schema file...");
            var schemaSql = await File.ReadAllTextAsync(_sqliteSchemaPath);
            Debug.WriteLine($"[TursoImport] Schema file size: {schemaSql.Length} characters");

            using (var connection = tempConnectionFactory.CreateConnection())
            {
                Debug.WriteLine($"[TursoImport] Executing schema SQL...");
                await connection.ExecuteAsync(schemaSql);
                Debug.WriteLine($"[TursoImport] Schema created successfully");
            }

            // Clear pools
            SqliteConnection.ClearAllPools();
            await Task.Delay(200);

            progress?.Report(new ImportProgress { CurrentOperation = "Importing data to temporary database..." });

            // Run the import using the temp connection factory
            Debug.WriteLine($"[TursoImport] Starting data import to temp SQLite...");
            result = await ImportToSqliteAsync(filePath, primaryManagerLogic, tempConnectionFactory, progress);
            Debug.WriteLine($"[TursoImport] Data import complete. Success: {result.Success}, Persons: {result.PersonsImported}, Contracts: {result.ContractsImported}");

            if (!result.Success)
            {
                Debug.WriteLine($"[TursoImport] Import failed: {result.ErrorMessage}");
                return result;
            }

            progress?.Report(new ImportProgress { CurrentOperation = "Uploading database to Turso..." });

            // Clear all pools before uploading
            SqliteConnection.ClearAllPools();
            await Task.Delay(500);

            // Get file size before upload
            var fileInfo = new FileInfo(tempDbPath);
            Debug.WriteLine($"[TursoImport] Temp database size: {fileInfo.Length / 1024} KB");

            // Upload to Turso (throws TursoConnectionException on failure)
            Debug.WriteLine($"[TursoImport] Starting upload to Turso...");
            await _tursoClient.UploadDatabaseAsync(tempDbPath);

            Debug.WriteLine($"[TursoImport] Upload successful!");
            progress?.Report(new ImportProgress { CurrentOperation = "Upload complete!" });
        }
        catch (TursoAuthException authEx)
        {
            Debug.WriteLine($"[TursoImport] Auth exception: {authEx.Message}");
            result.Success = false;
            result.ErrorMessage = $"Turso authentication failed: {(authEx.IsTokenExpired ? "Token has expired" : "Invalid token")}";
        }
        catch (TursoNetworkException netEx)
        {
            Debug.WriteLine($"[TursoImport] Network exception: {netEx.Message}");
            result.Success = false;
            result.ErrorMessage = $"Network error: {(netEx.IsOffline ? "No internet connection" : netEx.Message)}";
        }
        catch (TursoConnectionException connEx)
        {
            Debug.WriteLine($"[TursoImport] Connection exception: {connEx.Message}");

            // Check if this is an upload failure or database doesn't exist, and we have Platform API credentials for auto-create
            if ((connEx.Message.Contains("upload", StringComparison.OrdinalIgnoreCase) ||
                 connEx.Message.Contains("copying content", StringComparison.OrdinalIgnoreCase) ||
                 connEx.Message.Contains("doesn't exist", StringComparison.OrdinalIgnoreCase) ||
                 connEx.Message.Contains("Namespace", StringComparison.OrdinalIgnoreCase)) &&
                !string.IsNullOrEmpty(_userPreferencesService.TursoPlatformApiToken) &&
                !string.IsNullOrEmpty(_userPreferencesService.TursoDatabaseName))
            {
                Debug.WriteLine($"[TursoImport] Attempting auto-create with Platform API...");

                progress?.Report(new ImportProgress { CurrentOperation = "Database doesn't support uploads. Creating new database..." });

                try
                {
                    // Try to create a new database with upload support
                    var autoCreateResult = await TryAutoCreateTursoDatabaseAsync(tempDbPath!, progress);

                    if (autoCreateResult.Success)
                    {
                        result.Success = true;
                        result.PersonsImported = autoCreateResult.PersonsImported;
                        result.ContractsImported = autoCreateResult.ContractsImported;
                        Debug.WriteLine($"[TursoImport] Auto-create successful!");
                        return result;
                    }
                    else
                    {
                        result.Success = false;
                        result.ErrorMessage = autoCreateResult.ErrorMessage ?? "Auto-create failed";
                    }
                }
                catch (Exception autoEx)
                {
                    Debug.WriteLine($"[TursoImport] Auto-create failed: {autoEx.Message}");
                    result.Success = false;
                    result.ErrorMessage = $"Auto-create failed: {autoEx.Message}\n\n" +
                        $"Temp file preserved at:\n{tempDbPath}\n\n" +
                        "You can manually import using:\nturso db shell {database-name} < {tempDbPath}";
                }
            }
            else
            {
                result.Success = false;

                // Check for database not existing or upload failures
                bool isDatabaseNotFoundError = connEx.Message.Contains("doesn't exist", StringComparison.OrdinalIgnoreCase) ||
                                               connEx.Message.Contains("Namespace", StringComparison.OrdinalIgnoreCase);
                bool isUploadError = connEx.Message.Contains("upload", StringComparison.OrdinalIgnoreCase) ||
                                     connEx.Message.Contains("copying content", StringComparison.OrdinalIgnoreCase);

                if (isDatabaseNotFoundError || isUploadError)
                {
                    if (string.IsNullOrEmpty(_userPreferencesService.TursoPlatformApiToken))
                    {
                        result.ErrorMessage = (isDatabaseNotFoundError ? "Database doesn't exist" : "Upload failed") + ".\n\n" +
                            "To enable auto-create, configure Platform API Token and Database Name in Settings.\n\n" +
                            $"Temp file preserved at:\n{tempDbPath}";
                    }
                    else
                    {
                        result.ErrorMessage = "Automatic upload failed. Use CLI workflow instead:\n\n" +
                            $"1. Keep temp file at:\n   {tempDbPath}\n\n" +
                            "2. Run: turso db import {tempDbPath}\n\n" +
                            "3. Or use Turso shell: turso db shell {database-name}";
                    }
                }
                else
                {
                    result.ErrorMessage = $"Turso connection error: {connEx.Message}";
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[TursoImport] Exception: {ex.GetType().Name} - {ex.Message}");
            Debug.WriteLine($"[TursoImport] Stack trace: {ex.StackTrace}");
            result.Success = false;
            result.ErrorMessage = $"Import failed: {ex.Message}";
        }
        finally
        {
            // Clean up temp file only on success
            if (result.Success && tempDbPath != null)
            {
                try
                {
                    SqliteConnection.ClearAllPools();
                    await Task.Delay(200);

                    if (File.Exists(tempDbPath))
                    {
                        File.Delete(tempDbPath);
                        Debug.WriteLine($"[VaultImportService] Cleaned up temp file: {tempDbPath}");
                    }
                }
                catch (Exception cleanupEx)
                {
                    Debug.WriteLine($"[VaultImportService] Failed to clean up temp file: {cleanupEx.Message}");
                }
            }
            else if (!result.Success && tempDbPath != null && File.Exists(tempDbPath))
            {
                Debug.WriteLine($"[VaultImportService] Temp file preserved for manual CLI import: {tempDbPath}");
            }

            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;
        }

        return result;
    }

    /// <summary>
    /// Attempts to create a new Turso database with upload support and import the data.
    /// Called when upload fails because the existing database doesn't support uploads.
    /// </summary>
    private async Task<ImportResult> TryAutoCreateTursoDatabaseAsync(string tempDbPath, IProgress<ImportProgress>? progress = null)
    {
        var result = new ImportResult { Success = false };

        try
        {
            var platformService = new TursoPlatformService();
            platformService.SetAuthToken(_userPreferencesService.TursoPlatformApiToken!);

            var databaseName = _userPreferencesService.TursoDatabaseName!;
            var organization = _userPreferencesService.TursoOrganizationSlug;

            // First check if database already exists
            progress?.Report(new ImportProgress { CurrentOperation = $"Checking if database '{databaseName}' exists..." });
            Debug.WriteLine($"[TursoImport] Checking if database exists: {databaseName}");

            var databaseExists = await platformService.DatabaseExistsAsync(databaseName, organization);
            Debug.WriteLine($"[TursoImport] Database exists: {databaseExists}");

            if (databaseExists)
            {
                // Database exists - delete and recreate it
                Debug.WriteLine($"[TursoImport] Database already exists, deleting and recreating...");

                progress?.Report(new ImportProgress { CurrentOperation = $"Deleting existing database '{databaseName}'..." });

                try
                {
                    await platformService.DeleteDatabaseAsync(databaseName, organization);
                    Debug.WriteLine($"[TursoImport] Database deleted successfully");

                    // Wait a moment for Turso to process the deletion
                    await Task.Delay(2000);
                }
                catch (Exception delEx)
                {
                    Debug.WriteLine($"[TursoImport] Failed to delete database: {delEx.Message}");
                    result.ErrorMessage = $"Failed to delete existing database '{databaseName}': {delEx.Message}\n\n" +
                        $"Temp file preserved at:\n{tempDbPath}";
                    return result;
                }
            }

            progress?.Report(new ImportProgress { CurrentOperation = $"Creating Turso database '{databaseName}'..." });
            Debug.WriteLine($"[TursoImport] Creating database: {databaseName}");

            // Create the database with upload support
            var dbResult = await platformService.CreateDatabaseAsync(databaseName, "default", organization, forUpload: true);

            if (dbResult == null || string.IsNullOrEmpty(dbResult.Hostname))
            {
                result.ErrorMessage = "Failed to create database - no hostname returned";
                return result;
            }

            Debug.WriteLine($"[TursoImport] Database created: {dbResult.Url}");

            progress?.Report(new ImportProgress { CurrentOperation = "Creating database token..." });

            // Create a token for the new database
            var token = await platformService.CreateDatabaseTokenAsync(databaseName, organization);

            if (string.IsNullOrEmpty(token))
            {
                result.ErrorMessage = "Failed to create database token";
                return result;
            }

            Debug.WriteLine($"[TursoImport] Token created successfully");

            // Update preferences with new database credentials
            _userPreferencesService.TursoDatabaseUrl = dbResult.Url;
            _userPreferencesService.TursoAuthToken = token;
            await _userPreferencesService.SaveAsync();

            Debug.WriteLine($"[TursoImport] Updated preferences with new credentials");

            // Update the TursoClient with new credentials
            if (_tursoClient is TursoClient tursoClient)
            {
                tursoClient.UpdateCredentials(dbResult.Url, token);
            }

            progress?.Report(new ImportProgress { CurrentOperation = "Uploading database to new Turso database..." });

            // Retry the upload
            var uploadSuccess = await _tursoClient!.UploadDatabaseAsync(tempDbPath);

            if (!uploadSuccess)
            {
                result.ErrorMessage = "Upload to new database failed";
                return result;
            }

            Debug.WriteLine($"[TursoImport] Upload to new database successful!");

            // Get counts from temp database for result
            using (var tempConnection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={tempDbPath}"))
            {
                await tempConnection.OpenAsync();
                result.PersonsImported = await tempConnection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM persons");
                result.ContractsImported = await tempConnection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM contracts");
            }

            result.Success = true;
            result.NewDatabaseUrl = dbResult.Url;
            result.Message = $"Created new database: {dbResult.Url}";
        }
        catch (TursoPlatformException platEx)
        {
            Debug.WriteLine($"[TursoImport] Platform API error: {platEx.Message}");
            result.ErrorMessage = $"Platform API error: {platEx.Message}";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[TursoImport] Auto-create error: {ex.Message}");
            result.ErrorMessage = $"Auto-create error: {ex.Message}";
        }

        return result;
    }

    /// <summary>
    /// Imports data to a SQLite database using the provided connection factory.
    /// </summary>
    private async Task<ImportResult> ImportToSqliteAsync(string filePath, PrimaryManagerLogic primaryManagerLogic, SqliteConnectionFactory connectionFactory, IProgress<ImportProgress>? progress = null)
    {
        Debug.WriteLine($"[TursoImport] ImportToSqliteAsync starting...");
        var stopwatch = Stopwatch.StartNew();
        var result = new ImportResult { Success = true };

        try
        {
            // Step 1: Load and parse JSON
            progress?.Report(new ImportProgress { CurrentOperation = "Loading vault.json file..." });
            Debug.WriteLine($"[TursoImport] Loading JSON file: {filePath}");

            var json = await File.ReadAllTextAsync(filePath);
            Debug.WriteLine($"[TursoImport] JSON file size: {json.Length / 1024} KB");

            var vaultData = JsonSerializer.Deserialize<VaultRoot>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true
            });

            if (vaultData == null || vaultData.Persons == null)
            {
                Debug.WriteLine($"[TursoImport] ERROR: Invalid vault.json - vaultData={vaultData != null}, persons={vaultData?.Persons != null}");
                result.Success = false;
                result.ErrorMessage = "Invalid vault.json file or no persons found.";
                return result;
            }

            var totalPersons = vaultData.Persons.Count;
            var totalDepartments = vaultData.Departments?.Count ?? 0;
            Debug.WriteLine($"[TursoImport] Found {totalPersons} persons, {totalDepartments} departments");

            progress?.Report(new ImportProgress
            {
                CurrentOperation = $"Found {totalPersons} persons to import",
                TotalItems = totalPersons
            });

            // Step 2: Collect all source systems
            progress?.Report(new ImportProgress { CurrentOperation = "Collecting source system data..." });
            Debug.WriteLine($"[TursoImport] Collecting source systems...");

            var sourceSystems = new Dictionary<string, (string DisplayName, string IdentificationKey)>();

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

            // Import source systems
            Debug.WriteLine($"[TursoImport] Importing {sourceSystems.Count} source systems...");
            progress?.Report(new ImportProgress { CurrentOperation = $"Importing {sourceSystems.Count} source systems..." });

            using (var connection = connectionFactory.CreateConnection(enforceForeignKeys: false))
            {
                foreach (var (systemId, (displayName, identificationKey)) in sourceSystems)
                {
                    var sql = "INSERT OR IGNORE INTO source_system (system_id, display_name, identification_key) VALUES (@SystemId, @DisplayName, @IdentificationKey)";
                    var rowsAffected = await connection.ExecuteAsync(sql,
                        new { SystemId = systemId, DisplayName = displayName, IdentificationKey = identificationKey });
                    result.SourceSystemsImported += rowsAffected;
                }
            }
            Debug.WriteLine($"[TursoImport] Source systems imported: {result.SourceSystemsImported}");

            var sourceLookup = sourceSystems.Keys.ToDictionary(id => id, id => id);

            // Step 3: Collect lookup tables
            progress?.Report(new ImportProgress { CurrentOperation = "Collecting lookup table data..." });
            Debug.WriteLine($"[TursoImport] Collecting lookup table data...");

            var context = ReferenceDataCollector.Collect(vaultData, sourceLookup);

            // Step 3: Import lookup tables
            Debug.WriteLine($"[TursoImport] Lookup tables: Orgs={context.Organizations.Count}, Locs={context.Locations.Count}, Employers={context.Employers.Count}, CostCenters={context.CostCenters.Count}, CostBearers={context.CostBearers.Count}, Teams={context.Teams.Count}, Divisions={context.Divisions.Count}, Titles={context.Titles.Count}");
            progress?.Report(new ImportProgress { CurrentOperation = "Importing lookup tables..." });

            using (var connection = connectionFactory.CreateConnection(enforceForeignKeys: false))
            {
                foreach (var org in context.Organizations)
                {
                    var source = context.OrganizationSources.TryGetValue(org.ExternalId ?? string.Empty, out var orgSource) ? orgSource : string.Empty;
                    var sql = "INSERT OR IGNORE INTO organizations (external_id, code, name, source) VALUES (@ExternalId, @Code, @Name, @Source)";
                    result.OrganizationsImported += await connection.ExecuteAsync(sql, new { org.ExternalId, org.Code, org.Name, Source = source });
                }

                foreach (var loc in context.Locations)
                {
                    var source = context.LocationSources.TryGetValue(loc.ExternalId ?? string.Empty, out var locSource) ? locSource : string.Empty;
                    var sql = "INSERT OR IGNORE INTO locations (external_id, code, name, source) VALUES (@ExternalId, @Code, @Name, @Source)";
                    result.LocationsImported += await connection.ExecuteAsync(sql, new { loc.ExternalId, loc.Code, loc.Name, Source = source });
                }

                foreach (var emp in context.Employers)
                {
                    var employerNameKey = $"{emp.ExternalId ?? string.Empty}|{emp.Name ?? string.Empty}";
                    var source = context.EmployerNameToSourceMap.TryGetValue(employerNameKey, out var empSource) ? empSource : string.Empty;
                    var sql = "INSERT OR IGNORE INTO employers (external_id, code, name, source) VALUES (@ExternalId, @Code, @Name, @Source)";
                    result.EmployersImported += await connection.ExecuteAsync(sql, new { emp.ExternalId, emp.Code, emp.Name, Source = source });
                }

                foreach (var cc in context.CostCenters)
                {
                    var source = context.CostCenterSources.TryGetValue(cc.ExternalId ?? string.Empty, out var ccSource) ? ccSource : string.Empty;
                    var sql = "INSERT OR IGNORE INTO cost_centers (external_id, code, name, source) VALUES (@ExternalId, @Code, @Name, @Source)";
                    result.CostCentersImported += await connection.ExecuteAsync(sql, new { cc.ExternalId, cc.Code, cc.Name, Source = source });
                }

                foreach (var cb in context.CostBearers)
                {
                    var source = context.CostBearerSources.TryGetValue(cb.ExternalId ?? string.Empty, out var cbSource) ? cbSource : string.Empty;
                    var sql = "INSERT OR IGNORE INTO cost_bearers (external_id, code, name, source) VALUES (@ExternalId, @Code, @Name, @Source)";
                    result.CostBearersImported += await connection.ExecuteAsync(sql, new { cb.ExternalId, cb.Code, cb.Name, Source = source });
                }

                foreach (var team in context.Teams)
                {
                    var source = context.TeamSources.TryGetValue(team.ExternalId ?? string.Empty, out var teamSource) ? teamSource : string.Empty;
                    var sql = "INSERT OR IGNORE INTO teams (external_id, code, name, source) VALUES (@ExternalId, @Code, @Name, @Source)";
                    result.TeamsImported += await connection.ExecuteAsync(sql, new { team.ExternalId, team.Code, team.Name, Source = source });
                }

                foreach (var div in context.Divisions)
                {
                    var source = context.DivisionSources.TryGetValue(div.ExternalId ?? string.Empty, out var divSource) ? divSource : string.Empty;
                    var sql = "INSERT OR IGNORE INTO divisions (external_id, code, name, source) VALUES (@ExternalId, @Code, @Name, @Source)";
                    result.DivisionsImported += await connection.ExecuteAsync(sql, new { div.ExternalId, div.Code, div.Name, Source = source });
                }

                foreach (var title in context.Titles)
                {
                    var source = context.TitleSources.TryGetValue(title.ExternalId ?? string.Empty, out var titleSource) ? titleSource : string.Empty;
                    var sql = "INSERT OR IGNORE INTO titles (external_id, code, name, source) VALUES (@ExternalId, @Code, @Name, @Source)";
                    result.TitlesImported += await connection.ExecuteAsync(sql, new { title.ExternalId, title.Code, title.Name, Source = source });
                }
            }

            // Step 4: Import departments
            progress?.Report(new ImportProgress { CurrentOperation = "Importing departments..." });

            using (var connection = connectionFactory.CreateConnection(enforceForeignKeys: false))
            {
                var processedDepts = new HashSet<string>();
                if (vaultData.Departments != null)
                {
                    foreach (var dept in vaultData.Departments)
                    {
                        if (string.IsNullOrWhiteSpace(dept.ExternalId)) continue;
                        if (processedDepts.Contains(dept.ExternalId)) continue;

                        var source = dept.Source?.SystemId ?? string.Empty;
                        Debug.WriteLine($"[TursoImport] Inserting department: {dept.ExternalId} - {dept.DisplayName}");
                        var sql = "INSERT OR IGNORE INTO departments (external_id, code, display_name, parent_external_id, manager_person_id, source) VALUES (@ExternalId, @Code, @DisplayName, @ParentExternalId, @ManagerPersonId, @Source)";
                        result.DepartmentsImported += await connection.ExecuteAsync(sql,
                            new { ExternalId = dept.ExternalId, Code = dept.Code, DisplayName = dept.DisplayName, ParentExternalId = dept.ParentExternalId, ManagerPersonId = dept.Manager?.PersonId, Source = source });
                        processedDepts.Add(dept.ExternalId);
                    }
                }
            }

            // Step 4.5: Collect and create custom field schemas
            Debug.WriteLine($"[TursoImport] Creating custom field schemas...");
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
                            if (value != null)
                            {
                                customFieldSchemas[schemaKey].SampleValues.Add(value);
                            }
                        }
                    }
                }
            }

            Debug.WriteLine($"[TursoImport] Found {customFieldSchemas.Count} unique custom field schemas");

            // Insert custom field schemas
            using (var connection = connectionFactory.CreateConnection(enforceForeignKeys: false))
            {
                int sortOrder = 1;
                foreach (var ((tableName, fieldKey), (_, sampleValues)) in customFieldSchemas)
                {
                    var dataType = DetectDataType(sampleValues);
                    var displayName = FormatDisplayName(fieldKey);

                    var sql = "INSERT OR IGNORE INTO custom_field_schemas (table_name, field_key, display_name, sort_order) VALUES (@TableName, @FieldKey, @DisplayName, @SortOrder)";
                    var rowsAffected = await connection.ExecuteAsync(sql,
                        new
                        {
                            TableName = tableName,
                            FieldKey = fieldKey,
                            DisplayName = displayName,
                            SortOrder = sortOrder++
                        });

                    if (tableName == "persons")
                        result.CustomFieldPersonsImported += rowsAffected;
                    else if (tableName == "contracts")
                        result.CustomFieldContractsImported += rowsAffected;
                }
            }

            // Step 5: Import persons, contracts, contacts
            Debug.WriteLine($"[TursoImport] Importing persons, contracts, contacts...");
            progress?.Report(new ImportProgress { CurrentOperation = "Importing persons and contracts..." });

            using (var connection = connectionFactory.CreateConnection(enforceForeignKeys: false))
            {
                var processedPersons = 0;

                foreach (var vaultPerson in vaultData.Persons)
                {
                    if (string.IsNullOrWhiteSpace(vaultPerson.PersonId)) continue;

                    var personSource = vaultPerson.Source?.SystemId ?? string.Empty;

                    // Full schema with all fields
                    var personSql = @"INSERT OR IGNORE INTO persons (
                        person_id, display_name, external_id, user_name,
                        gender, honorific_prefix, honorific_suffix, birth_date, birth_locality, marital_status,
                        initials, given_name, family_name, family_name_prefix, convention, nick_name,
                        family_name_partner, family_name_partner_prefix,
                        blocked, status_reason, excluded, hr_excluded, manual_excluded,
                        source, primary_manager_person_id, primary_manager_source, primary_manager_updated_at,
                        custom_fields
                    ) VALUES (
                        @PersonId, @DisplayName, @ExternalId, @UserName,
                        @Gender, @HonorificPrefix, @HonorificSuffix, @BirthDate, @BirthLocality, @MaritalStatus,
                        @Initials, @GivenName, @FamilyName, @FamilyNamePrefix, @Convention, @NickName,
                        @FamilyNamePartner, @FamilyNamePartnerPrefix,
                        @Blocked, @StatusReason, @Excluded, @HrExcluded, @ManualExcluded,
                        @Source, @PrimaryManagerPersonId, @PrimaryManagerSource, @PrimaryManagerUpdatedAt,
                        @CustomFields
                    )";
                    await connection.ExecuteAsync(personSql,
                        new
                        {
                            PersonId = vaultPerson.PersonId,
                            DisplayName = vaultPerson.DisplayName,
                            ExternalId = vaultPerson.ExternalId,
                            UserName = vaultPerson.UserName,
                            Gender = vaultPerson.Details?.Gender,
                            HonorificPrefix = vaultPerson.Details?.HonorificPrefix,
                            HonorificSuffix = vaultPerson.Details?.HonorificSuffix,
                            BirthDate = vaultPerson.Details?.BirthDate?.ToString("yyyy-MM-dd"),
                            BirthLocality = vaultPerson.Details?.BirthLocality,
                            MaritalStatus = vaultPerson.Details?.MaritalStatus,
                            Initials = vaultPerson.Name?.Initials,
                            GivenName = vaultPerson.Name?.GivenName,
                            FamilyName = vaultPerson.Name?.FamilyName,
                            FamilyNamePrefix = vaultPerson.Name?.FamilyNamePrefix,
                            Convention = vaultPerson.Name?.Convention,
                            NickName = vaultPerson.Name?.NickName,
                            FamilyNamePartner = vaultPerson.Name?.FamilyNamePartner,
                            FamilyNamePartnerPrefix = vaultPerson.Name?.FamilyNamePartnerPrefix,
                            Blocked = vaultPerson.Status?.Blocked ?? false ? 1 : 0,
                            StatusReason = vaultPerson.Status?.Reason,
                            Excluded = vaultPerson.Excluded ? 1 : 0,
                            HrExcluded = vaultPerson.ExclusionDetails?.Hr ?? false ? 1 : 0,
                            ManualExcluded = vaultPerson.ExclusionDetails?.Manual ?? false ? 1 : 0,
                            Source = personSource,
                            PrimaryManagerPersonId = vaultPerson.PrimaryManager?.PersonId,
                            PrimaryManagerSource = personSource,
                            PrimaryManagerUpdatedAt = DateTime.UtcNow.ToString("O"),
                            CustomFields = vaultPerson.Custom != null && vaultPerson.Custom.Count > 0
                                ? JsonSerializer.Serialize(vaultPerson.Custom)
                                : "{}"
                        });
                    result.PersonsImported++;

                    // Import contacts (full schema with address fields)
                    if (vaultPerson.Contact != null)
                    {
                        if (vaultPerson.Contact.Personal != null)
                        {
                            var contactSql = "INSERT OR IGNORE INTO contacts (person_id, type, email, phone_mobile, phone_fixed, address_street, address_house_number, address_postal, address_locality, address_country) VALUES (@PersonId, @Type, @Email, @PhoneMobile, @PhoneFixed, @AddressStreet, @AddressHouseNumber, @AddressPostal, @AddressLocality, @AddressCountry)";
                            await connection.ExecuteAsync(contactSql,
                                new
                                {
                                    PersonId = vaultPerson.PersonId,
                                    Type = "Personal",
                                    Email = vaultPerson.Contact.Personal.Email,
                                    PhoneMobile = vaultPerson.Contact.Personal.Phone?.Mobile,
                                    PhoneFixed = vaultPerson.Contact.Personal.Phone?.Fixed,
                                    AddressStreet = vaultPerson.Contact.Personal.Address?.Street,
                                    AddressHouseNumber = vaultPerson.Contact.Personal.Address?.HouseNumber,
                                    AddressPostal = vaultPerson.Contact.Personal.Address?.PostalCode,
                                    AddressLocality = vaultPerson.Contact.Personal.Address?.Locality,
                                    AddressCountry = vaultPerson.Contact.Personal.Address?.Country
                                });
                            result.ContactsImported++;
                        }
                        if (vaultPerson.Contact.Business != null)
                        {
                            var contactSql = "INSERT OR IGNORE INTO contacts (person_id, type, email, phone_mobile, phone_fixed, address_street, address_house_number, address_postal, address_locality, address_country) VALUES (@PersonId, @Type, @Email, @PhoneMobile, @PhoneFixed, @AddressStreet, @AddressHouseNumber, @AddressPostal, @AddressLocality, @AddressCountry)";
                            await connection.ExecuteAsync(contactSql,
                                new
                                {
                                    PersonId = vaultPerson.PersonId,
                                    Type = "Business",
                                    Email = vaultPerson.Contact.Business.Email,
                                    PhoneMobile = vaultPerson.Contact.Business.Phone?.Mobile,
                                    PhoneFixed = vaultPerson.Contact.Business.Phone?.Fixed,
                                    AddressStreet = vaultPerson.Contact.Business.Address?.Street,
                                    AddressHouseNumber = vaultPerson.Contact.Business.Address?.HouseNumber,
                                    AddressPostal = vaultPerson.Contact.Business.Address?.PostalCode,
                                    AddressLocality = vaultPerson.Contact.Business.Address?.Locality,
                                    AddressCountry = vaultPerson.Contact.Business.Address?.Country
                                });
                            result.ContactsImported++;
                        }
                    }

                    // Import contracts
                    foreach (var contract in vaultPerson.Contracts)
                    {
                        // Full schema with all fields
                        var contractSql = @"INSERT OR IGNORE INTO contracts (
                            external_id, person_id, start_date, end_date,
                            type_code, type_description, fte, hours_per_week, percentage, sequence,
                            manager_person_external_id,
                            location_external_id, location_source,
                            cost_center_external_id, cost_center_source,
                            cost_bearer_external_id, cost_bearer_source,
                            employer_external_id, employer_source,
                            team_external_id, team_source,
                            department_external_id, department_source,
                            division_external_id, division_source,
                            title_external_id, title_source,
                            organization_external_id, organization_source,
                            source, custom_fields
                        ) VALUES (
                            @ExternalId, @PersonId, @StartDate, @EndDate,
                            @TypeCode, @TypeDescription, @Fte, @HoursPerWeek, @Percentage, @Sequence,
                            @ManagerPersonExternalId,
                            @LocationExternalId, @LocationSource,
                            @CostCenterExternalId, @CostCenterSource,
                            @CostBearerExternalId, @CostBearerSource,
                            @EmployerExternalId, @EmployerSource,
                            @TeamExternalId, @TeamSource,
                            @DepartmentExternalId, @DepartmentSource,
                            @DivisionExternalId, @DivisionSource,
                            @TitleExternalId, @TitleSource,
                            @OrganizationExternalId, @OrganizationSource,
                            @Source, @CustomFields
                        )";
                        var contractSource = contract.Source?.SystemId;
                        await connection.ExecuteAsync(contractSql,
                            new
                            {
                                ExternalId = contract.ExternalId,
                                PersonId = vaultPerson.PersonId,
                                StartDate = contract.StartDate?.ToString("yyyy-MM-dd"),
                                EndDate = contract.EndDate?.ToString("yyyy-MM-dd"),
                                TypeCode = contract.Type?.Code,
                                TypeDescription = contract.Type?.Description,
                                Fte = (double?)contract.Details?.Fte,
                                HoursPerWeek = (double?)contract.Details?.HoursPerWeek,
                                Percentage = (double?)contract.Details?.Percentage,
                                Sequence = contract.Details?.Sequence,
                                ManagerPersonExternalId = contract.Manager?.PersonId,
                                LocationExternalId = contract.Location?.ExternalId,
                                LocationSource = contractSource,  // VaultReference doesn't have Source
                                CostCenterExternalId = contract.CostCenter?.ExternalId,
                                CostCenterSource = contractSource,  // VaultReference doesn't have Source
                                CostBearerExternalId = contract.CostBearer?.ExternalId,
                                CostBearerSource = contractSource,  // VaultReference doesn't have Source
                                EmployerExternalId = contract.Employer?.ExternalId,
                                EmployerSource = contractSource,  // VaultReference doesn't have Source
                                TeamExternalId = contract.Team?.ExternalId,
                                TeamSource = contractSource,  // VaultReference doesn't have Source
                                DepartmentExternalId = contract.Department?.ExternalId,
                                DepartmentSource = contract.Department?.Source?.SystemId ?? contractSource,
                                DivisionExternalId = contract.Division?.ExternalId,
                                DivisionSource = contractSource,  // VaultReference doesn't have Source
                                TitleExternalId = contract.Title?.ExternalId,
                                TitleSource = contractSource,  // VaultReference doesn't have Source
                                OrganizationExternalId = contract.Organization?.ExternalId,
                                OrganizationSource = contractSource,  // VaultReference doesn't have Source
                                Source = contractSource,
                                CustomFields = contract.Custom != null && contract.Custom.Count > 0
                                    ? JsonSerializer.Serialize(contract.Custom)
                                    : "{}"
                            });
                        result.ContractsImported++;
                    }

                    processedPersons++;
                    if (processedPersons % 100 == 0)
                    {
                        progress?.Report(new ImportProgress
                        {
                            CurrentOperation = $"Imported {processedPersons}/{totalPersons} persons",
                            TotalItems = totalPersons,
                            ProcessedItems = processedPersons
                        });
                    }
                }
            }

            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;
            result.Success = true;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = $"Import failed: {ex.Message}";
            Debug.WriteLine($"[VaultImportService] ImportToSqliteAsync error: {ex}");
        }

        return result;
    }

    /// <summary>
    /// Imports company-only data to Turso by creating a temp SQLite database, importing, and uploading.
    /// </summary>
    private async Task<ImportResult> ImportCompanyOnlyToTursoAsync(string filePath, IProgress<ImportProgress>? progress = null)
    {
        if (_tursoClient == null)
        {
            return new ImportResult
            {
                Success = false,
                ErrorMessage = "Turso client is not configured. Please check your Turso settings."
            };
        }

        if (string.IsNullOrEmpty(_sqliteSchemaPath) || !File.Exists(_sqliteSchemaPath))
        {
            return new ImportResult
            {
                Success = false,
                ErrorMessage = "SQLite schema file not found. Cannot create temp database."
            };
        }

        var stopwatch = Stopwatch.StartNew();
        var result = new ImportResult { Success = true };
        string? tempDbPath = null;

        try
        {
            // Create temp database path
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "HelloID.Vault.Management", "db");
            Directory.CreateDirectory(appDataPath);
            tempDbPath = Path.Combine(appDataPath, "tempvault.db");

            // Delete existing temp file if present
            if (File.Exists(tempDbPath))
            {
                File.Delete(tempDbPath);
            }

            progress?.Report(new ImportProgress { CurrentOperation = "Creating temporary SQLite database..." });

            // Create temp SQLite connection factory
            var tempConnectionFactory = new SqliteConnectionFactory(tempDbPath);

            // Initialize schema
            var schemaSql = await File.ReadAllTextAsync(_sqliteSchemaPath);
            using (var connection = tempConnectionFactory.CreateConnection())
            {
                await connection.ExecuteAsync(schemaSql);
            }

            // Clear pools
            SqliteConnection.ClearAllPools();
            await Task.Delay(200);

            progress?.Report(new ImportProgress { CurrentOperation = "Importing company data to temporary database..." });

            // Run the company-only import
            result = await ImportCompanyOnlyToSqliteAsync(filePath, tempConnectionFactory, progress);

            if (!result.Success)
            {
                return result;
            }

            progress?.Report(new ImportProgress { CurrentOperation = "Uploading database to Turso..." });

            // Clear all pools before uploading
            SqliteConnection.ClearAllPools();
            await Task.Delay(500);

            // Try upload to Turso - catch exceptions to allow fallback
            bool uploadSuccess = false;
            try
            {
                uploadSuccess = await _tursoClient.UploadDatabaseAsync(tempDbPath);
            }
            catch (Exception uploadEx)
            {
                Debug.WriteLine($"[TursoCompanyImport] Upload failed: {uploadEx.Message}");
            }

            if (uploadSuccess)
            {
                progress?.Report(new ImportProgress { CurrentOperation = "Upload complete!" });
                return result;
            }

            // Simple upload failed - try auto-create with delete/recreate
            Debug.WriteLine($"[TursoCompanyImport] Simple upload failed, trying auto-create...");
            progress?.Report(new ImportProgress { CurrentOperation = "Upload failed, recreating database..." });

            if (string.IsNullOrEmpty(_userPreferencesService.TursoPlatformApiToken) ||
                string.IsNullOrEmpty(_userPreferencesService.TursoDatabaseName))
            {
                result.Success = false;
                result.ErrorMessage = "Database upload failed and Platform API is not configured.\n\n" +
                    "To enable automatic database recreation, configure Platform API Token and Database Name in Settings.";
                return result;
            }

            var autoCreateResult = await TryAutoCreateTursoDatabaseAsync(tempDbPath, progress);

            if (autoCreateResult.Success)
            {
                result.Success = true;
                Debug.WriteLine($"[TursoCompanyImport] Auto-create successful!");
            }
            else
            {
                result.Success = false;
                result.ErrorMessage = autoCreateResult.ErrorMessage ?? "Auto-create failed";
            }
        }
        catch (TursoAuthException authEx)
        {
            result.Success = false;
            result.ErrorMessage = $"Turso authentication failed: {(authEx.IsTokenExpired ? "Token has expired" : "Invalid token")}";
        }
        catch (TursoNetworkException netEx)
        {
            result.Success = false;
            result.ErrorMessage = $"Network error: {(netEx.IsOffline ? "No internet connection" : netEx.Message)}";
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = $"Import failed: {ex.Message}";
            Debug.WriteLine($"[VaultImportService] ImportCompanyOnlyToTursoAsync error: {ex}");
        }
        finally
        {
            // Clean up temp file
            if (tempDbPath != null)
            {
                try
                {
                    SqliteConnection.ClearAllPools();
                    await Task.Delay(200);

                    if (File.Exists(tempDbPath))
                    {
                        File.Delete(tempDbPath);
                        Debug.WriteLine($"[VaultImportService] Cleaned up temp file: {tempDbPath}");
                    }
                }
                catch (Exception cleanupEx)
                {
                    Debug.WriteLine($"[VaultImportService] Failed to clean up temp file: {cleanupEx.Message}");
                }
            }

            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;
        }

        return result;
    }

    /// <summary>
    /// Imports company-only data to a SQLite database using the provided connection factory.
    /// </summary>
    private async Task<ImportResult> ImportCompanyOnlyToSqliteAsync(string filePath, SqliteConnectionFactory connectionFactory, IProgress<ImportProgress>? progress = null)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new ImportResult { Success = true };

        try
        {
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

            // Step 2: Collect source systems
            progress?.Report(new ImportProgress { CurrentOperation = "Collecting source system data..." });

            var sourceSystems = new Dictionary<string, (string DisplayName, string IdentificationKey)>();

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

            // Import source systems
            using (var connection = connectionFactory.CreateConnection(enforceForeignKeys: false))
            {
                foreach (var (systemId, (displayName, identificationKey)) in sourceSystems)
                {
                    var sql = "INSERT OR IGNORE INTO source_system (system_id, display_name, identification_key) VALUES (@SystemId, @DisplayName, @IdentificationKey)";
                    result.SourceSystemsImported += await connection.ExecuteAsync(sql,
                        new { SystemId = systemId, DisplayName = displayName, IdentificationKey = identificationKey });
                }
            }

            var sourceLookup = sourceSystems.Keys.ToDictionary(id => id, id => id);

            // Step 3: Collect and import lookup tables
            progress?.Report(new ImportProgress { CurrentOperation = "Collecting lookup table data..." });

            var context = ReferenceDataCollector.Collect(vaultData, sourceLookup);

            progress?.Report(new ImportProgress { CurrentOperation = "Importing lookup tables..." });

            using (var conn = connectionFactory.CreateConnection(enforceForeignKeys: false))
            {
                foreach (var org in context.Organizations)
                {
                    var source = context.OrganizationSources.TryGetValue(org.ExternalId ?? string.Empty, out var orgSource) ? orgSource : null;
                    var sql = "INSERT OR IGNORE INTO organizations (external_id, code, name, source) VALUES (@ExternalId, @Code, @Name, @Source)";
                    result.OrganizationsImported += await conn.ExecuteAsync(sql, new { org.ExternalId, org.Code, org.Name, Source = source });
                }

                foreach (var loc in context.Locations)
                {
                    var source = context.LocationSources.TryGetValue(loc.ExternalId ?? string.Empty, out var locSource) ? locSource : null;
                    var sql = "INSERT OR IGNORE INTO locations (external_id, code, name, source) VALUES (@ExternalId, @Code, @Name, @Source)";
                    result.LocationsImported += await conn.ExecuteAsync(sql, new { loc.ExternalId, loc.Code, loc.Name, Source = source });
                }

                foreach (var emp in context.Employers)
                {
                    var employerNameKey = $"{emp.ExternalId ?? string.Empty}|{emp.Name ?? string.Empty}";
                    context.EmployerNameToSourceMap.TryGetValue(employerNameKey, out var source);
                    var sql = "INSERT OR IGNORE INTO employers (external_id, code, name, source) VALUES (@ExternalId, @Code, @Name, @Source)";
                    result.EmployersImported += await conn.ExecuteAsync(sql, new { emp.ExternalId, emp.Code, emp.Name, Source = source });
                }

                foreach (var cc in context.CostCenters)
                {
                    var source = context.CostCenterSources.TryGetValue(cc.ExternalId ?? string.Empty, out var ccSource) ? ccSource : null;
                    var sql = "INSERT OR IGNORE INTO cost_centers (external_id, code, name, source) VALUES (@ExternalId, @Code, @Name, @Source)";
                    result.CostCentersImported += await conn.ExecuteAsync(sql, new { cc.ExternalId, cc.Code, cc.Name, Source = source });
                }

                foreach (var cb in context.CostBearers)
                {
                    var source = context.CostBearerSources.TryGetValue(cb.ExternalId ?? string.Empty, out var cbSource) ? cbSource : null;
                    var sql = "INSERT OR IGNORE INTO cost_bearers (external_id, code, name, source) VALUES (@ExternalId, @Code, @Name, @Source)";
                    result.CostBearersImported += await conn.ExecuteAsync(sql, new { cb.ExternalId, cb.Code, cb.Name, Source = source });
                }

                foreach (var team in context.Teams)
                {
                    var source = context.TeamSources.TryGetValue(team.ExternalId ?? string.Empty, out var teamSource) ? teamSource : null;
                    var sql = "INSERT OR IGNORE INTO teams (external_id, code, name, source) VALUES (@ExternalId, @Code, @Name, @Source)";
                    result.TeamsImported += await conn.ExecuteAsync(sql, new { team.ExternalId, team.Code, team.Name, Source = source });
                }

                foreach (var div in context.Divisions)
                {
                    var source = context.DivisionSources.TryGetValue(div.ExternalId ?? string.Empty, out var divSource) ? divSource : null;
                    var sql = "INSERT OR IGNORE INTO divisions (external_id, code, name, source) VALUES (@ExternalId, @Code, @Name, @Source)";
                    result.DivisionsImported += await conn.ExecuteAsync(sql, new { div.ExternalId, div.Code, div.Name, Source = source });
                }

                foreach (var title in context.Titles)
                {
                    var source = context.TitleSources.TryGetValue(title.ExternalId ?? string.Empty, out var titleSource) ? titleSource : null;
                    var sql = "INSERT OR IGNORE INTO titles (external_id, code, name, source) VALUES (@ExternalId, @Code, @Name, @Source)";
                    result.TitlesImported += await conn.ExecuteAsync(sql, new { title.ExternalId, title.Code, title.Name, Source = source });
                }
            }

            // Step 4: Import departments
            progress?.Report(new ImportProgress { CurrentOperation = "Importing departments..." });

            using (var connection = connectionFactory.CreateConnection(enforceForeignKeys: false))
            {
                var processedDepts = new HashSet<string>();
                if (vaultData.Departments != null)
                {
                    foreach (var dept in vaultData.Departments)
                    {
                        if (string.IsNullOrWhiteSpace(dept.ExternalId)) continue;
                        if (processedDepts.Contains(dept.ExternalId)) continue;

                        var source = dept.Source?.SystemId ?? string.Empty;
                        Debug.WriteLine($"[TursoImport] Inserting department (company-only): {dept.ExternalId} - {dept.DisplayName}");
                        var sql = "INSERT OR IGNORE INTO departments (external_id, code, display_name, parent_external_id, manager_person_id, source) VALUES (@ExternalId, @Code, @DisplayName, @ParentExternalId, @ManagerPersonId, @Source)";
                        result.DepartmentsImported += await connection.ExecuteAsync(sql,
                            new { ExternalId = dept.ExternalId, Code = dept.Code, DisplayName = dept.DisplayName, ParentExternalId = dept.ParentExternalId, ManagerPersonId = (string?)null, Source = source });
                        processedDepts.Add(dept.ExternalId);
                    }
                }
            }

            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;
            result.Success = true;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = $"Import failed: {ex.Message}";
            Debug.WriteLine($"[VaultImportService] ImportCompanyOnlyToSqliteAsync error: {ex}");
        }

        return result;
    }
}
