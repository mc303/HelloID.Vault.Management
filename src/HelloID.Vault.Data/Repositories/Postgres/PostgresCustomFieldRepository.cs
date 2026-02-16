using Dapper;
using HelloID.Vault.Core.Models.Entities;
using HelloID.Vault.Data.Connection;
using HelloID.Vault.Data.Repositories.Base;
using System.Data;

namespace HelloID.Vault.Data.Repositories.Postgres;

/// <summary>
/// PostgreSQL-specific implementation of CustomFieldRepository.
/// Uses PostgreSQL's jsonb_set and proper JSON string formatting.
/// </summary>
public class PostgresCustomFieldRepository : AbstractCustomFieldRepository
{
    public PostgresCustomFieldRepository(IDatabaseConnectionFactory connectionFactory)
        : base(connectionFactory)
    {
    }

    protected override async Task<int> UpsertValueInternalAsync(CustomFieldValue value)
    {
        using var connection = ConnectionFactory.CreateConnection();

        var tableName = value.TableName == "persons" ? "persons" : "contracts";

        // Prepare the Path as a string array for PostgreSQL jsonb_set()
        // "MobileFromADUser" becomes ["MobileFromADUser"]
        // "$.MobileFromADUser" becomes ["MobileFromADUser"]
        // "Custom.Laptop" becomes ["Custom", "Laptop"]
        // "$.Custom.Laptop" becomes ["Custom", "Laptop"]
        var cleanKey = value.FieldKey.StartsWith("$.") ? value.FieldKey.Substring(2) : value.FieldKey;
        var pathArray = cleanKey.Split('.');

        // Determine SQL based on whether we're storing null or a value
        string sql;
        object parameters;

        if (value.TextValue == "null")
        {
            // For null values, use 'null'::jsonb directly
            sql = $@"
                UPDATE {tableName}
                SET custom_fields = jsonb_set(
                    COALESCE(custom_fields::jsonb, '{{}}'::jsonb),
                    @Path,
                    'null'::jsonb,
                    true
                )::text
                WHERE external_id = @EntityId";
            parameters = new { EntityId = value.EntityId, Path = pathArray };
        }
        else
        {
            // For non-null values, convert the text value to a proper JSON string
            sql = $@"
                UPDATE {tableName}
                SET custom_fields = jsonb_set(
                    COALESCE(custom_fields::jsonb, '{{}}'::jsonb),
                    @Path,
                    to_jsonb(@Value::text),
                    true
                )::text
                WHERE external_id = @EntityId";
            parameters = new { EntityId = value.EntityId, Path = pathArray, Value = value.TextValue };
        }

        return await connection.ExecuteAsync(sql, parameters).ConfigureAwait(false);
    }

    protected override async Task BackfillNewFieldAsync(CustomFieldSchema schema, IDbConnection connection)
    {
        var tableName = schema.TableName == "persons" ? "persons" : "contracts";

        // Prepare the Path as a string array for PostgreSQL jsonb_set()
        var cleanKey = schema.FieldKey.StartsWith("$.") ? schema.FieldKey.Substring(2) : schema.FieldKey;
        var pathArray = cleanKey.Split('.');

        var backfillSql = $@"
            UPDATE {tableName}
            SET custom_fields = jsonb_set(
                COALESCE(custom_fields::jsonb, '{{}}'::jsonb),
                @Path,
                'null'::jsonb,
                true
            )::text
            WHERE custom_fields IS NOT NULL";

        await connection.ExecuteAsync(backfillSql, new { Path = pathArray }).ConfigureAwait(false);
    }
}
