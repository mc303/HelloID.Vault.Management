using HelloID.Vault.Core.Models.Entities;

namespace HelloID.Vault.Data.Repositories.Interfaces;

public interface ITitleRepository
{
    Task<IEnumerable<Title>> GetAllAsync();
    Task<Title?> GetByIdAsync(string externalId, string source);
    Task<int> InsertAsync(Title title);
    Task<int> UpdateAsync(Title title);
    Task<int> DeleteAsync(string externalId, string source);
}
