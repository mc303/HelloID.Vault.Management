using System.Data;
using HelloID.Vault.Core.Models.Entities;
using HelloID.Vault.Core.Models.DTOs;

namespace HelloID.Vault.Data.Repositories.Interfaces;

/// <summary>
/// Repository interface for Department entity operations.
/// </summary>
public interface IDepartmentRepository
{
    /// <summary>
    /// Gets all departments.
    /// </summary>
    Task<IEnumerable<DepartmentDto>> GetAllAsync();

    /// <summary>
    /// Gets a department by its external ID and source.
    /// </summary>
    Task<Department?> GetByIdAsync(string externalId, string source);

    /// <summary>
    /// Gets child departments for a parent department in the same source.
    /// </summary>
    Task<IEnumerable<DepartmentDto>> GetChildrenAsync(string parentExternalId, string source);

    /// <summary>
    /// Inserts a new department.
    /// </summary>
    Task<int> InsertAsync(Department department);

    /// <summary>
    /// Inserts a new department within a transaction.
    /// </summary>
    /// <param name="department">The department to insert.</param>
    /// <param name="connection">The database connection to use.</param>
    /// <param name="transaction">The transaction to participate in.</param>
    Task<int> InsertAsync(Department department, IDbConnection connection, IDbTransaction transaction);

    /// <summary>
    /// Inserts multiple departments within a transaction.
    /// </summary>
    /// <param name="departments">The departments to insert.</param>
    /// <param name="connection">The database connection to use.</param>
    /// <param name="transaction">The transaction to participate in.</param>
    Task<int> InsertBatchAsync(IEnumerable<Department> departments, IDbConnection connection, IDbTransaction transaction);

    /// <summary>
    /// Updates an existing department.
    /// </summary>
    Task<int> UpdateAsync(Department department);

    /// <summary>
    /// Deletes a department by external ID and source.
    /// </summary>
    Task<int> DeleteAsync(string externalId, string source);

    /// <summary>
    /// Gets paged departments, optionally filtered by source.
    /// </summary>
    Task<IEnumerable<DepartmentDto>> GetPagedAsync(int page, int pageSize, string? source = null);

    /// <summary>
    /// Gets the total count of departments.
    /// </summary>
    Task<int> GetCountAsync();
}
