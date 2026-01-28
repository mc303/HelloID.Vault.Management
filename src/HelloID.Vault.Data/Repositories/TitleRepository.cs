using Dapper;
using HelloID.Vault.Core.Models.Entities;
using HelloID.Vault.Data.Connection;
using HelloID.Vault.Data.Repositories.Interfaces;

namespace HelloID.Vault.Data.Repositories;

public class TitleRepository : ITitleRepository
{
    private readonly ISqliteConnectionFactory _connectionFactory;

    public TitleRepository(ISqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<IEnumerable<Title>> GetAllAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        var sql = "SELECT external_id AS ExternalId, code AS Code, name AS Name, source AS Source FROM titles ORDER BY name";
        return await connection.QueryAsync<Title>(sql).ConfigureAwait(false);
    }

    public async Task<Title?> GetByIdAsync(string externalId, string source)
    {
        using var connection = _connectionFactory.CreateConnection();
        var sql = "SELECT external_id AS ExternalId, code AS Code, name AS Name, source AS Source FROM titles WHERE external_id = @ExternalId AND source = @Source";
        return await connection.QuerySingleOrDefaultAsync<Title>(sql, new { ExternalId = externalId, Source = source }).ConfigureAwait(false);
    }

    public async Task<int> InsertAsync(Title title)
    {
        using var connection = _connectionFactory.CreateConnection();
        var sql = "INSERT INTO titles (external_id, code, name, source) VALUES (@ExternalId, @Code, @Name, @Source)";
        return await connection.ExecuteAsync(sql, title).ConfigureAwait(false);
    }

    public async Task<int> UpdateAsync(Title title)
    {
        using var connection = _connectionFactory.CreateConnection();
        var sql = "UPDATE titles SET code = @Code, name = @Name, source = @Source WHERE external_id = @ExternalId AND source = @Source";
        return await connection.ExecuteAsync(sql, title).ConfigureAwait(false);
    }

    public async Task<int> DeleteAsync(string externalId, string source)
    {
        using var connection = _connectionFactory.CreateConnection();
        var sql = "DELETE FROM titles WHERE external_id = @ExternalId AND source = @Source";
        return await connection.ExecuteAsync(sql, new { ExternalId = externalId, Source = source }).ConfigureAwait(false);
    }
}
