namespace HelloID.Vault.Core.Models.DTOs;

/// <summary>
/// DTO for contract filter persistence.
/// </summary>
public class ContractFilterDto
{
    public string FieldName { get; set; } = string.Empty;
    public string FieldDisplayName { get; set; } = string.Empty;
    public string Operator { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
