using HelloID.Vault.Core.Models.DTOs;
using HelloID.Vault.Core.Models.Entities;
using HelloID.Vault.Core.Models.Filters;
using HelloID.Vault.Data.Connection;
using HelloID.Vault.Data.Repositories.Base;
using HelloID.Vault.Data.Repositories.Interfaces;

namespace HelloID.Vault.Data.Repositories.Turso;

/// <summary>
/// Person repository for Turso: pipeline execution of the shared SQL.
/// The ambient-transaction insert falls back to the plain insert (Turso has
/// no ambient transactions over HTTP).
/// </summary>
public class TursoPersonRepository : AbstractPersonRepository, IPersonRepository
{
    private readonly ITursoClient _client;

    public TursoPersonRepository(ITursoClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    protected override async Task<IEnumerable<TResult>> QueryRowsAsync<TResult>(string sql, object? parameters = null)
    {
        var result = await _client.QueryAsync<TResult>(sql, parameters);
        return result.Rows;
    }

    protected override async Task<TResult?> QueryFirstRowAsync<TResult>(string sql, object? parameters = null) where TResult : class
    {
        return await _client.QueryFirstOrDefaultAsync<TResult>(sql, parameters);
    }

    protected override async Task<int> ExecuteRowsAsync(string sql, object? parameters = null)
    {
        return await _client.ExecuteAsync(sql, parameters);
    }

    protected override async Task<TResult> ExecuteScalarValueAsync<TResult>(string sql, object? parameters = null)
    {
        var result = await _client.ExecuteScalarAsync<TResult>(sql, parameters);
        return result ?? throw new InvalidOperationException("Query returned no value.");
    }
}
