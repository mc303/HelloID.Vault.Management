using System.Data;
using HelloID.Vault.Core.Models.Entities;
using HelloID.Vault.Data.Repositories.Interfaces;

namespace HelloID.Vault.Services.Import.Strategies;

/// <summary>
/// Strategy interface for database-specific import handling.
/// Different databases have different capabilities for handling FK constraints during bulk imports.
/// </summary>
public interface IImportStrategy
{
    /// <summary>
    /// Gets the name of this strategy for logging purposes.
    /// </summary>
    string StrategyName { get; }

    /// <summary>
    /// Prepares the database connection for bulk import (disable FK constraints, etc.).
    /// Called BEFORE starting a transaction.
    /// </summary>
    Task PrepareForImportAsync(IDbConnection connection);

    /// <summary>
    /// Cleans up after import (re-enable FK constraints, etc.).
    /// Called AFTER transaction completes.
    /// </summary>
    Task CleanupAfterImportAsync(IDbConnection connection);

    /// <summary>
    /// Imports departments with appropriate FK constraint handling for this strategy.
    /// </summary>
    Task<int> ImportDepartmentsAsync(
        IEnumerable<Department> departments,
        HashSet<string> validDepartmentKeys,
        IDepartmentRepository departmentRepository,
        IDbConnection connection,
        IDbTransaction transaction,
        Action<int>? progressCallback = null);

    /// <summary>
    /// Imports persons with appropriate FK constraint handling for this strategy.
    /// </summary>
    Task<int> ImportPersonsAsync(
        IEnumerable<Person> persons,
        HashSet<string> validPersonIds,
        IPersonRepository personRepository,
        IDbConnection connection,
        IDbTransaction transaction,
        Action<int, int>? progressCallback = null);

    /// <summary>
    /// Gets the number of invalid department parent references (for reporting).
    /// </summary>
    int InvalidDepartmentParents { get; }

    /// <summary>
    /// Gets the number of invalid manager references (for reporting).
    /// </summary>
    int InvalidManagerReferences { get; }
}
