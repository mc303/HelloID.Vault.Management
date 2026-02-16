using System.Data;
using System.Diagnostics;
using Dapper;
using HelloID.Vault.Core.Models.Entities;
using HelloID.Vault.Data.Repositories.Interfaces;

namespace HelloID.Vault.Services.Import.Strategies;

/// <summary>
/// Import strategy for SQLite databases.
/// Uses PRAGMA foreign_keys = OFF/ON to disable FK constraints during bulk import.
/// </summary>
public class SqliteImportStrategy : BaseImportStrategy
{
    public override string StrategyName => "SQLite (PRAGMA foreign_keys)";

    public override Task PrepareForImportAsync(IDbConnection connection)
    {
        // SQLite: Disable FK constraints for the session
        Debug.WriteLine($"[{StrategyName}] Disabling foreign key constraints...");
        return Task.CompletedTask; // FK disabled via connection factory enforceForeignKeys: false
    }

    public override Task CleanupAfterImportAsync(IDbConnection connection)
    {
        // SQLite: Re-enable FK constraints
        Debug.WriteLine($"[{StrategyName}] Re-enabling foreign key constraints...");
        return Task.CompletedTask; // FK re-enabled by caller
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

        // Validate and fix any orphaned parent references
        await ValidateDepartmentParentsAsync(connection, transaction);

        // Validate manager references
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

        // Validate and fix any orphaned manager references in persons table
        // (primary_manager_person_id is a self-reference that can point to non-existent persons)
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
            Debug.WriteLine($"[{StrategyName}] Found {invalidManagers.Count()} persons with invalid managers, fixing...");
            foreach (var invalid in invalidManagers)
            {
                Debug.WriteLine($"[{StrategyName}]   - {invalid.DisplayName} ({invalid.PersonId}) has non-existent manager: {invalid.ManagerPersonId}");

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
