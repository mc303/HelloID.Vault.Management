using System.Diagnostics;
using HelloID.Vault.Core.Models.Entities;
using HelloID.Vault.Data.Connection;
using HelloID.Vault.Data.Repositories.Interfaces;

namespace HelloID.Vault.Data.Repositories.Turso;

public class TursoOrganizationRepository : IOrganizationRepository
{
    private readonly ITursoClient _client;

    public TursoOrganizationRepository(ITursoClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    public async Task<IEnumerable<Organization>> GetAllAsync()
    {
        Debug.WriteLine("[TursoOrganizationRepository] GetAllAsync");
        var sql = "SELECT external_id AS ExternalId, code AS Code, name AS Name, source AS Source FROM organizations ORDER BY name";
        var result = await _client.QueryAsync<Organization>(sql);
        return result.Rows;
    }

    public async Task<Organization?> GetByIdAsync(string externalId, string source)
    {
        Debug.WriteLine($"[TursoOrganizationRepository] GetByIdAsync: {externalId}");
        var sql = "SELECT external_id AS ExternalId, code AS Code, name AS Name, source AS Source FROM organizations WHERE external_id = ? AND source = ?";
        return await _client.QueryFirstOrDefaultAsync<Organization>(sql, new { externalId, source });
    }

    public async Task<int> InsertAsync(Organization organization)
    {
        Debug.WriteLine($"[TursoOrganizationRepository] InsertAsync: {organization.ExternalId}");
        var sql = "INSERT INTO organizations (external_id, code, name, source) VALUES (?, ?, ?, ?)";
        return await _client.ExecuteAsync(sql, organization);
    }

    public async Task<int> UpdateAsync(Organization organization)
    {
        Debug.WriteLine($"[TursoOrganizationRepository] UpdateAsync: {organization.ExternalId}");
        var sql = "UPDATE organizations SET code = ?, name = ?, source = ? WHERE external_id = ? AND source = ?";
        return await _client.ExecuteAsync(sql, new { organization.Code, organization.Name, organization.Source, organization.ExternalId });
    }

    public async Task<int> DeleteAsync(string externalId, string source)
    {
        Debug.WriteLine($"[TursoOrganizationRepository] DeleteAsync: {externalId}");
        var sql = "DELETE FROM organizations WHERE external_id = ? AND source = ?";
        return await _client.ExecuteAsync(sql, new { externalId, source });
    }
}
