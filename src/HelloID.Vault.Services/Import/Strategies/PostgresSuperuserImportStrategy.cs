using System.Data;
using System.Diagnostics;
using Dapper;
using HelloID.Vault.Core.Models.Entities;
using HelloID.Vault.Data.Repositories.Interfaces;
using Npgsql;

namespace HelloID.Vault.Services.Import.Strategies;

/// <summary>
/// Import strategy for PostgreSQL with superuser privileges.
/// Uses session_replication_role = 'replica' to disable FK constraint triggers during bulk import.
/// Works with: Self-hosted PostgreSQL, Supabase service role connection.
/// </summary>
public class PostgresSuperuserImportStrategy : BaseImportStrategy
{
    private bool _sessionReplicationRoleEnabled = false;

    public override string StrategyName => "PostgreSQL Superuser (session_replication_role)";

    public override async Task PrepareForImportAsync(IDbConnection connection)
    {
        // PostgreSQL: Try to disable FK constraint triggers via session_replication_role
        // This requires superuser privileges
        Debug.WriteLine($"[{StrategyName}] Attempting to SET session_replication_role = 'replica'...");

        try
        {
            await connection.ExecuteAsync("SET session_replication_role = 'replica'");
            _sessionReplicationRoleEnabled = true;
            Debug.WriteLine($"[{StrategyName}] SUCCESS - FK constraints disabled");
        }
        catch (PostgresException ex) when (ex.SqlState == "42501") // insufficient_privilege
        {
            Debug.WriteLine($"[{StrategyName}] PERMISSION DENIED - cannot set session_replication_role (requires superuser)");
            _sessionReplicationRoleEnabled = false;
            throw new InvalidOperationException(
                "PostgresSuperuserImportStrategy requires superuser privileges to set session_replication_role. " +
                "Use PostgresManagedImportStrategy for managed PostgreSQL services.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[{StrategyName}] ERROR setting session_replication_role: {ex.Message}");
            _sessionReplicationRoleEnabled = false;
            throw;
        }
    }

    public override async Task CleanupAfterImportAsync(IDbConnection connection)
    {
        // PostgreSQL: Re-enable FK constraint triggers
        if (_sessionReplicationRoleEnabled)
        {
            Debug.WriteLine($"[{StrategyName}] Resetting session_replication_role to 'origin'...");
            try
            {
                await connection.ExecuteAsync("SET session_replication_role = 'origin'");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[{StrategyName}] Warning: Could not reset session_replication_role: {ex.Message}");
            }
            finally
            {
                _sessionReplicationRoleEnabled = false;
            }
        }
    }

    public override async Task<int> ImportDepartmentsAsync(
        IEnumerable<Department> departments,
        HashSet<string> validDepartmentKeys,
        IDepartmentRepository departmentRepository,
        IDbConnection connection,
        IDbTransaction transaction,
        Action<int>? progressCallback = null)
    {
        var sortedDepartments = TopologicalSortDepartments(departments.ToList());
        var count = 0;

        foreach (var dept in sortedDepartments)
        {
            // Validate parent department reference
            if (!string.IsNullOrWhiteSpace(dept.ParentExternalId))
            {
                var parentKey = $"{dept.ParentExternalId}|{dept.Source}";
                if (!validDepartmentKeys.Contains(parentKey))
                {
                    Debug.WriteLine($"[{StrategyName}] Department {dept.DisplayName} ({dept.ExternalId}) has invalid parent {dept.ParentExternalId} - setting to NULL");
                    _invalidDepartmentParents++;
                    dept.ParentExternalId = null;
                }
            }

            await departmentRepository.InsertAsync(dept, connection, transaction);
            count++;
            progressCallback?.Invoke(count);
        }

        // With session_replication_role, FK constraints are disabled
        // but we still validate to report data quality issues
        await ValidateDepartmentParentsAsync(connection, transaction);
        await ValidateDepartmentManagersAsync(connection, transaction);

        return count;
    }

    public override async Task<int> ImportPersonsAsync(
        IEnumerable<Person> persons,
        HashSet<string> validPersonIds,
        IPersonRepository personRepository,
        IDbConnection connection,
        IDbTransaction transaction,
        Action<int, int>? progressCallback = null)
    {
        var personList = persons.ToList();
        var count = 0;

        foreach (var person in personList)
        {
            await personRepository.InsertAsync(person, connection, transaction);
            count++;
            progressCallback?.Invoke(count, personList.Count);
        }

        // With session_replication_role, FK constraints are disabled
        // but we validate to report data quality issues
        await ValidatePersonManagersAsync(connection, transaction);

        return count;
    }

    /// <summary>
    /// Validates and fixes invalid primary_manager_person_id references after import.
    /// </summary>
    private async Task ValidatePersonManagersAsync(IDbConnection connection, IDbTransaction transaction)
    {
        var invalidManagers = await connection.QueryAsync<(string PersonId, string DisplayName, string ManagerPersonId)>(@"
            SELECT p.person_id as PersonId, p.display_name as DisplayName, p.primary_manager_person_id as ManagerPersonId
            FROM persons p
            LEFT JOIN persons m ON p.primary_manager_person_id = m.person_id
            WHERE p.primary_manager_person_id IS NOT NULL
              AND p.primary_manager_person_id != ''
              AND m.person_id IS NULL", transaction: transaction);

        if (invalidManagers.Any())
        {
            Debug.WriteLine($"[{StrategyName}] Found {invalidManagers.Count()} persons with invalid managers (data quality issue)");
            _invalidManagerReferences += invalidManagers.Count();

            foreach (var invalid in invalidManagers)
            {
                Debug.WriteLine($"[{StrategyName}]   - {invalid.DisplayName} ({invalid.PersonId}) references non-existent manager: {invalid.ManagerPersonId}");

                await connection.ExecuteAsync(@"
                    UPDATE persons
                    SET primary_manager_person_id = NULL
                    WHERE person_id = @PersonId",
                    new { PersonId = invalid.PersonId },
                    transaction);
            }
        }
    }
}
