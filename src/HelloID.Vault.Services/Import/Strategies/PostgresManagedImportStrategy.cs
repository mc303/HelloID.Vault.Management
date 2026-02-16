using System.Data;
using System.Diagnostics;
using Dapper;
using HelloID.Vault.Core.Models.Entities;
using HelloID.Vault.Data.Repositories.Interfaces;

namespace HelloID.Vault.Services.Import.Strategies;

/// <summary>
/// Import strategy for managed PostgreSQL services without superuser privileges.
/// Uses two-pass import approach: INSERT with NULL FK refs → UPDATE refs after all entities exist.
/// Works with: Aiven, AWS RDS, Azure Database for PostgreSQL, Supabase (non-service role).
/// </summary>
public class PostgresManagedImportStrategy : BaseImportStrategy
{
    public override string StrategyName => "PostgreSQL Managed (Two-Pass)";

    public override Task PrepareForImportAsync(IDbConnection connection)
    {
        // Managed PostgreSQL: Cannot use session_replication_role (requires superuser)
        // Will use two-pass approach instead
        Debug.WriteLine($"[{StrategyName}] Using two-pass import (no superuser privileges available)");
        return Task.CompletedTask;
    }

    public override Task CleanupAfterImportAsync(IDbConnection connection)
    {
        // No cleanup needed for two-pass approach
        Debug.WriteLine($"[{StrategyName}] Import complete");
        return Task.CompletedTask;
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

        // Store original references for pass 2
        var parentRefs = new Dictionary<string, string?>();
        var managerRefs = new Dictionary<string, string?>();

        // === PASS 1: Insert all departments with NULL parent and manager references ===
        Debug.WriteLine($"[{StrategyName}] Pass 1: Inserting {sortedDepartments.Count} departments with NULL FK refs...");

        foreach (var dept in sortedDepartments)
        {
            // Store original references for pass 2
            parentRefs[dept.ExternalId] = dept.ParentExternalId;
            managerRefs[dept.ExternalId] = dept.ManagerPersonId;

            // Validate parent reference against import data
            if (!string.IsNullOrWhiteSpace(dept.ParentExternalId))
            {
                var parentKey = $"{dept.ParentExternalId}|{dept.Source}";
                if (!validDepartmentKeys.Contains(parentKey))
                {
                    Debug.WriteLine($"[{StrategyName}] Department {dept.DisplayName} ({dept.ExternalId}) has invalid parent {dept.ParentExternalId} - will be NULL");
                    _invalidDepartmentParents++;
                    parentRefs[dept.ExternalId] = null; // Don't try to update invalid refs
                }
            }

            // Create department with NULL references for pass 1
            var deptWithoutRefs = new Department
            {
                ExternalId = dept.ExternalId,
                DisplayName = dept.DisplayName,
                Code = dept.Code,
                ParentExternalId = null,  // NULL for pass 1
                ManagerPersonId = null,   // NULL for pass 1
                Source = dept.Source
            };

            await departmentRepository.InsertAsync(deptWithoutRefs, connection, transaction);
            count++;
            progressCallback?.Invoke(count);
        }

        // === PASS 2: Update parent references (all departments now exist) ===
        Debug.WriteLine($"[{StrategyName}] Pass 2: Updating parent references...");

        foreach (var kvp in parentRefs)
        {
            if (!string.IsNullOrWhiteSpace(kvp.Value))
            {
                await connection.ExecuteAsync(@"
                    UPDATE departments
                    SET parent_external_id = @ParentExternalId
                    WHERE external_id = @ExternalId",
                    new { ExternalId = kvp.Key, ParentExternalId = kvp.Value },
                    transaction);
            }
        }

        // === PASS 2b: Update manager references ===
        Debug.WriteLine($"[{StrategyName}] Pass 2b: Updating manager references...");

        foreach (var kvp in managerRefs)
        {
            if (!string.IsNullOrWhiteSpace(kvp.Value))
            {
                await connection.ExecuteAsync(@"
                    UPDATE departments
                    SET manager_person_id = @ManagerPersonId
                    WHERE external_id = @ExternalId",
                    new { ExternalId = kvp.Key, ManagerPersonId = kvp.Value },
                    transaction);
            }
        }

        // Final validation (detects any remaining orphaned refs)
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

        // === PASS 1: Insert all persons (no FK refs that need deferred) ===
        // Persons only have primary_manager_person_id which is a self-reference
        // Since we insert all persons before updating refs, this works with DEFERRABLE
        Debug.WriteLine($"[{StrategyName}] Inserting {personList.Count} persons...");

        foreach (var person in personList)
        {
            await personRepository.InsertAsync(person, connection, transaction);
            count++;
            progressCallback?.Invoke(count, personList.Count);
        }

        // Validate and fix any orphaned manager references
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
            _invalidManagerReferences += invalidManagers.Count();

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
