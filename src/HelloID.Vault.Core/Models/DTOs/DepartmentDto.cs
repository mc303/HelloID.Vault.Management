namespace HelloID.Vault.Core.Models.DTOs;

/// <summary>
/// DTO for displaying department information.
/// </summary>
public class DepartmentDto
{
    public string ExternalId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string? ParentExternalId { get; set; }
    public string? ParentName { get; set; }
    public string? ManagerPersonId { get; set; }
    public string? ManagerName { get; set; }
    public string? Source { get; set; }
}
