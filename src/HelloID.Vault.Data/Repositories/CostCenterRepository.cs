using Dapper;
using HelloID.Vault.Core.Models.Entities;
using HelloID.Vault.Data.Connection;
using HelloID.Vault.Data.Repositories.Interfaces;

namespace HelloID.Vault.Data.Repositories;

public class CostCenterRepository : ICostCenterRepository
{
    private readonly ISqliteConnectionFactory _connectionFactory;

    public CostCenterRepository(ISqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<IEnumerable<CostCenter>> GetAllAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        var sql = "SELECT external_id AS ExternalId, code AS Code, name AS Name, source AS Source FROM cost_centers ORDER BY name";
        return await connection.QueryAsync<CostCenter>(sql);
    }

    public async Task<CostCenter?> GetByIdAsync(string externalId, string source)
    {
        using var connection = _connectionFactory.CreateConnection();
        var sql = "SELECT external_id AS ExternalId, code AS Code, name AS Name, source AS Source FROM cost_centers WHERE external_id = @ExternalId AND source = @Source";
        return await connection.QuerySingleOrDefaultAsync<CostCenter>(sql, new { ExternalId = externalId, Source = source });
    }

    public async Task<int> InsertAsync(CostCenter costCenter)
    {
        using var connection = _connectionFactory.CreateConnection();
        var sql = "INSERT INTO cost_centers (external_id, code, name, source) VALUES (@ExternalId, @Code, @Name, @Source)";
        return await connection.ExecuteAsync(sql, costCenter);
    }

    public async Task<int> UpdateAsync(CostCenter costCenter)
    {
        using var connection = _connectionFactory.CreateConnection();
        var sql = "UPDATE cost_centers SET code = @Code, name = @Name, source = @Source WHERE external_id = @ExternalId AND source = @Source";
        return await connection.ExecuteAsync(sql, costCenter);
    }

    public async Task<int> DeleteAsync(string externalId, string source)
    {
        using var connection = _connectionFactory.CreateConnection();
        var sql = "DELETE FROM cost_centers WHERE external_id = @ExternalId AND source = @Source";
        return await connection.ExecuteAsync(sql, new { ExternalId = externalId, Source = source });
    }
}
