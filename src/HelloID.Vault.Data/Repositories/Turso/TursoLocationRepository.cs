using System.Diagnostics;
using HelloID.Vault.Core.Models.Entities;
using HelloID.Vault.Data.Connection;
using HelloID.Vault.Data.Repositories.Interfaces;

namespace HelloID.Vault.Data.Repositories.Turso;

public class TursoLocationRepository : ILocationRepository
{
    private readonly ITursoClient _client;

    public TursoLocationRepository(ITursoClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    public async Task<IEnumerable<Location>> GetAllAsync()
    {
        Debug.WriteLine("[TursoLocationRepository] GetAllAsync");
        var sql = "SELECT external_id AS ExternalId, code AS Code, name AS Name, source AS Source FROM locations ORDER BY name";
        var result = await _client.QueryAsync<Location>(sql);
        return result.Rows;
    }

    public async Task<Location?> GetByIdAsync(string externalId, string source)
    {
        Debug.WriteLine($"[TursoLocationRepository] GetByIdAsync: {externalId}");
        var sql = "SELECT external_id AS ExternalId, code AS Code, name AS Name, source AS Source FROM locations WHERE external_id = ? AND source = ?";
        return await _client.QueryFirstOrDefaultAsync<Location>(sql, new { externalId, source });
    }

    public async Task<int> InsertAsync(Location location)
    {
        Debug.WriteLine($"[TursoLocationRepository] InsertAsync: {location.ExternalId}");
        var sql = "INSERT INTO locations (external_id, code, name, source) VALUES (?, ?, ?, ?)";
        return await _client.ExecuteAsync(sql, location);
    }

    public async Task<int> UpdateAsync(Location location)
    {
        Debug.WriteLine($"[TursoLocationRepository] UpdateAsync: {location.ExternalId}");
        var sql = "UPDATE locations SET code = ?, name = ?, source = ? WHERE external_id = ? AND source = ?";
        return await _client.ExecuteAsync(sql, new { location.Code, location.Name, location.Source, location.ExternalId });
    }

    public async Task<int> DeleteAsync(string externalId, string source)
    {
        Debug.WriteLine($"[TursoLocationRepository] DeleteAsync: {externalId}");
        var sql = "DELETE FROM locations WHERE external_id = ? AND source = ?";
        return await _client.ExecuteAsync(sql, new { externalId, source });
    }
}
