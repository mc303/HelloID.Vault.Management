using System.Data;
using System.Diagnostics;
using System.Text.Json;
using HelloID.Vault.Core.Models.Entities;
using HelloID.Vault.Core.Models.Filters;
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

    public async Task<DataTable> GetPivotDataAsync(string tableName, int page, int pageSize, string? searchTerm = null, List<FieldFilterCriteria>? advancedFilters = null)
    {
        var schemas = (await GetSchemasAsync(tableName)).OrderBy(s => s.SortOrder).ThenBy(s => s.DisplayName).ToList();
        var offset = (page - 1) * pageSize;

        var columns = new List<string>();
        var alias = tableName == "persons" ? "p" : "c";

        if (tableName == "persons")
        {
            columns.Add("p.person_id");
            columns.Add("p.display_name");
            columns.Add("p.external_id");
        }
        else
        {
            columns.Add("c.contract_id");
            columns.Add("c.external_id");
            columns.Add("p.display_name AS person_name");
            columns.Add("c.person_id");
        }

        foreach (var schema in schemas)
        {
            columns.Add($"json_extract({alias}.custom_fields, '$.{schema.FieldKey}') AS \"{schema.FieldKey}\"");
        }

        var fromClause = tableName == "persons"
            ? "FROM persons p"
            : "FROM contracts c LEFT JOIN persons p ON c.person_id = p.person_id";

        var parameters = new Dictionary<string, object?>
        {
            ["Limit"] = pageSize,
            ["Offset"] = offset
        };

        var whereParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            parameters["SearchTerm"] = $"%{searchTerm.ToLower()}%";

            var searchSubParts = new List<string>();
            if (tableName == "persons")
            {
                searchSubParts.Add("LOWER(p.display_name) LIKE @SearchTerm");
                searchSubParts.Add("LOWER(p.external_id) LIKE @SearchTerm");
            }
            else
            {
                searchSubParts.Add("LOWER(c.external_id) LIKE @SearchTerm");
                searchSubParts.Add("LOWER(p.display_name) LIKE @SearchTerm");
            }

            foreach (var schema in schemas)
            {
                searchSubParts.Add($"LOWER(json_extract({alias}.custom_fields, '$.{schema.FieldKey}')) LIKE @SearchTerm");
            }

            whereParts.Add("(" + string.Join(" OR ", searchSubParts) + ")");
        }

        // Advanced filters (AND between filters)
        if (advancedFilters != null && advancedFilters.Count > 0)
        {
            for (int i = 0; i < advancedFilters.Count; i++)
            {
                var filter = advancedFilters[i];
                var paramPrefix = $"af{i}";
                var fieldExpr = $"json_extract({alias}.custom_fields, '$.{filter.FieldName}')";

                string condition = filter.Operator switch
                {
                    FieldFilterOperators.Contains => $"LOWER({fieldExpr}) LIKE @{paramPrefix}_Value",
                    FieldFilterOperators.Equals => $"LOWER({fieldExpr}) = LOWER(@{paramPrefix}_Value)",
                    FieldFilterOperators.NotEquals => $"({fieldExpr} IS NULL OR LOWER({fieldExpr}) != LOWER(@{paramPrefix}_Value))",
                    FieldFilterOperators.IsEmpty => $"({fieldExpr} IS NULL OR {fieldExpr} = '')",
                    FieldFilterOperators.IsNotEmpty => $"({fieldExpr} IS NOT NULL AND {fieldExpr} != '')",
                    _ => $"LOWER({fieldExpr}) LIKE @{paramPrefix}_Value"
                };

                if (filter.Operator is FieldFilterOperators.Contains)
                    parameters[$"{paramPrefix}_Value"] = $"%{filter.Value.ToLower()}%";
                else
                    parameters[$"{paramPrefix}_Value"] = filter.Value;

                whereParts.Add(condition);
            }
        }

        var whereClause = whereParts.Count > 0 ? "WHERE " + string.Join(" AND ", whereParts) : "";
        var orderBy = tableName == "persons" ? "p.display_name" : "c.external_id";

        var sql = $@"
            SELECT {string.Join(", ", columns)}
            {fromClause}
            {whereClause}
            ORDER BY {orderBy}
            LIMIT @Limit OFFSET @Offset";

        var result = await _client.QueryAsync<dynamic>(sql, parameters);

        var dt = new DataTable();

        // Define columns
        if (tableName == "persons")
        {
            dt.Columns.Add("person_id", typeof(string));
            dt.Columns.Add("display_name", typeof(string));
            dt.Columns.Add("external_id", typeof(string));
        }
        else
        {
            dt.Columns.Add("contract_id", typeof(string));
            dt.Columns.Add("external_id", typeof(string));
            dt.Columns.Add("person_name", typeof(string));
            dt.Columns.Add("person_id", typeof(string));
        }

        foreach (var schema in schemas)
        {
            dt.Columns.Add(schema.FieldKey, typeof(string));
        }

        // Populate rows
        foreach (var row in result.Rows)
        {
            var dict = (IDictionary<string, object>)row;
            var dataRow = dt.NewRow();

            foreach (DataColumn col in dt.Columns)
            {
                var key = col.ColumnName;
                dataRow[key] = dict.TryGetValue(key, out var val) && val != null ? val.ToString() : DBNull.Value;
            }

            dt.Rows.Add(dataRow);
        }

        return dt;
    }

    public async Task<int> GetPivotCountAsync(string tableName, string? searchTerm = null, List<FieldFilterCriteria>? advancedFilters = null)
    {
        var schemas = (await GetSchemasAsync(tableName)).OrderBy(s => s.SortOrder).ThenBy(s => s.DisplayName).ToList();
        var alias = tableName == "persons" ? "p" : "c";

        var fromClause = tableName == "persons"
            ? "FROM persons p"
            : "FROM contracts c LEFT JOIN persons p ON c.person_id = p.person_id";

        var parameters = new Dictionary<string, object?>();
        var whereParts = new List<string>();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            parameters["SearchTerm"] = $"%{searchTerm.ToLower()}%";

            var searchSubParts = new List<string>();
            if (tableName == "persons")
            {
                searchSubParts.Add("LOWER(p.display_name) LIKE @SearchTerm");
                searchSubParts.Add("LOWER(p.external_id) LIKE @SearchTerm");
            }
            else
            {
                searchSubParts.Add("LOWER(c.external_id) LIKE @SearchTerm");
                searchSubParts.Add("LOWER(p.display_name) LIKE @SearchTerm");
            }

            foreach (var schema in schemas)
            {
                searchSubParts.Add($"LOWER(json_extract({alias}.custom_fields, '$.{schema.FieldKey}')) LIKE @SearchTerm");
            }

            whereParts.Add("(" + string.Join(" OR ", searchSubParts) + ")");
        }

        // Advanced filters (AND between filters)
        if (advancedFilters != null && advancedFilters.Count > 0)
        {
            for (int i = 0; i < advancedFilters.Count; i++)
            {
                var filter = advancedFilters[i];
                var paramPrefix = $"af{i}";
                var fieldExpr = $"json_extract({alias}.custom_fields, '$.{filter.FieldName}')";

                string condition = filter.Operator switch
                {
                    FieldFilterOperators.Contains => $"LOWER({fieldExpr}) LIKE @{paramPrefix}_Value",
                    FieldFilterOperators.Equals => $"LOWER({fieldExpr}) = LOWER(@{paramPrefix}_Value)",
                    FieldFilterOperators.NotEquals => $"({fieldExpr} IS NULL OR LOWER({fieldExpr}) != LOWER(@{paramPrefix}_Value))",
                    FieldFilterOperators.IsEmpty => $"({fieldExpr} IS NULL OR {fieldExpr} = '')",
                    FieldFilterOperators.IsNotEmpty => $"({fieldExpr} IS NOT NULL AND {fieldExpr} != '')",
                    _ => $"LOWER({fieldExpr}) LIKE @{paramPrefix}_Value"
                };

                if (filter.Operator is FieldFilterOperators.Contains)
                    parameters[$"{paramPrefix}_Value"] = $"%{filter.Value.ToLower()}%";
                else
                    parameters[$"{paramPrefix}_Value"] = filter.Value;

                whereParts.Add(condition);
            }
        }

        var whereClause = whereParts.Count > 0 ? "WHERE " + string.Join(" AND ", whereParts) : "";

        var sql = $"SELECT COUNT(*) {fromClause} {whereClause}";
        return await _client.ExecuteScalarAsync<int>(sql, parameters);
    }
}
