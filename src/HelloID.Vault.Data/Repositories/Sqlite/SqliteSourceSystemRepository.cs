using Dapper;
using HelloID.Vault.Core.Models.Entities;
using HelloID.Vault.Data.Connection;
using HelloID.Vault.Data.Repositories.Base;
using System.Data;

namespace HelloID.Vault.Data.Repositories.Sqlite;

/// <summary>
/// SQLite-specific implementation of SourceSystemRepository.
/// Uses INSERT OR IGNORE for upsert operations.
/// </summary>
public class SqliteSourceSystemRepository : AbstractSourceSystemRepository
{
    public SqliteSourceSystemRepository(IDatabaseConnectionFactory connectionFactory)
        : base(connectionFactory)
    {
    }

    protected override async Task<int> InsertInternalAsync(SourceSystem sourceSystem, IDbConnection connection, IDbTransaction? transaction = null)
    {
        var sql = @"
            INSERT OR IGNORE INTO source_system (system_id, display_name, identification_key)
            VALUES (@SystemId, @DisplayName, @IdentificationKey)";

        return transaction is null
            ? await connection.ExecuteAsync(sql, sourceSystem).ConfigureAwait(false)
            : await connection.ExecuteAsync(sql, sourceSystem, transaction).ConfigureAwait(false);
    }
}
