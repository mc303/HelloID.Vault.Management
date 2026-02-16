using System.Data;
using System.Diagnostics;
using Dapper;
using HelloID.Vault.Core.Models.Entities;
using HelloID.Vault.Data.Repositories.Interfaces;
using HelloID.Vault.Services.Import.Utilities;

namespace HelloID.Vault.Services.Import.Strategies;

/// <summary>
/// Base class for import strategies with common functionality.
/// </summary>
public abstract class BaseImportStrategy : IImportStrategy
{
    protected int _invalidDepartmentParents = 0;
    protected int _invalidManagerReferences = 0;

    public abstract string StrategyName { get; }

    public int InvalidDepartmentParents => _invalidDepartmentParents;
    public int InvalidManagerReferences => _invalidManagerReferences;

    public abstract Task PrepareForImportAsync(IDbConnection connection);
    public abstract Task CleanupAfterImportAsync(IDbConnection connection);
    public abstract Task<int> ImportDepartmentsAsync(
        IEnumerable<Department> departments,
        HashSet<string> validDepartmentKeys,
        IDepartmentRepository departmentRepository,
        IDbConnection connection,
        IDbTransaction transaction,
        Action<int>? progressCallback = null);

    public abstract Task<int> ImportPersonsAsync(
        IEnumerable<Person> persons,
        HashSet<string> validPersonIds,
        IPersonRepository personRepository,
        IDbConnection connection,
        IDbTransaction transaction,
        Action<int, int>? progressCallback = null);

    /// <summary>
    /// Performs topological sort on departments to ensure parents are inserted before children.
    /// </summary>
    protected List<Department> TopologicalSortDepartments(List<Department> departments)
    {
        return DepartmentSorter.TopologicalSort(departments);
    }

    /// <summary>
    /// Validates and fixes orphaned department parent references after import.
    /// </summary>
    protected async Task ValidateDepartmentParentsAsync(IDbConnection connection, IDbTransaction transaction)
    {
        var orphanedDepts = await connection.QueryAsync<(string ExternalId, string DisplayName, string ParentExternalId)>(@"
            SELECT d.external_id as ExternalId, d.display_name as DisplayName, d.parent_external_id as ParentExternalId
            FROM departments d
            LEFT JOIN departments p ON d.parent_external_id = p.external_id
            WHERE d.parent_external_id IS NOT NULL
              AND d.parent_external_id != ''
              AND p.external_id IS NULL", transaction: transaction);

        if (orphanedDepts.Any())
        {
            Debug.WriteLine($"[{StrategyName}] Found {orphanedDepts.Count()} orphaned departments, fixing...");
            foreach (var orphan in orphanedDepts)
            {
                Debug.WriteLine($"[{StrategyName}]   - {orphan.DisplayName} ({orphan.ExternalId}) has non-existent parent: {orphan.ParentExternalId}");

                await connection.ExecuteAsync(@"
                    UPDATE departments
                    SET parent_external_id = NULL
                    WHERE external_id = @ExternalId",
                    new { ExternalId = orphan.ExternalId },
                    transaction);
            }
        }
    }

    /// <summary>
    /// Validates and fixes invalid manager references after import.
    /// </summary>
    protected async Task ValidateDepartmentManagersAsync(IDbConnection connection, IDbTransaction transaction)
    {
        var invalidManagers = await connection.QueryAsync<(string ExternalId, string DisplayName, string ManagerPersonId)>(@"
            SELECT d.external_id as ExternalId, d.display_name as DisplayName, d.manager_person_id as ManagerPersonId
            FROM departments d
            LEFT JOIN persons p ON d.manager_person_id = p.person_id
            WHERE d.manager_person_id IS NOT NULL
              AND d.manager_person_id != ''
              AND p.person_id IS NULL", transaction: transaction);

        if (invalidManagers.Any())
        {
            Debug.WriteLine($"[{StrategyName}] Found {invalidManagers.Count()} departments with invalid managers, fixing...");
            foreach (var invalid in invalidManagers)
            {
                Debug.WriteLine($"[{StrategyName}]   - {invalid.DisplayName} ({invalid.ExternalId}) has non-existent manager: {invalid.ManagerPersonId}");

                await connection.ExecuteAsync(@"
                    UPDATE departments
                    SET manager_person_id = NULL
                    WHERE external_id = @ExternalId",
                    new { ExternalId = invalid.ExternalId },
                    transaction);
            }
        }
    }
}
