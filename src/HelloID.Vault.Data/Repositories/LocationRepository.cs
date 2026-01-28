using Dapper;
using HelloID.Vault.Core.Models.Entities;
using HelloID.Vault.Data.Connection;
using HelloID.Vault.Data.Repositories.Interfaces;

namespace HelloID.Vault.Data.Repositories;

public class LocationRepository : ILocationRepository
{
    private readonly ISqliteConnectionFactory _connectionFactory;

    public LocationRepository(ISqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<IEnumerable<Location>> GetAllAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        var sql = "SELECT external_id AS ExternalId, code AS Code, name AS Name, source AS Source FROM locations ORDER BY name";
        return await connection.QueryAsync<Location>(sql).ConfigureAwait(false);
    }

    public async Task<Location?> GetByIdAsync(string externalId, string source)
    {
        using var connection = _connectionFactory.CreateConnection();
        var sql = "SELECT external_id AS ExternalId, code AS Code, name AS Name, source AS Source FROM locations WHERE external_id = @ExternalId AND source = @Source";
        return await connection.QuerySingleOrDefaultAsync<Location>(sql, new { ExternalId = externalId, Source = source }).ConfigureAwait(false);
    }

    public async Task<int> InsertAsync(Location location)
    {
        using var connection = _connectionFactory.CreateConnection();
        var sql = "INSERT INTO locations (external_id, code, name, source) VALUES (@ExternalId, @Code, @Name, @Source)";
        return await connection.ExecuteAsync(sql, location).ConfigureAwait(false);
    }

    public async Task<int> UpdateAsync(Location location)
    {
        using var connection = _connectionFactory.CreateConnection();
        var sql = "UPDATE locations SET code = @Code, name = @Name, source = @Source WHERE external_id = @ExternalId AND source = @Source";
        return await connection.ExecuteAsync(sql, location).ConfigureAwait(false);
    }

    public async Task<int> DeleteAsync(string externalId, string source)
    {
        using var connection = _connectionFactory.CreateConnection();
        var sql = "DELETE FROM locations WHERE external_id = @ExternalId AND source = @Source";
        return await connection.ExecuteAsync(sql, new { ExternalId = externalId, Source = source }).ConfigureAwait(false);
    }
}
