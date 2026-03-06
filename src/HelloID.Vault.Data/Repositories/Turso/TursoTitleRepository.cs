using System.Diagnostics;
using HelloID.Vault.Core.Models.Entities;
using HelloID.Vault.Data.Connection;
using HelloID.Vault.Data.Repositories.Interfaces;

namespace HelloID.Vault.Data.Repositories.Turso;

public class TursoTitleRepository : ITitleRepository
{
    private readonly ITursoClient _client;

    public TursoTitleRepository(ITursoClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    public async Task<IEnumerable<Title>> GetAllAsync()
    {
        Debug.WriteLine("[TursoTitleRepository] GetAllAsync");
        var sql = "SELECT external_id AS ExternalId, code AS Code, name AS Name, source AS Source FROM titles ORDER BY name";
        var result = await _client.QueryAsync<Title>(sql);
        return result.Rows;
    }

    public async Task<Title?> GetByIdAsync(string externalId, string source)
    {
        Debug.WriteLine($"[TursoTitleRepository] GetByIdAsync: {externalId}");
        var sql = "SELECT external_id AS ExternalId, code AS Code, name AS Name, source AS Source FROM titles WHERE external_id = ? AND source = ?";
        return await _client.QueryFirstOrDefaultAsync<Title>(sql, new { externalId, source });
    }

    public async Task<int> InsertAsync(Title title)
    {
        Debug.WriteLine($"[TursoTitleRepository] InsertAsync: {title.ExternalId}");
        var sql = "INSERT INTO titles (external_id, code, name, source) VALUES (?, ?, ?, ?)";
        return await _client.ExecuteAsync(sql, title);
    }

    public async Task<int> UpdateAsync(Title title)
    {
        Debug.WriteLine($"[TursoTitleRepository] UpdateAsync: {title.ExternalId}");
        var sql = "UPDATE titles SET code = ?, name = ?, source = ? WHERE external_id = ? AND source = ?";
        return await _client.ExecuteAsync(sql, new { title.Code, title.Name, title.Source, title.ExternalId });
    }

    public async Task<int> DeleteAsync(string externalId, string source)
    {
        Debug.WriteLine($"[TursoTitleRepository] DeleteAsync: {externalId}");
        var sql = "DELETE FROM titles WHERE external_id = ? AND source = ?";
        return await _client.ExecuteAsync(sql, new { externalId, source });
    }
}
