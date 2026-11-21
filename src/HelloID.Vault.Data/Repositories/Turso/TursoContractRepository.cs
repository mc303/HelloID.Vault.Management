using System.Diagnostics;
using HelloID.Vault.Core.Models.DTOs;
using HelloID.Vault.Core.Models.Entities;
using HelloID.Vault.Core.Models.Filters;
using HelloID.Vault.Data.Connection;
using HelloID.Vault.Data.Repositories.Base;
using HelloID.Vault.Data.Repositories.Interfaces;

namespace HelloID.Vault.Data.Repositories.Turso;

/// <summary>
/// Contract repository for Turso: pipeline execution of the shared SQL.
/// The cache rebuild runs as one atomic pipeline transaction; the
/// ambient-transaction insert falls back to the plain insert.
/// </summary>
public class TursoContractRepository : AbstractContractRepository, IContractRepository
{
    private readonly ITursoClient _client;

    public TursoContractRepository(ITursoClient client)
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

    private class CountResult
    {
        public int Count { get; set; }
    }

    /// <summary>
    /// Atomic rebuild in one pipeline transaction: readers never see an empty
    /// cache and a failure rolls back to the previous contents.
    /// </summary>
    public override async Task RebuildCacheAsync()
    {
        Debug.WriteLine("[TursoContractRepository] RebuildCacheAsync");
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var statements = new List<TursoStatement>
        {
            new() { Sql = CacheDeleteSql, WantRows = false },
            new() { Sql = CacheFillSql, WantRows = false }
        };

        await _client.ExecuteTransactionAsync(statements);

        var rowCount = await ExecuteScalarValueAsync<int>(CacheCountSql);

        stopwatch.Stop();

        await ExecuteRowsAsync(CacheMetadataUpdateSql, new
        {
            LastRefreshed = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
            RowCount = rowCount,
            RefreshDurationMs = stopwatch.ElapsedMilliseconds,
            CacheName = "contract_details_cache"
        });
    }

    public override async Task RefreshContractCacheItemAsync(int contractId)
    {
        Debug.WriteLine($"[TursoContractRepository] RefreshContractCacheItemAsync: {contractId}");
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        await ExecuteRowsAsync(
            "INSERT OR REPLACE INTO contract_details_cache SELECT * FROM contract_details_view WHERE contract_id = @ContractId",
            new { ContractId = contractId });

        stopwatch.Stop();

        var updateMetadataSql = @"
            UPDATE cache_metadata
            SET last_refreshed = @LastRefreshed,
                refresh_duration_ms = @RefreshDurationMs
            WHERE cache_name = @CacheName";

        await ExecuteRowsAsync(updateMetadataSql, new
        {
            LastRefreshed = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
            RefreshDurationMs = stopwatch.ElapsedMilliseconds,
            CacheName = "contract_details_cache"
        });
    }
}
