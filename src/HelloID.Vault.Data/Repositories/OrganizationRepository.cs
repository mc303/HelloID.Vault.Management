using Dapper;
using HelloID.Vault.Core.Models.Entities;
using HelloID.Vault.Data.Connection;
using HelloID.Vault.Data.Repositories.Interfaces;

namespace HelloID.Vault.Data.Repositories;

public class OrganizationRepository : IOrganizationRepository
{
    private readonly ISqliteConnectionFactory _connectionFactory;

    public OrganizationRepository(ISqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<IEnumerable<Organization>> GetAllAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        var sql = "SELECT external_id AS ExternalId, code AS Code, name AS Name, source AS Source FROM organizations ORDER BY name";
        return await connection.QueryAsync<Organization>(sql).ConfigureAwait(false);
    }

    public async Task<Organization?> GetByIdAsync(string externalId, string source)
    {
        using var connection = _connectionFactory.CreateConnection();
        var sql = "SELECT external_id AS ExternalId, code AS Code, name AS Name, source AS Source FROM organizations WHERE external_id = @ExternalId AND source = @Source";
        return await connection.QuerySingleOrDefaultAsync<Organization>(sql, new { ExternalId = externalId, Source = source }).ConfigureAwait(false);
    }

    public async Task<int> InsertAsync(Organization organization)
    {
        using var connection = _connectionFactory.CreateConnection();
        var sql = "INSERT INTO organizations (external_id, code, name, source) VALUES (@ExternalId, @Code, @Name, @Source)";
        return await connection.ExecuteAsync(sql, organization).ConfigureAwait(false);
    }

    public async Task<int> UpdateAsync(Organization organization)
    {
        using var connection = _connectionFactory.CreateConnection();
        var sql = "UPDATE organizations SET code = @Code, name = @Name, source = @Source WHERE external_id = @ExternalId AND source = @Source";
        return await connection.ExecuteAsync(sql, organization).ConfigureAwait(false);
    }

    public async Task<int> DeleteAsync(string externalId, string source)
    {
        using var connection = _connectionFactory.CreateConnection();
        var sql = "DELETE FROM organizations WHERE external_id = @ExternalId AND source = @Source";
        return await connection.ExecuteAsync(sql, new { ExternalId = externalId, Source = source }).ConfigureAwait(false);
    }
}
