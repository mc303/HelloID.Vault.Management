namespace HelloID.Vault.Core.Models.Entities;

/// <summary>
/// Represents a custom field value (JSON-based).
/// </summary>
public class CustomFieldValue
{
    public string EntityId { get; set; } = string.Empty; // external_id from persons or contracts
    public string TableName { get; set; } = string.Empty; // "persons" or "contracts"
    public string FieldKey { get; set; } = string.Empty;
    public string? TextValue { get; set; }
}
