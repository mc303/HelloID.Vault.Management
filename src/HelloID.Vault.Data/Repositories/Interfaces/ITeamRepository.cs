using HelloID.Vault.Core.Models.Entities;

namespace HelloID.Vault.Data.Repositories.Interfaces;

public interface ITeamRepository
{
    Task<IEnumerable<Team>> GetAllAsync();
    Task<Team?> GetByIdAsync(string externalId, string source);
    Task<int> InsertAsync(Team team);
    Task<int> UpdateAsync(Team team);
    Task<int> DeleteAsync(string externalId, string source);
}
