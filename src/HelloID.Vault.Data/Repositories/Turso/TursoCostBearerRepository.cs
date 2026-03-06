using System.Diagnostics;
using HelloID.Vault.Core.Models.Entities;
using HelloID.Vault.Data.Connection;
using HelloID.Vault.Data.Repositories.Interfaces;

namespace HelloID.Vault.Data.Repositories.Turso;

public class TursoCostBearerRepository : ICostBearerRepository
{
    private readonly ITursoClient _client;

    public TursoCostBearerRepository(ITursoClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    public async Task<IEnumerable<CostBearer>> GetAllAsync()
    {
        Debug.WriteLine("[TursoCostBearerRepository] GetAllAsync");
        var sql = "SELECT external_id AS ExternalId, code AS Code, name AS Name, source AS Source FROM cost_bearers ORDER BY name";
        var result = await _client.QueryAsync<CostBearer>(sql);
        return result.Rows;
    }

    public async Task<CostBearer?> GetByIdAsync(string externalId, string source)
    {
        Debug.WriteLine($"[TursoCostBearerRepository] GetByIdAsync: {externalId}");
        var sql = "SELECT external_id AS ExternalId, code AS Code, name AS Name, source AS Source FROM cost_bearers WHERE external_id = ? AND source = ?";
        return await _client.QueryFirstOrDefaultAsync<CostBearer>(sql, new { externalId, source });
    }

    public async Task<int> InsertAsync(CostBearer costBearer)
    {
        Debug.WriteLine($"[TursoCostBearerRepository] InsertAsync: {costBearer.ExternalId}");
        var sql = "INSERT INTO cost_bearers (external_id, code, name, source) VALUES (?, ?, ?, ?)";
        return await _client.ExecuteAsync(sql, costBearer);
    }

    public async Task<int> UpdateAsync(CostBearer costBearer)
    {
        Debug.WriteLine($"[TursoCostBearerRepository] UpdateAsync: {costBearer.ExternalId}");
        var sql = "UPDATE cost_bearers SET code = ?, name = ?, source = ? WHERE external_id = ? AND source = ?";
        return await _client.ExecuteAsync(sql, new { costBearer.Code, costBearer.Name, costBearer.Source, costBearer.ExternalId });
    }

    public async Task<int> DeleteAsync(string externalId, string source)
    {
        Debug.WriteLine($"[TursoCostBearerRepository] DeleteAsync: {externalId}");
        var sql = "DELETE FROM cost_bearers WHERE external_id = ? AND source = ?";
        return await _client.ExecuteAsync(sql, new { externalId, source });
    }
}
