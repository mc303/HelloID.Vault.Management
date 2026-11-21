using Dapper;
using HelloID.Vault.Core.Models.Entities;
using HelloID.Vault.Core.Models.Json;
using HelloID.Vault.Core.Models;
using HelloID.Vault.Data.Connection;
using HelloID.Vault.Data.Repositories.Interfaces;
using HelloID.Vault.Data;
using HelloID.Vault.Services.Database;
using HelloID.Vault.Services.Import.Collectors;
using HelloID.Vault.Services.Import.Detection;
using HelloID.Vault.Services.Import.Mappers;
using HelloID.Vault.Services.Import.Models;
using HelloID.Vault.Services.Import.Strategies;
using HelloID.Vault.Services.Import.Utilities;
using HelloID.Vault.Services.Import.Validators;
using HelloID.Vault.Services.Interfaces;
using Microsoft.Data.Sqlite;
using Npgsql;
using System.Data;
using System.Diagnostics;
using System.Text.Json;

namespace HelloID.Vault.Services;

/// <summary>
/// Turso/platform, custom field and company-import parts of VaultImportService.
/// </summary>
public partial class VaultImportService
{

    /// <summary>
    /// Collects and imports custom field schemas from vault data.
    /// Used by both full import and company-only import.
    /// </summary>
    private async Task ImportCustomFieldSchemasAsync(VaultRoot vaultData, ImportResult result, IProgress<ImportProgress>? progress)
    {
        progress?.Report(new ImportProgress { CurrentOperation = "Creating custom field schemas..." });
        Debug.WriteLine($"[Import] Starting custom field schema creation...");

        var customFieldSchemas = new Dictionary<(string TableName, string FieldKey), (string DataType, HashSet<object> SampleValues)>();

        foreach (var vaultPerson in vaultData.Persons)
        {
            if (vaultPerson.Custom != null)
            {
                foreach (var (key, value) in vaultPerson.Custom)
                {
                    var schemaKey = ("persons", key);
                    if (!customFieldSchemas.ContainsKey(schemaKey))
                    {
                        customFieldSchemas[schemaKey] = ("text", new HashSet<object>());
                    }
                    if (value != null)
                    {
                        customFieldSchemas[schemaKey].SampleValues.Add(value);
                    }
                }
            }

            foreach (var vaultContract in vaultPerson.Contracts)
            {
                if (vaultContract.Custom != null)
                {
                    foreach (var (key, value) in vaultContract.Custom)
                    {
                        var schemaKey = ("contracts", key);
                        if (!customFieldSchemas.ContainsKey(schemaKey))
                        {
                            customFieldSchemas[schemaKey] = ("text", new HashSet<object>());
                        }
                        if (value != null)
                        {
                            customFieldSchemas[schemaKey].SampleValues.Add(value);
                        }
                    }
                }
            }
        }

        Debug.WriteLine($"[Import] Found {customFieldSchemas.Count} unique custom field schemas");

        using (var connection = _connectionFactory.CreateConnection(enforceForeignKeys: false))
        {
            int sortOrder = 1;
            foreach (var ((tableName, fieldKey), (_, sampleValues)) in customFieldSchemas)
            {
                var displayName = FormatDisplayName(fieldKey);
                var sql = GetInsertIgnoreSql("custom_field_schemas", "table_name, field_key, display_name, sort_order", "@TableName, @FieldKey, @DisplayName, @SortOrder");
                var rowsAffected = await connection.ExecuteAsync(sql,
                    new { TableName = tableName, FieldKey = fieldKey, DisplayName = displayName, SortOrder = sortOrder++ });

                if (tableName == "persons")
                {
                    result.CustomFieldPersonsImported += rowsAffected;
                }
                else if (tableName == "contracts")
                {
                    result.CustomFieldContractsImported += rowsAffected;
                }
            }
        }
    }


    /// <summary>
    /// Collects and imports custom field schemas to a specific connection factory (for Turso temp DB).
    /// </summary>
    private async Task ImportCustomFieldSchemasToSqliteAsync(VaultRoot vaultData, ImportResult result, SqliteConnectionFactory connectionFactory, IProgress<ImportProgress>? progress)
    {
        progress?.Report(new ImportProgress { CurrentOperation = "Creating custom field schemas..." });

        var customFieldSchemas = new Dictionary<(string TableName, string FieldKey), (string DataType, HashSet<object> SampleValues)>();

        foreach (var vaultPerson in vaultData.Persons)
        {
            if (vaultPerson.Custom != null)
            {
                foreach (var (key, value) in vaultPerson.Custom)
                {
                    var schemaKey = ("persons", key);
                    if (!customFieldSchemas.ContainsKey(schemaKey))
                    {
                        customFieldSchemas[schemaKey] = ("text", new HashSet<object>());
                    }
                    if (value != null)
                    {
                        customFieldSchemas[schemaKey].SampleValues.Add(value);
                    }
                }
            }

            foreach (var vaultContract in vaultPerson.Contracts)
            {
                if (vaultContract.Custom != null)
                {
                    foreach (var (key, value) in vaultContract.Custom)
                    {
                        var schemaKey = ("contracts", key);
                        if (!customFieldSchemas.ContainsKey(schemaKey))
                        {
                            customFieldSchemas[schemaKey] = ("text", new HashSet<object>());
                        }
                        if (value != null)
                        {
                            customFieldSchemas[schemaKey].SampleValues.Add(value);
                        }
                    }
                }
            }
        }

        using (var connection = connectionFactory.CreateConnection(enforceForeignKeys: false))
        {
            int sortOrder = 1;
            foreach (var ((tableName, fieldKey), _) in customFieldSchemas)
            {
                var displayName = FormatDisplayName(fieldKey);
                var sql = "INSERT OR IGNORE INTO custom_field_schemas (table_name, field_key, display_name, sort_order) VALUES (@TableName, @FieldKey, @DisplayName, @SortOrder)";
                var rowsAffected = await connection.ExecuteAsync(sql,
                    new { TableName = tableName, FieldKey = fieldKey, DisplayName = displayName, SortOrder = sortOrder++ });

                if (tableName == "persons")
                {
                    result.CustomFieldPersonsImported += rowsAffected;
                }
                else if (tableName == "contracts")
                {
                    result.CustomFieldContractsImported += rowsAffected;
                }
            }
        }
    }


}
