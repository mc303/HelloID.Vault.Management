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

        _primaryManagerDetector = new PrimaryManagerDetector(
            connectionFactory,
            primaryManagerService,
            userPreferencesService);
    }

    public DatabaseType DatabaseType => _connectionFactory.DatabaseType;

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
}
