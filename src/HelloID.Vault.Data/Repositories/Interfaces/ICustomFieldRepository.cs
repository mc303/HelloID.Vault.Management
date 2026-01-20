using HelloID.Vault.Core.Models.Entities;

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
}
