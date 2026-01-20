using HelloID.Vault.Core.Models.Entities;

namespace HelloID.Vault.Data.Repositories.Interfaces;

public interface IDivisionRepository
{
    Task<IEnumerable<Division>> GetAllAsync();
    Task<Division?> GetByIdAsync(string externalId, string source);
    Task<int> InsertAsync(Division division);
    Task<int> UpdateAsync(Division division);
    Task<int> DeleteAsync(string externalId, string source);
}
