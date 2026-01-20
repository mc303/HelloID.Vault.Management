using HelloID.Vault.Core.Models.Entities;

namespace HelloID.Vault.Data.Repositories.Interfaces;

public interface ICostBearerRepository
{
    Task<IEnumerable<CostBearer>> GetAllAsync();
    Task<CostBearer?> GetByIdAsync(string externalId, string source);
    Task<int> InsertAsync(CostBearer costBearer);
    Task<int> UpdateAsync(CostBearer costBearer);
    Task<int> DeleteAsync(string externalId, string source);
}
