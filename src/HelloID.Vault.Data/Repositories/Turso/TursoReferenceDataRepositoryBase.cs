using HelloID.Vault.Core.Models.Entities;
using HelloID.Vault.Data.Connection;

namespace HelloID.Vault.Data.Repositories.Turso;

/// <summary>
/// Executes the shared reference data SQL through the Turso HTTP pipeline.
/// </summary>
public abstract class TursoReferenceDataRepositoryBase<TEntity> : Base.ReferenceDataRepositoryBase<TEntity>
    where TEntity : class, IReferenceDataEntity
{
    protected readonly ITursoClient Client;

    protected TursoReferenceDataRepositoryBase(ITursoClient client)
    {
        Client = client ?? throw new ArgumentNullException(nameof(client));
    }

    protected override async Task<IEnumerable<TResult>> QueryRowsAsync<TResult>(string sql, object? parameters = null)
    {
        var result = await Client.QueryAsync<TResult>(sql, parameters);
        return result.Rows;
    }

    protected override async Task<TResult?> QueryFirstRowAsync<TResult>(string sql, object? parameters = null) where TResult : class
    {
        return await Client.QueryFirstOrDefaultAsync<TResult>(sql, parameters);
    }

    protected override async Task<int> ExecuteRowsAsync(string sql, object? parameters = null)
    {
        return await Client.ExecuteAsync(sql, parameters);
    }

    protected override async Task<TResult> ExecuteScalarValueAsync<TResult>(string sql, object? parameters = null)
    {
        var result = await Client.ExecuteScalarAsync<TResult>(sql, parameters);
        return result ?? throw new InvalidOperationException("Query returned no value.");
    }
}
