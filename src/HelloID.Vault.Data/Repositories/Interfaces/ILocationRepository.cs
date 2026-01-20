using HelloID.Vault.Core.Models.Entities;

namespace HelloID.Vault.Data.Repositories.Interfaces;

public interface ILocationRepository
{
    Task<IEnumerable<Location>> GetAllAsync();
    Task<Location?> GetByIdAsync(string externalId, string source);
    Task<int> InsertAsync(Location location);
    Task<int> UpdateAsync(Location location);
    Task<int> DeleteAsync(string externalId, string source);
}
