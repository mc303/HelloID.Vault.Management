using HelloID.Vault.Core.Models.Entities;

namespace HelloID.Vault.Data.Repositories.Interfaces;

/// <summary>
/// Repository for managing primary contract configuration.
/// </summary>
public interface IPrimaryContractConfigRepository
{
    /// <summary>
    /// Gets all configuration entries (active and inactive).
    /// </summary>
    Task<IEnumerable<PrimaryContractConfig>> GetAllAsync();

    /// <summary>
    /// Gets only active configuration entries, ordered by priority.
    /// </summary>
    Task<IEnumerable<PrimaryContractConfig>> GetActiveConfigAsync();

    /// <summary>
    /// Updates the configuration (priority order, sort direction, active status).
    /// </summary>
    Task UpdateConfigAsync(IEnumerable<PrimaryContractConfig> configs);

    /// <summary>
    /// Adds new configuration entries.
    /// </summary>
    Task AddConfigAsync(IEnumerable<PrimaryContractConfig> configs);

    /// <summary>
    /// Deletes configuration entries by field name.
    /// </summary>
    Task DeleteConfigAsync(IEnumerable<string> fieldNames);

    /// <summary>
    /// Resets configuration to default values from Business-Logic-Rules.md.
    /// </summary>
    Task ResetToDefaultAsync();
}
