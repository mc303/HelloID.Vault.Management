namespace HelloID.Vault.Core.Models.Entities;

/// <summary>
/// Represents a job title.
/// </summary>
public class Title
{
    public string ExternalId { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string? Name { get; set; }
    public string? Source { get; set; }
}
