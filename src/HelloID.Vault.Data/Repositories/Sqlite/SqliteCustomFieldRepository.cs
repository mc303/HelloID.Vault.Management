using Dapper;
using HelloID.Vault.Core.Models.Entities;
using HelloID.Vault.Data.Connection;
using HelloID.Vault.Data.Repositories.Base;
using System.Data;

namespace HelloID.Vault.Data.Repositories.Sqlite;

/// <summary>
/// SQLite-specific implementation of CustomFieldRepository.
/// Uses SQLite's json_set function for JSON operations.
/// </summary>
public class SqliteCustomFieldRepository : AbstractCustomFieldRepository
{
    public SqliteCustomFieldRepository(IDatabaseConnectionFactory connectionFactory)
        : base(connectionFactory)
    {
    }

    protected override async Task<int> UpsertValueInternalAsync(CustomFieldValue value)
    {
        using var connection = ConnectionFactory.CreateConnection();

        var tableName = value.TableName == "persons" ? "persons" : "contracts";

        // SQLite uses json_set for JSON modifications
        var sql = $@"
            UPDATE {tableName}
            SET custom_fields = json_set(
                COALESCE(custom_fields, '{{}}'),
                '$.{value.FieldKey}',
                @Value
            )
            WHERE external_id = @EntityId";

        // Use null parameter value to store JSON null
        var paramValue = value.TextValue == "null" ? (object?)null : value.TextValue;

        return await connection.ExecuteAsync(sql, new { EntityId = value.EntityId, Value = paramValue }).ConfigureAwait(false);
    }

    protected override async Task BackfillNewFieldAsync(CustomFieldSchema schema, IDbConnection connection)
    {
        var tableName = schema.TableName == "persons" ? "persons" : "contracts";

        var backfillSql = $@"
            UPDATE {tableName}
            SET custom_fields = json_set(
                COALESCE(custom_fields, '{{}}'),
                '$.{schema.FieldKey}',
                @Value
            )
            WHERE custom_fields IS NOT NULL";

        await connection.ExecuteAsync(backfillSql, new { Value = (object?)null }).ConfigureAwait(false);
    }
}
