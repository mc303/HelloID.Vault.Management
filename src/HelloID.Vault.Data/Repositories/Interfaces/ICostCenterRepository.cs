using HelloID.Vault.Core.Models.Entities;

namespace HelloID.Vault.Data.Repositories.Interfaces;

public interface ICostCenterRepository
{
    Task<IEnumerable<CostCenter>> GetAllAsync();
    Task<CostCenter?> GetByIdAsync(string externalId, string source);
    Task<int> InsertAsync(CostCenter costCenter);
    Task<int> UpdateAsync(CostCenter costCenter);
    Task<int> DeleteAsync(string externalId, string source);
}
