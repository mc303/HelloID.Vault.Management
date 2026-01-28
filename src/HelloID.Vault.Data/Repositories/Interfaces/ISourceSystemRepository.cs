using HelloID.Vault.Core.Models.Entities;
using HelloID.Vault.Core.Models.DTOs;

namespace HelloID.Vault.Data.Repositories.Interfaces;

/// <summary>
/// Repository interface for SourceSystem entity operations.
/// </summary>
public interface ISourceSystemRepository
{
    /// <summary>
    /// Gets all source systems with enhanced information.
    /// </summary>
    Task<IEnumerable<SourceSystemDto>> GetAllAsync();

    /// <summary>
    /// Gets a source system by its system ID.
    /// </summary>
    Task<SourceSystemDto?> GetByIdAsync(string systemId);

    /// <summary>
    /// Gets source systems with no references (unused systems).
    /// </summary>
    Task<IEnumerable<SourceSystemDto>> GetUnusedAsync();

    /// <summary>
    /// Gets source systems sorted by reference count (most used first).
    /// </summary>
    Task<IEnumerable<SourceSystemDto>> GetMostUsedAsync(int limit = 10);

    /// <summary>
    /// Inserts a new source system.
    /// </summary>
    Task<int> InsertAsync(SourceSystem sourceSystem);

    /// <summary>
    /// Inserts a new source system within a transaction.
    /// </summary>
    /// <param name="sourceSystem">The source system to insert.</param>
    /// <param name="connection">The database connection to use.</param>
    /// <param name="transaction">The transaction to participate in.</param>
    Task<int> InsertAsync(SourceSystem sourceSystem, System.Data.IDbConnection connection, System.Data.IDbTransaction transaction);

    /// <summary>
    /// Updates an existing source system.
    /// </summary>
    Task<int> UpdateAsync(SourceSystem sourceSystem);

    /// <summary>
    /// Deletes a source system by system ID.
    /// </summary>
    Task<int> DeleteAsync(string systemId);

    /// <summary>
    /// Gets the total count of source systems.
    /// </summary>
    Task<int> GetCountAsync();

    /// <summary>
    /// Checks if a source system exists by system ID.
    /// </summary>
    Task<bool> ExistsAsync(string systemId);

    /// <summary>
    /// Gets source system usage statistics.
    /// </summary>
    Task<Dictionary<string, int>> GetUsageStatisticsAsync();
}