namespace HelloID.Vault.Core.Models.Entities;

/// <summary>
/// Represents a custom field schema definition (text-only).
/// </summary>
public class CustomFieldSchema
{
    public string FieldKey { get; set; } = string.Empty;
    public string TableName { get; set; } = string.Empty; // "persons" or "contracts"
    public string DisplayName { get; set; } = string.Empty;
    public string? ValidationRegex { get; set; }
    public int SortOrder { get; set; }
    public string? HelpText { get; set; }
    public string? CreatedAt { get; set; }
}
