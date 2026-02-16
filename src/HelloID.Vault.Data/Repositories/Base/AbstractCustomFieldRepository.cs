using Dapper;
using HelloID.Vault.Core.Models.Entities;
using HelloID.Vault.Data.Connection;
using HelloID.Vault.Data.Repositories.Interfaces;
using System.Data;
using System.Text.Json;

namespace HelloID.Vault.Data.Repositories.Base;

/// <summary>
/// Abstract base repository for CustomField operations.
/// Contains shared logic and defines database-specific abstract methods.
/// </summary>
public abstract class AbstractCustomFieldRepository : ICustomFieldRepository
{
    private readonly IDatabaseConnectionFactory _connectionFactory;

    protected AbstractCustomFieldRepository(IDatabaseConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    /// <summary>
    /// Gets the database connection factory.
    /// </summary>
    protected IDatabaseConnectionFactory ConnectionFactory => _connectionFactory;

    public async Task<IEnumerable<CustomFieldSchema>> GetSchemasAsync(string tableName)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            SELECT
                field_key AS FieldKey,
                table_name AS TableName,
                display_name AS DisplayName,
                validation_regex AS ValidationRegex,
                sort_order AS SortOrder,
                help_text AS HelpText,
                created_at AS CreatedAt
            FROM custom_field_schemas
            WHERE table_name = @TableName
            ORDER BY sort_order, display_name";

        return await connection.QueryAsync<CustomFieldSchema>(sql, new { TableName = tableName }).ConfigureAwait(false);
    }

    public async Task<IEnumerable<CustomFieldValue>> GetValuesAsync(string entityId, string tableName)
    {
        using var connection = _connectionFactory.CreateConnection();

        var tableNameColumn = tableName == "persons" ? "persons" : "contracts";

        var sql = $@"
            SELECT custom_fields
            FROM {tableNameColumn}
            WHERE external_id = @EntityId";

        var customFieldsJson = await connection.QueryFirstOrDefaultAsync<string?>(sql, new { EntityId = entityId }).ConfigureAwait(false);

        if (string.IsNullOrEmpty(customFieldsJson))
        {
            return Enumerable.Empty<CustomFieldValue>();
        }

        // Deserialize to Dictionary<string, JsonElement> to properly handle JSON null
        var jsonFields = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(customFieldsJson);
        if (jsonFields == null || jsonFields.Count == 0)
        {
            return Enumerable.Empty<CustomFieldValue>();
        }

        var schemas = await GetSchemasAsync(tableName);
        var schemaList = schemas.ToList();
        var result = new List<CustomFieldValue>();

        foreach (var kv in jsonFields)
        {
            if (schemaList.Any(s => s.FieldKey == kv.Key))
            {
                // Convert JsonElement to string, with null for JSON null values
                string? textValue = kv.Value.ValueKind == JsonValueKind.Null
                    ? null
                    : kv.Value.ToString();

                result.Add(new CustomFieldValue
                {
                    EntityId = entityId,
                    TableName = tableName,
                    FieldKey = kv.Key,
                    TextValue = textValue
                });
            }
        }

        return result;
    }

    public async Task<int> InsertSchemaAsync(CustomFieldSchema schema)
    {
        using var connection = _connectionFactory.CreateConnection();

        var insertSql = @"
            INSERT INTO custom_field_schemas (
                table_name, field_key, display_name, validation_regex, sort_order, help_text
            ) VALUES (
                @TableName, @FieldKey, @DisplayName, @ValidationRegex, @SortOrder, @HelpText
            )";

        var result = await connection.ExecuteAsync(insertSql, new
        {
            TableName = schema.TableName,
            FieldKey = schema.FieldKey,
            DisplayName = schema.DisplayName,
            ValidationRegex = schema.ValidationRegex,
            SortOrder = schema.SortOrder,
            HelpText = schema.HelpText
        }).ConfigureAwait(false);

        // Backfill: Add the new field to all existing records with null value
        await BackfillNewFieldAsync(schema, connection).ConfigureAwait(false);

        return result;
    }

    public async Task<int> UpdateSchemaAsync(CustomFieldSchema schema)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            UPDATE custom_field_schemas SET
                display_name = @DisplayName,
                validation_regex = @ValidationRegex,
                sort_order = @SortOrder,
                help_text = @HelpText
            WHERE table_name = @TableName AND field_key = @FieldKey";

        return await connection.ExecuteAsync(sql, new
        {
            TableName = schema.TableName,
            FieldKey = schema.FieldKey,
            DisplayName = schema.DisplayName,
            ValidationRegex = schema.ValidationRegex,
            SortOrder = schema.SortOrder,
            HelpText = schema.HelpText
        }).ConfigureAwait(false);
    }

    public async Task<int> DeleteSchemaAsync(string id)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = "DELETE FROM custom_field_schemas WHERE field_key = @FieldKey";

        return await connection.ExecuteAsync(sql, new { FieldKey = id }).ConfigureAwait(false);
    }

    public async Task<int> UpsertValueAsync(CustomFieldValue value)
    {
        return await UpsertValueInternalAsync(value).ConfigureAwait(false);
    }

    public async Task<int> DeleteValuesAsync(string entityId, string tableName)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = $@"
            UPDATE {tableName}
            SET custom_fields = NULL
            WHERE external_id = @EntityId";

        return await connection.ExecuteAsync(sql, new { EntityId = entityId }).ConfigureAwait(false);
    }

    /// <summary>
    /// Database-specific implementation for upserting a custom field value.
    /// </summary>
    protected abstract Task<int> UpsertValueInternalAsync(CustomFieldValue value);

    /// <summary>
    /// Database-specific implementation for backfilling a new field to existing records.
    /// </summary>
    protected abstract Task BackfillNewFieldAsync(CustomFieldSchema schema, IDbConnection connection);
}
