using Dapper;
using HelloID.Vault.Data.Connection;
using HelloID.Vault.Data.Repositories.Base;
using System.Data;

namespace HelloID.Vault.Data.Repositories.Sqlite;

/// <summary>
/// SQLite-specific implementation of ContractRepository.
/// Uses INSERT OR REPLACE for cache refresh operations.
/// </summary>
public class SqliteContractRepository : AbstractContractRepository
{
    public SqliteContractRepository(IDatabaseConnectionFactory connectionFactory)
        : base(connectionFactory)
    {
    }

    protected override async Task RefreshContractCacheItemInternalAsync(int contractId, IDbConnection connection)
    {
        // SQLite: Use INSERT OR REPLACE
        var refreshSql = @"
            INSERT OR REPLACE INTO contract_details_cache
            SELECT * FROM contract_details_view WHERE contract_id = @ContractId";

        await connection.ExecuteAsync(refreshSql, new { ContractId = contractId }).ConfigureAwait(false);
    }
}
