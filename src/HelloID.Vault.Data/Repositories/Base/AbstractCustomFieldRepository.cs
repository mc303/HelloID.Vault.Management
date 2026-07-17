using Dapper;
using HelloID.Vault.Core.Models.Entities;
using HelloID.Vault.Core.Models.Filters;
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

    /// <inheritdoc />
    public virtual async Task<DataTable> GetPivotDataAsync(string tableName, int page, int pageSize, string? searchTerm = null, List<FieldFilterCriteria>? advancedFilters = null)
    {
        var schemas = (await GetSchemasAsync(tableName)).OrderBy(s => s.SortOrder).ThenBy(s => s.DisplayName).ToList();
        var offset = (page - 1) * pageSize;
        var isPostgres = _connectionFactory.DatabaseType == DatabaseType.PostgreSql;
        var alias = tableName == "persons" ? "p" : "c";

        var columns = new List<string>();
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
            if (isPostgres)
                columns.Add($"jsonb_extract_path_text({alias}.custom_fields::jsonb, '{schema.FieldKey}') AS \"{schema.FieldKey}\"");
            else
                columns.Add($"json_extract({alias}.custom_fields, '$.{schema.FieldKey}') AS \"{schema.FieldKey}\"");
        }

        var fromClause = tableName == "persons"
            ? "FROM persons p"
            : "FROM contracts c LEFT JOIN persons p ON c.person_id = p.person_id";

        var (whereClause, parameters) = BuildWhereClause(tableName, alias, isPostgres, schemas, searchTerm, advancedFilters);

        var sql = $@"
            SELECT {string.Join(", ", columns)}
            {fromClause}
            {whereClause}
            ORDER BY {(tableName == "persons" ? "p.display_name" : "c.external_id")}
            LIMIT @PageSize OFFSET @Offset";

        parameters["PageSize"] = pageSize;
        parameters["Offset"] = offset;

        using var connection = _connectionFactory.CreateConnection();
        using var reader = await connection.ExecuteReaderAsync(sql, parameters).ConfigureAwait(false);

        var dt = new DataTable();
        dt.Load(reader);
        return dt;
    }

    /// <inheritdoc />
    public virtual async Task<int> GetPivotCountAsync(string tableName, string? searchTerm = null, List<FieldFilterCriteria>? advancedFilters = null)
    {
        var schemas = (await GetSchemasAsync(tableName)).OrderBy(s => s.SortOrder).ThenBy(s => s.DisplayName).ToList();
        var isPostgres = _connectionFactory.DatabaseType == DatabaseType.PostgreSql;
        var alias = tableName == "persons" ? "p" : "c";

        var fromClause = tableName == "persons"
            ? "FROM persons p"
            : "FROM contracts c LEFT JOIN persons p ON c.person_id = p.person_id";

        var (whereClause, parameters) = BuildWhereClause(tableName, alias, isPostgres, schemas, searchTerm, advancedFilters);

        var sql = $"SELECT COUNT(*) {fromClause} {whereClause}";

        using var connection = _connectionFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int>(sql, parameters).ConfigureAwait(false);
    }

    /// <summary>
    /// Builds a WHERE clause from search term and advanced filters.
    /// Search term uses OR across all fields; advanced filters use AND between filters.
    /// </summary>
    private static (string whereClause, Dictionary<string, object?> parameters) BuildWhereClause(
        string tableName, string alias, bool isPostgres,
        List<CustomFieldSchema> schemas, string? searchTerm, List<FieldFilterCriteria>? advancedFilters)
    {
        var parameters = new Dictionary<string, object?>();
        var conditions = new List<string>();

        // Search term (OR across fields)
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            parameters["SearchTerm"] = $"%{searchTerm.ToLower()}%";
            var searchParts = new List<string>();

            if (isPostgres)
            {
                var likeOp = "ILIKE";
                if (tableName == "persons")
                {
                    searchParts.Add($"p.display_name {likeOp} @SearchTerm");
                    searchParts.Add($"p.external_id {likeOp} @SearchTerm");
                }
                else
                {
                    searchParts.Add($"c.external_id {likeOp} @SearchTerm");
                    searchParts.Add($"p.display_name {likeOp} @SearchTerm");
                }

                foreach (var schema in schemas)
                    searchParts.Add($"jsonb_extract_path_text({alias}.custom_fields::jsonb, '{schema.FieldKey}') {likeOp} @SearchTerm");
            }
            else
            {
                if (tableName == "persons")
                {
                    searchParts.Add("LOWER(p.display_name) LIKE @SearchTerm");
                    searchParts.Add("LOWER(p.external_id) LIKE @SearchTerm");
                }
                else
                {
                    searchParts.Add("LOWER(c.external_id) LIKE @SearchTerm");
                    searchParts.Add("LOWER(p.display_name) LIKE @SearchTerm");
                }

                foreach (var schema in schemas)
                    searchParts.Add($"LOWER(json_extract({alias}.custom_fields, '$.{schema.FieldKey}')) LIKE @SearchTerm");
            }

            conditions.Add("(" + string.Join(" OR ", searchParts) + ")");
        }

        // Advanced filters (AND between filters)
        if (advancedFilters != null && advancedFilters.Count > 0)
        {
            for (int i = 0; i < advancedFilters.Count; i++)
            {
                var filter = advancedFilters[i];
                var paramPrefix = $"af{i}";
                var fieldExpr = isPostgres
                    ? $"jsonb_extract_path_text({alias}.custom_fields::jsonb, '{filter.FieldName}')"
                    : $"json_extract({alias}.custom_fields, '$.{filter.FieldName}')";
                var lowerFieldExpr = $"LOWER({fieldExpr})";

                string condition = filter.Operator switch
                {
                    FieldFilterOperators.Contains => isPostgres
                        ? $"{fieldExpr} ILIKE @{paramPrefix}_Value"
                        : $"{lowerFieldExpr} LIKE @{paramPrefix}_Value",
                    FieldFilterOperators.Equals => $"{lowerFieldExpr} = LOWER(@{paramPrefix}_Value)",
                    FieldFilterOperators.NotEquals => $"({fieldExpr} IS NULL OR {lowerFieldExpr} != LOWER(@{paramPrefix}_Value))",
                    FieldFilterOperators.IsEmpty => $"({fieldExpr} IS NULL OR {fieldExpr} = '')",
                    FieldFilterOperators.IsNotEmpty => $"({fieldExpr} IS NOT NULL AND {fieldExpr} != '')",
                    _ => isPostgres
                        ? $"{fieldExpr} ILIKE @{paramPrefix}_Value"
                        : $"{lowerFieldExpr} LIKE @{paramPrefix}_Value"
                };

                if (filter.Operator is FieldFilterOperators.Contains)
                    parameters[$"{paramPrefix}_Value"] = $"%{filter.Value.ToLower()}%";
                else
                    parameters[$"{paramPrefix}_Value"] = filter.Value;

                conditions.Add(condition);
            }
        }

        var whereClause = conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : "";
        return (whereClause, parameters);
    }
}
