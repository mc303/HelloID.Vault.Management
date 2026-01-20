namespace HelloID.Vault.Core.Models.Entities;

/// <summary>
/// Represents an organization.
/// </summary>
public class Organization
{
    public string ExternalId { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string? Name { get; set; }
    public string? Source { get; set; }
}
