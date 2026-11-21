using Dapper;
using HelloID.Vault.Core.Models.Entities;
using HelloID.Vault.Data.Connection;

namespace HelloID.Vault.Data.Repositories.Base;

/// <summary>
/// Contract repository execution for SQLite/PostgreSQL: Dapper on connections
/// from the factory, plus the ambient-transaction insert used during imports
/// and the transactional cache rebuild.
/// </summary>
public abstract class DapperContractRepositoryBase : AbstractContractRepository
{
    private readonly IDatabaseConnectionFactory _connectionFactory;

    protected DapperContractRepositoryBase(IDatabaseConnectionFactory connectionFactory)
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

    protected override async Task<int> ExecuteRowsRelaxedAsync(string sql, object? parameters = null)
    {
        // Foreign keys relaxed: the contract update carries reference columns
        // (manager, cost center, ...) that are not all pre-validated
        using var connection = _connectionFactory.CreateConnection(enforceForeignKeys: false);
        return await connection.ExecuteAsync(sql, parameters).ConfigureAwait(false);
    }

    protected override async Task<TResult> ExecuteScalarValueAsync<TResult>(string sql, object? parameters = null)
    {
        using var connection = _connectionFactory.CreateConnection();
        var result = await connection.ExecuteScalarAsync<TResult>(sql, parameters).ConfigureAwait(false);
        return result ?? throw new InvalidOperationException("Query returned no value.");
    }

    public override async Task<int> InsertAsync(Contract contract, System.Data.IDbConnection connection, System.Data.IDbTransaction transaction)
    {
        var sql = @"
            INSERT INTO contracts (
                external_id, person_id, start_date, end_date, type_code, type_description,
                fte, hours_per_week, percentage, sequence,
                manager_person_external_id, location_external_id, cost_center_external_id, cost_bearer_external_id,
                employer_external_id, team_external_id, department_external_id, division_external_id, title_external_id, organization_external_id, source,
                location_source, cost_center_source, cost_bearer_source, employer_source,
                team_source, department_source, division_source, title_source, organization_source
            ) VALUES (
                @ExternalId, @PersonId, @StartDate, @EndDate, @TypeCode, @TypeDescription,
                @Fte, @HoursPerWeek, @Percentage, @Sequence,
                @ManagerPersonExternalId, @LocationExternalId, @CostCenterExternalId, @CostBearerExternalId,
                @EmployerExternalId, @TeamExternalId, @DepartmentExternalId, @DivisionExternalId, @TitleExternalId, @OrganizationExternalId, @Source,
                @LocationSource, @CostCenterSource, @CostBearerSource, @EmployerSource,
                @TeamSource, @DepartmentSource, @DivisionSource, @TitleSource, @OrganizationSource
            )";

        return await connection.ExecuteAsync(sql, contract, transaction).ConfigureAwait(false);
    }

    /// <summary>
    /// Atomic rebuild in one transaction: readers never see an empty cache and
    /// a failure rolls back to the previous contents.
    /// </summary>
    public override async Task RebuildCacheAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        using var transaction = connection.BeginTransaction();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        await connection.ExecuteAsync(CacheDeleteSql, transaction).ConfigureAwait(false);
        await connection.ExecuteAsync(CacheFillSql, transaction).ConfigureAwait(false);
        var rowCount = await connection.ExecuteScalarAsync<int>(CacheCountSql, transaction).ConfigureAwait(false);

        stopwatch.Stop();

        await connection.ExecuteAsync(CacheMetadataUpdateSql, new
        {
            CacheName = "contract_details_cache",
            LastRefreshed = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
            RowCount = rowCount,
            RefreshDurationMs = stopwatch.ElapsedMilliseconds
        }, transaction).ConfigureAwait(false);

        transaction.Commit();
    }

    public override async Task RefreshContractCacheItemAsync(int contractId)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        using var connection = _connectionFactory.CreateConnection();
        await RefreshContractCacheItemInternalAsync(contractId, connection).ConfigureAwait(false);

        stopwatch.Stop();

        var updateMetadataSql = @"
            UPDATE cache_metadata
            SET last_refreshed = @LastRefreshed,
                refresh_duration_ms = @RefreshDurationMs
            WHERE cache_name = @CacheName";

        await connection.ExecuteAsync(updateMetadataSql, new
        {
            CacheName = "contract_details_cache",
            LastRefreshed = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
            RefreshDurationMs = stopwatch.ElapsedMilliseconds
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Backend-specific single-item cache refresh on an open connection.
    /// </summary>
    protected abstract Task RefreshContractCacheItemInternalAsync(int contractId, System.Data.IDbConnection connection);
}
