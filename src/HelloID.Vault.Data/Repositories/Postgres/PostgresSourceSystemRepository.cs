using Dapper;
using HelloID.Vault.Core.Models.Entities;
using HelloID.Vault.Data.Connection;
using HelloID.Vault.Data.Repositories.Base;
using System.Data;

namespace HelloID.Vault.Data.Repositories.Postgres;

/// <summary>
/// PostgreSQL-specific implementation of SourceSystemRepository.
/// Uses INSERT ... ON CONFLICT DO NOTHING for upsert operations.
/// </summary>
public class PostgresSourceSystemRepository : AbstractSourceSystemRepository
{
    public PostgresSourceSystemRepository(IDatabaseConnectionFactory connectionFactory)
        : base(connectionFactory)
    {
    }

    protected override async Task<int> InsertInternalAsync(SourceSystem sourceSystem, IDbConnection connection, IDbTransaction? transaction = null)
    {
        var sql = @"
            INSERT INTO source_system (system_id, display_name, identification_key)
            VALUES (@SystemId, @DisplayName, @IdentificationKey)
            ON CONFLICT (system_id) DO NOTHING";

        return transaction is null
            ? await connection.ExecuteAsync(sql, sourceSystem).ConfigureAwait(false)
            : await connection.ExecuteAsync(sql, sourceSystem, transaction).ConfigureAwait(false);
    }
}
