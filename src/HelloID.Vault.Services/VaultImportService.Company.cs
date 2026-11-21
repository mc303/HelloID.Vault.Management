using Dapper;
using HelloID.Vault.Core.Models.Entities;
using HelloID.Vault.Core.Models.Json;
using HelloID.Vault.Core.Models;
using HelloID.Vault.Data.Connection;
using HelloID.Vault.Data.Repositories.Interfaces;
using HelloID.Vault.Data;
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
using System.Data;
using System.Diagnostics;
using System.Text.Json;

namespace HelloID.Vault.Services;

/// <summary>
/// Turso/platform, custom field and company-import parts of VaultImportService.
/// </summary>
public partial class VaultImportService
{

    public async Task<ImportResult> ImportCompanyOnlyAsync(string filePath, PrimaryManagerLogic primaryManagerLogic, bool createBackup = false, IProgress<ImportProgress>? progress = null)
    {
        // Turso requires special handling via temp SQLite + HTTP upload
        if (_connectionFactory.DatabaseType == DatabaseType.Turso)
        {
            return await ImportCompanyOnlyToTursoAsync(filePath, createBackup, progress);
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
            HelloID.Vault.Core.Utilities.FieldKeySanitizer.SanitizeVaultData(vaultData);

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

            var defaultSource = sourceSystems.Keys.FirstOrDefault() ?? "unknown";

            using (var connection = _connectionFactory.CreateConnection(enforceForeignKeys: false))
            {
                // Insert organizations
                foreach (var org in context.Organizations)
                {
                    var source = context.OrganizationSources.TryGetValue(org.ExternalId ?? string.Empty, out var orgSource) ? orgSource : null;
                    source ??= defaultSource;
                    var sql = GetInsertIgnoreSql("organizations", "external_id, code, name, source", "@ExternalId, @Code, @Name, @Source");
                    var rowsAffected = await connection.ExecuteAsync(sql,
                        new { org.ExternalId, org.Code, org.Name, Source = source });
                    result.OrganizationsImported += rowsAffected;
                }

                // Insert locations
                foreach (var loc in context.Locations)
                {
                    var source = context.LocationSources.TryGetValue(loc.ExternalId ?? string.Empty, out var locSource) ? locSource : null;
                    source ??= defaultSource;
                    var sql = GetInsertIgnoreSql("locations", "external_id, code, name, source", "@ExternalId, @Code, @Name, @Source");
                    var rowsAffected = await connection.ExecuteAsync(sql,
                        new { loc.ExternalId, loc.Code, loc.Name, Source = source });
                    result.LocationsImported += rowsAffected;
                }

                // Insert employers
                Debug.WriteLine($"[Import CompanyOnly] About to insert {context.Employers.Count} employers:");
                foreach (var emp in context.Employers)
                {
                    var employerNameKey = $"{emp.ExternalId ?? string.Empty}|{emp.Name ?? string.Empty}";
                    context.EmployerNameToSourceMap.TryGetValue(employerNameKey, out var source);
                    source ??= defaultSource;
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
                    source ??= defaultSource;
                    var sql = GetInsertIgnoreSql("cost_centers", "external_id, code, name, source", "@ExternalId, @Code, @Name, @Source");
                    var rowsAffected = await connection.ExecuteAsync(sql,
                        new { cc.ExternalId, cc.Code, cc.Name, Source = source });
                    result.CostCentersImported += rowsAffected;
                }

                // Insert cost bearers
                foreach (var cb in context.CostBearers)
                {
                    var source = context.CostBearerSources.TryGetValue(cb.ExternalId ?? string.Empty, out var cbSource) ? cbSource : null;
                    source ??= defaultSource;
                    var sql = GetInsertIgnoreSql("cost_bearers", "external_id, code, name, source", "@ExternalId, @Code, @Name, @Source");
                    var rowsAffected = await connection.ExecuteAsync(sql,
                        new { cb.ExternalId, cb.Code, cb.Name, Source = source });
                    result.CostBearersImported += rowsAffected;
                }

                // Insert teams
                foreach (var team in context.Teams)
                {
                    var source = context.TeamSources.TryGetValue(team.ExternalId ?? string.Empty, out var teamSource) ? teamSource : null;
                    source ??= defaultSource;
                    var sql = GetInsertIgnoreSql("teams", "external_id, code, name, source", "@ExternalId, @Code, @Name, @Source");
                    var rowsAffected = await connection.ExecuteAsync(sql,
                        new { team.ExternalId, team.Code, team.Name, Source = source });
                    result.TeamsImported += rowsAffected;
                }

                // Insert divisions
                foreach (var div in context.Divisions)
                {
                    var source = context.DivisionSources.TryGetValue(div.ExternalId ?? string.Empty, out var divSource) ? divSource : null;
                    source ??= defaultSource;
                    var sql = GetInsertIgnoreSql("divisions", "external_id, code, name, source", "@ExternalId, @Code, @Name, @Source");
                    var rowsAffected = await connection.ExecuteAsync(sql,
                        new { div.ExternalId, div.Code, div.Name, Source = source });
                    result.DivisionsImported += rowsAffected;
                }

                // Insert titles
                foreach (var title in context.Titles)
                {
                    var source = context.TitleSources.TryGetValue(title.ExternalId ?? string.Empty, out var titleSource) ? titleSource : null;
                    source ??= defaultSource;
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

            // Step 6: Import custom field schemas (schemas only, not values)
            if (vaultData.Persons != null && vaultData.Persons.Count > 0)
            {
                await ImportCustomFieldSchemasAsync(vaultData, result, progress);
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


}
