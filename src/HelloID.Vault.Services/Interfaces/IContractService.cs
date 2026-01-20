using HelloID.Vault.Core.Models.Entities;
using HelloID.Vault.Core.Models.DTOs;
using HelloID.Vault.Core.Models.Filters;

namespace HelloID.Vault.Services.Interfaces;

public interface IContractService
{
    Task<Contract?> GetByIdAsync(int contractId);
    Task<(IEnumerable<ContractDetailDto> items, int totalCount)> GetPagedAsync(ContractFilter filter, int page, int pageSize);

    /// <summary>
    /// Gets all contracts without pagination for in-memory filtering.
    /// </summary>
    Task<IEnumerable<ContractDetailDto>> GetAllAsync();

    Task<ContractJsonDto?> GetContractJsonByIdAsync(int contractId);
    Task<int> SaveAsync(Contract contract);
    Task DeleteAsync(int contractId);
    Task ValidateAsync(Contract contract);

    /// <summary>
    /// Gets all contracts from cache (fast, ~100ms).
    /// </summary>
    Task<IEnumerable<ContractDetailDto>> GetAllFromCacheAsync();

    /// <summary>
    /// Rebuilds the contract cache. Called automatically after Save/Delete.
    /// </summary>
    Task RebuildCacheAsync();

    /// <summary>
    /// Refreshes a single contract in cache (incremental update, ~5-10ms).
    /// </summary>
    Task RefreshContractCacheItemAsync(int contractId);

    /// <summary>
    /// Gets cache metadata.
    /// </summary>
    Task<CacheMetadata> GetCacheMetadataAsync();
}
