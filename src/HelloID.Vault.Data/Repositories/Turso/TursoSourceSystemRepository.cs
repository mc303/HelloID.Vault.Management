using System.Data;
using System.Diagnostics;
using HelloID.Vault.Core.Models.Entities;
using HelloID.Vault.Core.Models.DTOs;
using HelloID.Vault.Data.Connection;
using HelloID.Vault.Data.Repositories.Interfaces;

namespace HelloID.Vault.Data.Repositories.Turso;

public class TursoSourceSystemRepository : ISourceSystemRepository
{
    private readonly ITursoClient _client;

    public TursoSourceSystemRepository(ITursoClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    public async Task<IEnumerable<SourceSystemDto>> GetAllAsync()
    {
        Debug.WriteLine("[TursoSourceSystemRepository] GetAllAsync");
        var sql = @"
            SELECT
                system_id AS SystemId,
                display_name AS DisplayName,
                identification_key AS IdentificationKey,
                reference_count AS ReferenceCount,
                created_at AS CreatedAt
            FROM source_systems_view
            ORDER BY display_name";
        var result = await _client.QueryAsync<SourceSystemDto>(sql);
        return result.Rows;
    }

    public async Task<SourceSystemDto?> GetByIdAsync(string systemId)
    {
        Debug.WriteLine($"[TursoSourceSystemRepository] GetByIdAsync: {systemId}");
        var sql = @"
            SELECT
                system_id AS SystemId,
                display_name AS DisplayName,
                identification_key AS IdentificationKey,
                reference_count AS ReferenceCount,
                created_at AS CreatedAt
            FROM source_systems_view
            WHERE system_id = ?";
        return await _client.QueryFirstOrDefaultAsync<SourceSystemDto>(sql, new { systemId });
    }

    public async Task<IEnumerable<SourceSystemDto>> GetUnusedAsync()
    {
        Debug.WriteLine("[TursoSourceSystemRepository] GetUnusedAsync");
        var sql = @"
            SELECT
                system_id AS SystemId,
                display_name AS DisplayName,
                identification_key AS IdentificationKey,
                reference_count AS ReferenceCount,
                created_at AS CreatedAt
            FROM source_systems_view
            WHERE reference_count = 0
            ORDER BY display_name";
        var result = await _client.QueryAsync<SourceSystemDto>(sql);
        return result.Rows;
    }

    public async Task<IEnumerable<SourceSystemDto>> GetMostUsedAsync(int limit = 10)
    {
        Debug.WriteLine($"[TursoSourceSystemRepository] GetMostUsedAsync: {limit}");
        var sql = @"
            SELECT
                system_id AS SystemId,
                display_name AS DisplayName,
                identification_key AS IdentificationKey,
                reference_count AS ReferenceCount,
                created_at AS CreatedAt
            FROM source_systems_view
            ORDER BY reference_count DESC
            LIMIT ?";
        var result = await _client.QueryAsync<SourceSystemDto>(sql, new { limit });
        return result.Rows;
    }

    public async Task<int> InsertAsync(SourceSystem sourceSystem)
    {
        Debug.WriteLine($"[TursoSourceSystemRepository] InsertAsync: {sourceSystem.SystemId}");
        var sql = "INSERT INTO source_system (system_id, display_name, identification_key) VALUES (?, ?, ?)";
        return await _client.ExecuteAsync(sql, sourceSystem);
    }

    public async Task<int> InsertAsync(SourceSystem sourceSystem, IDbConnection connection, IDbTransaction transaction)
    {
        return await InsertAsync(sourceSystem);
    }

    public async Task<int> UpdateAsync(SourceSystem sourceSystem)
    {
        Debug.WriteLine($"[TursoSourceSystemRepository] UpdateAsync: {sourceSystem.SystemId}");
        var sql = @"
            UPDATE source_system
            SET display_name = ?,
                identification_key = ?
            WHERE system_id = ?";
        return await _client.ExecuteAsync(sql, new { sourceSystem.DisplayName, sourceSystem.IdentificationKey, sourceSystem.SystemId });
    }

    public async Task<int> DeleteAsync(string systemId)
    {
        Debug.WriteLine($"[TursoSourceSystemRepository] DeleteAsync: {systemId}");
        var sql = "DELETE FROM source_system WHERE system_id = ?";
        return await _client.ExecuteAsync(sql, new { systemId });
    }

    public async Task<int> GetCountAsync()
    {
        Debug.WriteLine("[TursoSourceSystemRepository] GetCountAsync");
        var sql = "SELECT COUNT(*) AS Count FROM source_system";
        var result = await _client.QueryFirstOrDefaultAsync<CountResult>(sql);
        return result?.Count ?? 0;
    }

    public async Task<bool> ExistsAsync(string systemId)
    {
        Debug.WriteLine($"[TursoSourceSystemRepository] ExistsAsync: {systemId}");
        var sql = "SELECT COUNT(*) AS Count FROM source_system WHERE system_id = ?";
        var result = await _client.QueryFirstOrDefaultAsync<CountResult>(sql, new { systemId });
        return (result?.Count ?? 0) > 0;
    }

    public async Task<Dictionary<string, int>> GetUsageStatisticsAsync()
    {
        Debug.WriteLine("[TursoSourceSystemRepository] GetUsageStatisticsAsync");
        var sql = @"
            SELECT
                ss.display_name AS DisplayName,
                ss.reference_count AS ReferenceCount
            FROM source_systems_view ss
            ORDER BY ss.reference_count DESC";
        var result = await _client.QueryAsync<UsageStat>(sql);
        return result.Rows.ToDictionary(x => x.DisplayName ?? string.Empty, x => x.ReferenceCount);
    }
}

internal class UsageStat
{
    public string? DisplayName { get; set; }
    public int ReferenceCount { get; set; }
}
