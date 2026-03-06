using System.Data;
using System.Diagnostics;
using System.Text.Json;
using HelloID.Vault.Core.Models.Entities;
using HelloID.Vault.Data.Connection;
using HelloID.Vault.Data.Repositories.Interfaces;

namespace HelloID.Vault.Data.Repositories.Turso;

public class TursoCustomFieldRepository : ICustomFieldRepository
{
    private readonly ITursoClient _client;

    public TursoCustomFieldRepository(ITursoClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    public async Task<IEnumerable<CustomFieldSchema>> GetSchemasAsync(string tableName)
    {
        Debug.WriteLine($"[TursoCustomFieldRepository] GetSchemasAsync: {tableName}");
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
            WHERE table_name = ?
            ORDER BY sort_order, display_name";
        var result = await _client.QueryAsync<CustomFieldSchema>(sql, new { tableName });
        return result.Rows;
    }

    public async Task<IEnumerable<CustomFieldValue>> GetValuesAsync(string entityId, string tableName)
    {
        Debug.WriteLine($"[TursoCustomFieldRepository] GetValuesAsync: {entityId}, {tableName}");
        
        var tableNameColumn = tableName == "persons" ? "persons" : "contracts";
        var sql = $"SELECT custom_fields FROM {tableNameColumn} WHERE external_id = ?";
        
        var customFieldsJson = await _client.ExecuteScalarAsync<string>(sql, new { entityId = entityId });

        if (string.IsNullOrEmpty(customFieldsJson))
        {
            return Enumerable.Empty<CustomFieldValue>();
        }

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
        Debug.WriteLine($"[TursoCustomFieldRepository] InsertSchemaAsync: {schema.FieldKey}");
        var sql = @"
            INSERT INTO custom_field_schemas (
                table_name, field_key, display_name, validation_regex, sort_order, help_text
            ) VALUES (?, ?, ?, ?, ?, ?)";

        var result = await _client.ExecuteAsync(sql, new
        {
            TableName = schema.TableName,
            FieldKey = schema.FieldKey,
            DisplayName = schema.DisplayName,
            ValidationRegex = schema.ValidationRegex,
            SortOrder = schema.SortOrder,
            HelpText = schema.HelpText
        });

        await BackfillNewFieldAsync(schema);

        return result;
    }

    public async Task<int> UpdateSchemaAsync(CustomFieldSchema schema)
    {
        Debug.WriteLine($"[TursoCustomFieldRepository] UpdateSchemaAsync: {schema.FieldKey}");
        var sql = @"
            UPDATE custom_field_schemas SET
                display_name = ?,
                validation_regex = ?,
                sort_order = ?,
                help_text = ?
            WHERE table_name = ? AND field_key = ?";

        return await _client.ExecuteAsync(sql, new
        {
            DisplayName = schema.DisplayName,
            ValidationRegex = schema.ValidationRegex,
            SortOrder = schema.SortOrder,
            HelpText = schema.HelpText,
            TableName = schema.TableName,
            FieldKey = schema.FieldKey
        });
    }

    public async Task<int> DeleteSchemaAsync(string id)
    {
        Debug.WriteLine($"[TursoCustomFieldRepository] DeleteSchemaAsync: {id}");
        var sql = "DELETE FROM custom_field_schemas WHERE field_key = ?";
        return await _client.ExecuteAsync(sql, new { id });
    }

    public async Task<int> UpsertValueAsync(CustomFieldValue value)
    {
        Debug.WriteLine($"[TursoCustomFieldRepository] UpsertValueAsync: {value.FieldKey}");
        
        var tableNameColumn = value.TableName == "persons" ? "persons" : "contracts";
        
        var currentJson = await _client.ExecuteScalarAsync<string>(
            $"SELECT custom_fields FROM {tableNameColumn} WHERE external_id = ?",
            new { entityId = value.EntityId });

        Dictionary<string, object?> fields;
        if (string.IsNullOrEmpty(currentJson))
        {
            fields = new Dictionary<string, object?>();
        }
        else
        {
            fields = JsonSerializer.Deserialize<Dictionary<string, object?>>(currentJson) ?? new Dictionary<string, object?>();
        }

        fields[value.FieldKey] = value.TextValue;
        var newJson = JsonSerializer.Serialize(fields);

        var sql = $"UPDATE {tableNameColumn} SET custom_fields = ? WHERE external_id = ?";
        return await _client.ExecuteAsync(sql, new { customFields = newJson, entityId = value.EntityId });
    }

    public async Task<int> DeleteValuesAsync(string entityId, string tableName)
    {
        Debug.WriteLine($"[TursoCustomFieldRepository] DeleteValuesAsync: {entityId}");
        var sql = $"UPDATE {tableName} SET custom_fields = NULL WHERE external_id = ?";
        return await _client.ExecuteAsync(sql, new { entityId });
    }

    private async Task BackfillNewFieldAsync(CustomFieldSchema schema)
    {
        Debug.WriteLine($"[TursoCustomFieldRepository] BackfillNewFieldAsync: {schema.FieldKey}");
        var tableName = schema.TableName == "persons" ? "persons" : "contracts";
        var sql = $"UPDATE {tableName} SET custom_fields = json_set(COALESCE(custom_fields, '{{}}'), '$.{schema.FieldKey}', NULL) WHERE custom_fields IS NULL OR json_type(custom_fields, '$.{schema.FieldKey}') IS NULL";
        await _client.ExecuteAsync(sql);
    }
}
