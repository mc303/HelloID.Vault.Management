using HelloID.Vault.Core.Models.Entities;
using HelloID.Vault.Core.Models.DTOs;
using HelloID.Vault.Core.Models.Filters;

namespace HelloID.Vault.Data.Repositories.Interfaces;

/// <summary>
/// Repository interface for Contract entity operations.
/// </summary>
public interface IContractRepository
{
    /// <summary>
    /// Gets a paged list of contracts based on filter criteria.
    /// </summary>
    Task<IEnumerable<ContractDto>> GetPagedAsync(ContractFilter filter, int page, int pageSize);

    /// <summary>
    /// Gets a paged list of contract details with all related entities from contract_details_view.
    /// </summary>
    Task<(IEnumerable<ContractDetailDto> items, int totalCount)> GetPagedDetailsAsync(ContractFilter filter, int page, int pageSize);

    /// <summary>
    /// Gets all contracts for a specific person.
    /// </summary>
    Task<IEnumerable<ContractDto>> GetByPersonIdAsync(string personId);

    /// <summary>
    /// Gets a contract by its ID.
    /// </summary>
    Task<Contract?> GetByIdAsync(int contractId);

    /// <summary>
    /// Gets a contract in JSON format from contract_json_view.
    /// </summary>
    Task<ContractJsonDto?> GetJsonViewByIdAsync(int contractId);

    /// <summary>
    /// Inserts a new contract.
    /// </summary>
    Task<int> InsertAsync(Contract contract);

    /// <summary>
    /// Inserts a new contract using the specified connection and transaction.
    /// </summary>
    Task<int> InsertAsync(Contract contract, System.Data.IDbConnection connection, System.Data.IDbTransaction transaction);

    /// <summary>
    /// Updates an existing contract.
    /// </summary>
    Task<int> UpdateAsync(Contract contract);

    /// <summary>
    /// Deletes a contract by ID.
    /// </summary>
    Task<int> DeleteAsync(int contractId);

    /// <summary>
    /// Gets the total count of contracts.
    /// </summary>
    Task<int> GetCountAsync(ContractFilter filter);

    /// <summary>
    /// Gets all contracts from contract_details_view without pagination.
    /// Use for in-memory filtering scenarios.
    /// </summary>
    Task<IEnumerable<ContractDetailDto>> GetAllDetailsAsync();

    /// <summary>
    /// Gets all contracts from the cached table (contract_details_cache).
    /// Provides 20-30x faster performance than GetAllDetailsAsync by eliminating JOINs.
    /// </summary>
    Task<IEnumerable<ContractDetailDto>> GetAllFromCacheAsync();

    /// <summary>
    /// Rebuilds the contract_details_cache table from contract_details_view.
    /// Call after any edit to contracts or related entities.
    /// </summary>
    Task RebuildCacheAsync();

    /// <summary>
    /// Gets cache metadata including last refresh time and row count.
    /// </summary>
    Task<CacheMetadata> GetCacheMetadataAsync();

    /// <summary>
    /// Refreshes a single contract in cache (incremental update).
    /// </summary>
    Task RefreshContractCacheItemAsync(int contractId);
}
