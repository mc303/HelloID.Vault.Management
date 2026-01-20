using Dapper;
using HelloID.Vault.Core.Models.Entities;
using HelloID.Vault.Core.Models.DTOs;
using HelloID.Vault.Core.Utilities;
using HelloID.Vault.Data.Connection;
using HelloID.Vault.Data.Repositories.Interfaces;

namespace HelloID.Vault.Data.Repositories;

/// <summary>
/// Repository implementation for SourceSystem entity using Dapper.
/// </summary>
public class SourceSystemRepository : ISourceSystemRepository
{
    private readonly ISqliteConnectionFactory _connectionFactory;

    public SourceSystemRepository(ISqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<IEnumerable<SourceSystemDto>> GetAllAsync()
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            SELECT
                system_id AS SystemId,
                display_name AS DisplayName,
                identification_key AS IdentificationKey,
                reference_count AS ReferenceCount,
                created_at AS CreatedAt
            FROM source_systems_view
            ORDER BY display_name";

        var results = await connection.QueryAsync<SourceSystemDto>(sql);

        // Compute hash prefix for each result
        foreach (var result in results)
        {
            result.HashPrefix = SourceHashProvider.GetSourceHash(result.SystemId);
        }

        return results;
    }

    public async Task<SourceSystemDto?> GetByIdAsync(string systemId)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            SELECT
                system_id AS SystemId,
                display_name AS DisplayName,
                identification_key AS IdentificationKey,
                reference_count AS ReferenceCount,
                created_at AS CreatedAt
            FROM source_systems_view
            WHERE system_id = @SystemId";

        var result = await connection.QuerySingleOrDefaultAsync<SourceSystemDto>(sql, new { SystemId = systemId });

        if (result != null)
        {
            result.HashPrefix = SourceHashProvider.GetSourceHash(result.SystemId);
        }

        return result;
    }

    public async Task<SourceSystemDto?> GetByHashPrefixAsync(string hashPrefix)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            SELECT
                system_id AS SystemId,
                display_name AS DisplayName,
                identification_key AS IdentificationKey,
                reference_count AS ReferenceCount,
                created_at AS CreatedAt
            FROM source_systems_view
            ORDER BY display_name";

        var allResults = await connection.QueryAsync<SourceSystemDto>(sql);

        // Compute hash for each and find the match
        foreach (var result in allResults)
        {
            result.HashPrefix = SourceHashProvider.GetSourceHash(result.SystemId);
            if (result.HashPrefix == hashPrefix)
            {
                return result;
            }
        }

        return null;
    }

    public async Task<IEnumerable<SourceSystemDto>> GetUnusedAsync()
    {
        using var connection = _connectionFactory.CreateConnection();

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

        var results = await connection.QueryAsync<SourceSystemDto>(sql);

        // Compute hash prefix for each result
        foreach (var result in results)
        {
            result.HashPrefix = SourceHashProvider.GetSourceHash(result.SystemId);
        }

        return results;
    }

    public async Task<IEnumerable<SourceSystemDto>> GetMostUsedAsync(int limit = 10)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            SELECT
                system_id AS SystemId,
                display_name AS DisplayName,
                identification_key AS IdentificationKey,
                reference_count AS ReferenceCount,
                created_at AS CreatedAt
            FROM source_systems_view
            ORDER BY reference_count DESC
            LIMIT @Limit";

        var results = await connection.QueryAsync<SourceSystemDto>(sql, new { Limit = limit });

        // Compute hash prefix for each result
        foreach (var result in results)
        {
            result.HashPrefix = SourceHashProvider.GetSourceHash(result.SystemId);
        }

        return results;
    }

    public async Task<int> InsertAsync(SourceSystem sourceSystem)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            INSERT OR IGNORE INTO source_system (system_id, display_name, identification_key)
            VALUES (@SystemId, @DisplayName, @IdentificationKey)";

        return await connection.ExecuteAsync(sql, sourceSystem);
    }

    public async Task<int> InsertAsync(SourceSystem sourceSystem, System.Data.IDbConnection connection, System.Data.IDbTransaction transaction)
    {
        var sql = @"
            INSERT OR IGNORE INTO source_system (system_id, display_name, identification_key)
            VALUES (@SystemId, @DisplayName, @IdentificationKey)";

        return await connection.ExecuteAsync(sql, sourceSystem, transaction);
    }

    public async Task<int> UpdateAsync(SourceSystem sourceSystem)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            UPDATE source_system
            SET display_name = @DisplayName,
                identification_key = @IdentificationKey
            WHERE system_id = @SystemId";

        return await connection.ExecuteAsync(sql, sourceSystem);
    }

    public async Task<int> DeleteAsync(string systemId)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = "DELETE FROM source_system WHERE system_id = @SystemId";

        return await connection.ExecuteAsync(sql, new { SystemId = systemId });
    }

    public async Task<int> GetCountAsync()
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = "SELECT COUNT(*) FROM source_system";

        return await connection.ExecuteScalarAsync<int>(sql);
    }

    public async Task<bool> ExistsAsync(string systemId)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = "SELECT COUNT(*) FROM source_system WHERE system_id = @SystemId";

        var count = await connection.ExecuteScalarAsync<int>(sql, new { SystemId = systemId });

        return count > 0;
    }

    public async Task<Dictionary<string, int>> GetUsageStatisticsAsync()
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            SELECT
                ss.display_name,
                ss.reference_count
            FROM source_systems_view ss
            ORDER BY ss.reference_count DESC";

        var results = await connection.QueryAsync<(string DisplayName, int ReferenceCount)>(sql);

        return results.ToDictionary(x => x.DisplayName, x => x.ReferenceCount);
    }
}