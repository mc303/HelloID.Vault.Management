using HelloID.Vault.Core.Models.Entities;

namespace HelloID.Vault.Data.Repositories.Interfaces;

public interface IOrganizationRepository
{
    Task<IEnumerable<Organization>> GetAllAsync();
    Task<Organization?> GetByIdAsync(string externalId, string source);
    Task<int> InsertAsync(Organization organization);
    Task<int> UpdateAsync(Organization organization);
    Task<int> DeleteAsync(string externalId, string source);
}
