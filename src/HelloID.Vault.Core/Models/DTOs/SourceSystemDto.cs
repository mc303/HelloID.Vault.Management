namespace HelloID.Vault.Core.Models.DTOs;

/// <summary>
/// DTO for displaying source system information with enhanced computed fields.
/// </summary>
public class SourceSystemDto
{
    public string SystemId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string IdentificationKey { get; set; } = string.Empty;
    public int ReferenceCount { get; set; } = 0;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}