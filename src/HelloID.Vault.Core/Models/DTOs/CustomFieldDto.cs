namespace HelloID.Vault.Core.Models.DTOs;

/// <summary>
/// DTO for displaying custom field values.
/// </summary>
public class CustomFieldDto
{
    public string FieldKey { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Value { get; set; }
    public string DataType { get; set; } = "text";
}
