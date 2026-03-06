using System.Diagnostics;
using HelloID.Vault.Core.Models.Entities;
using HelloID.Vault.Data.Connection;
using HelloID.Vault.Data.Repositories.Interfaces;

namespace HelloID.Vault.Data.Repositories.Turso;

public class TursoCostCenterRepository : ICostCenterRepository
{
    private readonly ITursoClient _client;

    public TursoCostCenterRepository(ITursoClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    public async Task<IEnumerable<CostCenter>> GetAllAsync()
    {
        Debug.WriteLine("[TursoCostCenterRepository] GetAllAsync");
        var sql = "SELECT external_id AS ExternalId, code AS Code, name AS Name, source AS Source FROM cost_centers ORDER BY name";
        var result = await _client.QueryAsync<CostCenter>(sql);
        return result.Rows;
    }

    public async Task<CostCenter?> GetByIdAsync(string externalId, string source)
    {
        Debug.WriteLine($"[TursoCostCenterRepository] GetByIdAsync: {externalId}");
        var sql = "SELECT external_id AS ExternalId, code AS Code, name AS Name, source AS Source FROM cost_centers WHERE external_id = ? AND source = ?";
        return await _client.QueryFirstOrDefaultAsync<CostCenter>(sql, new { externalId, source });
    }

    public async Task<int> InsertAsync(CostCenter costCenter)
    {
        Debug.WriteLine($"[TursoCostCenterRepository] InsertAsync: {costCenter.ExternalId}");
        var sql = "INSERT INTO cost_centers (external_id, code, name, source) VALUES (?, ?, ?, ?)";
        return await _client.ExecuteAsync(sql, costCenter);
    }

    public async Task<int> UpdateAsync(CostCenter costCenter)
    {
        Debug.WriteLine($"[TursoCostCenterRepository] UpdateAsync: {costCenter.ExternalId}");
        var sql = "UPDATE cost_centers SET code = ?, name = ?, source = ? WHERE external_id = ? AND source = ?";
        return await _client.ExecuteAsync(sql, new { costCenter.Code, costCenter.Name, costCenter.Source, costCenter.ExternalId });
    }

    public async Task<int> DeleteAsync(string externalId, string source)
    {
        Debug.WriteLine($"[TursoCostCenterRepository] DeleteAsync: {externalId}");
        var sql = "DELETE FROM cost_centers WHERE external_id = ? AND source = ?";
        return await _client.ExecuteAsync(sql, new { externalId, source });
    }
}
