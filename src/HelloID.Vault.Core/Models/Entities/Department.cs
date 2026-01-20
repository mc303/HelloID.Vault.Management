namespace HelloID.Vault.Core.Models.Entities;

/// <summary>
/// Represents an organizational department with hierarchical structure.
/// </summary>
public class Department
{
    public string ExternalId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string? ParentExternalId { get; set; }
    public string? ManagerPersonId { get; set; }
    public string? Source { get; set; }
}
