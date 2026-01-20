using Dapper;
using HelloID.Vault.Core.Models.Entities;
using HelloID.Vault.Data.Connection;
using HelloID.Vault.Data.Repositories.Interfaces;

namespace HelloID.Vault.Data.Repositories;

public class TeamRepository : ITeamRepository
{
    private readonly ISqliteConnectionFactory _connectionFactory;

    public TeamRepository(ISqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<IEnumerable<Team>> GetAllAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        var sql = "SELECT external_id AS ExternalId, code AS Code, name AS Name, source AS Source FROM teams ORDER BY name";
        return await connection.QueryAsync<Team>(sql);
    }

    public async Task<Team?> GetByIdAsync(string externalId, string source)
    {
        using var connection = _connectionFactory.CreateConnection();
        var sql = "SELECT external_id AS ExternalId, code AS Code, name AS Name, source AS Source FROM teams WHERE external_id = @ExternalId AND source = @Source";
        return await connection.QuerySingleOrDefaultAsync<Team>(sql, new { ExternalId = externalId, Source = source });
    }

    public async Task<int> InsertAsync(Team team)
    {
        using var connection = _connectionFactory.CreateConnection();
        var sql = "INSERT INTO teams (external_id, code, name, source) VALUES (@ExternalId, @Code, @Name, @Source)";
        return await connection.ExecuteAsync(sql, team);
    }

    public async Task<int> UpdateAsync(Team team)
    {
        using var connection = _connectionFactory.CreateConnection();
        var sql = "UPDATE teams SET code = @Code, name = @Name, source = @Source WHERE external_id = @ExternalId AND source = @Source";
        return await connection.ExecuteAsync(sql, team);
    }

    public async Task<int> DeleteAsync(string externalId, string source)
    {
        using var connection = _connectionFactory.CreateConnection();
        var sql = "DELETE FROM teams WHERE external_id = @ExternalId AND source = @Source";
        return await connection.ExecuteAsync(sql, new { ExternalId = externalId, Source = source });
    }
}
