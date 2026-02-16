using Dapper;
using HelloID.Vault.Core.Models.Entities;
using HelloID.Vault.Core.Models.DTOs;
using HelloID.Vault.Data.Connection;
using HelloID.Vault.Data.Repositories.Interfaces;
using System.Data;

namespace HelloID.Vault.Data.Repositories.Base;

/// <summary>
/// Abstract base repository for SourceSystem operations.
/// Contains shared logic and defines database-specific abstract methods.
/// </summary>
public abstract class AbstractSourceSystemRepository : ISourceSystemRepository
{
    private readonly IDatabaseConnectionFactory _connectionFactory;

    protected AbstractSourceSystemRepository(IDatabaseConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    /// <summary>
    /// Gets the database connection factory.
    /// </summary>
    protected IDatabaseConnectionFactory ConnectionFactory => _connectionFactory;

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

        var results = await connection.QueryAsync<SourceSystemDto>(sql).ConfigureAwait(false);

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

        var result = await connection.QuerySingleOrDefaultAsync<SourceSystemDto>(sql, new { SystemId = systemId }).ConfigureAwait(false);

        return result;
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

        var results = await connection.QueryAsync<SourceSystemDto>(sql).ConfigureAwait(false);

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

        var results = await connection.QueryAsync<SourceSystemDto>(sql, new { Limit = limit }).ConfigureAwait(false);

        return results;
    }

    public async Task<int> InsertAsync(SourceSystem sourceSystem)
    {
        using var connection = _connectionFactory.CreateConnection();

        return await InsertInternalAsync(sourceSystem, connection).ConfigureAwait(false);
    }

    public async Task<int> InsertAsync(SourceSystem sourceSystem, IDbConnection connection, IDbTransaction transaction)
    {
        return await InsertInternalAsync(sourceSystem, connection, transaction).ConfigureAwait(false);
    }

    public async Task<int> UpdateAsync(SourceSystem sourceSystem)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            UPDATE source_system
            SET display_name = @DisplayName,
                identification_key = @IdentificationKey
            WHERE system_id = @SystemId";

        return await connection.ExecuteAsync(sql, sourceSystem).ConfigureAwait(false);
    }

    public async Task<int> DeleteAsync(string systemId)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = "DELETE FROM source_system WHERE system_id = @SystemId";

        return await connection.ExecuteAsync(sql, new { SystemId = systemId }).ConfigureAwait(false);
    }

    public async Task<int> GetCountAsync()
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = "SELECT COUNT(*) FROM source_system";

        return await connection.ExecuteScalarAsync<int>(sql).ConfigureAwait(false);
    }

    public async Task<bool> ExistsAsync(string systemId)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = "SELECT COUNT(*) FROM source_system WHERE system_id = @SystemId";

        var count = await connection.ExecuteScalarAsync<int>(sql, new { SystemId = systemId }).ConfigureAwait(false);

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

        var results = await connection.QueryAsync<(string DisplayName, int ReferenceCount)>(sql).ConfigureAwait(false);

        return results.ToDictionary(x => x.DisplayName, x => x.ReferenceCount);
    }

    /// <summary>
    /// Database-specific implementation for inserting a source system.
    /// </summary>
    protected abstract Task<int> InsertInternalAsync(SourceSystem sourceSystem, IDbConnection connection, IDbTransaction? transaction = null);
}
