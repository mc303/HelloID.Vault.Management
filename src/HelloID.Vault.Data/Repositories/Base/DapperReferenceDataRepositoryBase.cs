using Dapper;
using HelloID.Vault.Core.Models.Entities;
using HelloID.Vault.Data.Connection;

namespace HelloID.Vault.Data.Repositories.Base;

/// <summary>
/// Executes the shared reference data SQL through Dapper on a new connection
/// from the factory (SQLite and PostgreSQL backends).
/// </summary>
public abstract class DapperReferenceDataRepositoryBase<TEntity> : ReferenceDataRepositoryBase<TEntity>
    where TEntity : class, IReferenceDataEntity
{
    private readonly IDatabaseConnectionFactory _connectionFactory;

    protected DapperReferenceDataRepositoryBase(IDatabaseConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    protected override async Task<IEnumerable<TResult>> QueryRowsAsync<TResult>(string sql, object? parameters = null)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QueryAsync<TResult>(sql, parameters).ConfigureAwait(false);
    }

    protected override async Task<TResult?> QueryFirstRowAsync<TResult>(string sql, object? parameters = null) where TResult : class
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QueryFirstOrDefaultAsync<TResult>(sql, parameters).ConfigureAwait(false);
    }

    protected override async Task<int> ExecuteRowsAsync(string sql, object? parameters = null)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.ExecuteAsync(sql, parameters).ConfigureAwait(false);
    }

    protected override async Task<TResult> ExecuteScalarValueAsync<TResult>(string sql, object? parameters = null)
    {
        using var connection = _connectionFactory.CreateConnection();
        var result = await connection.ExecuteScalarAsync<TResult>(sql, parameters).ConfigureAwait(false);
        return result ?? throw new InvalidOperationException("Query returned no value.");
    }
}
