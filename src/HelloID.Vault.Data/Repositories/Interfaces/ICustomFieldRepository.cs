using System.Data;
using HelloID.Vault.Core.Models.Entities;
using HelloID.Vault.Core.Models.Filters;

namespace HelloID.Vault.Data.Repositories.Interfaces;

/// <summary>
/// Repository interface for CustomField operations (JSON storage).
/// </summary>
public interface ICustomFieldRepository
{
    /// <summary>
    /// Gets all custom field schemas for a table.
    /// </summary>
    Task<IEnumerable<CustomFieldSchema>> GetSchemasAsync(string tableName);

    /// <summary>
    /// Gets all custom field values for an entity.
    /// </summary>
    Task<IEnumerable<CustomFieldValue>> GetValuesAsync(string entityId, string tableName);

    /// <summary>
    /// Inserts a custom field schema.
    /// </summary>
    Task<int> InsertSchemaAsync(CustomFieldSchema schema);

    /// <summary>
    /// Updates a custom field schema.
    /// </summary>
    Task<int> UpdateSchemaAsync(CustomFieldSchema schema);

    /// <summary>
    /// Deletes a custom field schema.
    /// </summary>
    Task<int> DeleteSchemaAsync(string id);

    /// <summary>
    /// Upserts a custom field value.
    /// </summary>
    Task<int> UpsertValueAsync(CustomFieldValue value);

    /// <summary>
    /// Clears all custom field values for an entity (sets custom_fields to NULL).
    /// </summary>
    Task<int> DeleteValuesAsync(string entityId, string tableName);

    /// <summary>
    /// Gets a pivot table of custom field values for all entities.
    /// Returns a DataTable with fixed columns (display_name, external_id) plus
    /// dynamic columns for each custom field schema.
    /// </summary>
    /// <param name="tableName">"persons" or "contracts"</param>
    /// <param name="page">Page number (1-based)</param>
    /// <param name="pageSize">Rows per page</param>
    /// <param name="searchTerm">Optional search filter</param>
    Task<DataTable> GetPivotDataAsync(string tableName, int page, int pageSize, string? searchTerm = null, List<FieldFilterCriteria>? advancedFilters = null);

    /// <summary>
    /// Gets the total count of entities that have custom field data.
    /// </summary>
    Task<int> GetPivotCountAsync(string tableName, string? searchTerm = null, List<FieldFilterCriteria>? advancedFilters = null);
}
