using System.Diagnostics;
using HelloID.Vault.Core.Models.Entities;
using HelloID.Vault.Data.Connection;
using HelloID.Vault.Data.Repositories.Interfaces;

namespace HelloID.Vault.Data.Repositories.Turso;

public class TursoDivisionRepository : IDivisionRepository
{
    private readonly ITursoClient _client;

    public TursoDivisionRepository(ITursoClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    public async Task<IEnumerable<Division>> GetAllAsync()
    {
        Debug.WriteLine("[TursoDivisionRepository] GetAllAsync");
        var sql = "SELECT external_id AS ExternalId, code AS Code, name AS Name, source AS Source FROM divisions ORDER BY name";
        var result = await _client.QueryAsync<Division>(sql);
        return result.Rows;
    }

    public async Task<Division?> GetByIdAsync(string externalId, string source)
    {
        Debug.WriteLine($"[TursoDivisionRepository] GetByIdAsync: {externalId}");
        var sql = "SELECT external_id AS ExternalId, code AS Code, name AS Name, source AS Source FROM divisions WHERE external_id = ? AND source = ?";
        return await _client.QueryFirstOrDefaultAsync<Division>(sql, new { externalId, source });
    }

    public async Task<int> InsertAsync(Division division)
    {
        Debug.WriteLine($"[TursoDivisionRepository] InsertAsync: {division.ExternalId}");
        var sql = "INSERT INTO divisions (external_id, code, name, source) VALUES (?, ?, ?, ?)";
        return await _client.ExecuteAsync(sql, division);
    }

    public async Task<int> UpdateAsync(Division division)
    {
        Debug.WriteLine($"[TursoDivisionRepository] UpdateAsync: {division.ExternalId}");
        var sql = "UPDATE divisions SET code = ?, name = ?, source = ? WHERE external_id = ? AND source = ?";
        return await _client.ExecuteAsync(sql, new { division.Code, division.Name, division.Source, division.ExternalId });
    }

    public async Task<int> DeleteAsync(string externalId, string source)
    {
        Debug.WriteLine($"[TursoDivisionRepository] DeleteAsync: {externalId}");
        var sql = "DELETE FROM divisions WHERE external_id = ? AND source = ?";
        return await _client.ExecuteAsync(sql, new { externalId, source });
    }
}
