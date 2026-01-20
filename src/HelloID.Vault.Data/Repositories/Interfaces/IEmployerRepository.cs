using HelloID.Vault.Core.Models.Entities;

namespace HelloID.Vault.Data.Repositories.Interfaces;

public interface IEmployerRepository
{
    Task<IEnumerable<Employer>> GetAllAsync();
    Task<Employer?> GetByIdAsync(string externalId, string source);
    Task<int> InsertAsync(Employer employer);
    Task<int> UpdateAsync(Employer employer);
    Task<int> DeleteAsync(string externalId, string source);
}
