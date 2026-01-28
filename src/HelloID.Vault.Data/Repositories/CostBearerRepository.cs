using Dapper;
using HelloID.Vault.Core.Models.Entities;
using HelloID.Vault.Data.Connection;
using HelloID.Vault.Data.Repositories.Interfaces;

namespace HelloID.Vault.Data.Repositories;

public class CostBearerRepository : ICostBearerRepository
{
    private readonly ISqliteConnectionFactory _connectionFactory;

    public CostBearerRepository(ISqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<IEnumerable<CostBearer>> GetAllAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        var sql = "SELECT external_id AS ExternalId, code AS Code, name AS Name, source AS Source FROM cost_bearers ORDER BY name";
        return await connection.QueryAsync<CostBearer>(sql).ConfigureAwait(false);
    }

    public async Task<CostBearer?> GetByIdAsync(string externalId, string source)
    {
        using var connection = _connectionFactory.CreateConnection();
        var sql = "SELECT external_id AS ExternalId, code AS Code, name AS Name, source AS Source FROM cost_bearers WHERE external_id = @ExternalId AND source = @Source";
        return await connection.QuerySingleOrDefaultAsync<CostBearer>(sql, new { ExternalId = externalId, Source = source }).ConfigureAwait(false);
    }

    public async Task<int> InsertAsync(CostBearer costBearer)
    {
        using var connection = _connectionFactory.CreateConnection();
        var sql = "INSERT INTO cost_bearers (external_id, code, name, source) VALUES (@ExternalId, @Code, @Name, @Source)";
        return await connection.ExecuteAsync(sql, costBearer).ConfigureAwait(false);
    }

    public async Task<int> UpdateAsync(CostBearer costBearer)
    {
        using var connection = _connectionFactory.CreateConnection();
        var sql = "UPDATE cost_bearers SET code = @Code, name = @Name, source = @Source WHERE external_id = @ExternalId AND source = @Source";
        return await connection.ExecuteAsync(sql, costBearer).ConfigureAwait(false);
    }

    public async Task<int> DeleteAsync(string externalId, string source)
    {
        using var connection = _connectionFactory.CreateConnection();
        var sql = "DELETE FROM cost_bearers WHERE external_id = @ExternalId AND source = @Source";
        return await connection.ExecuteAsync(sql, new { ExternalId = externalId, Source = source }).ConfigureAwait(false);
    }
}
