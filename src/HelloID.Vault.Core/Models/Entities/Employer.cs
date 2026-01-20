namespace HelloID.Vault.Core.Models.Entities;

/// <summary>
/// Represents an employer.
/// </summary>
public class Employer
{
    public string ExternalId { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string? Name { get; set; }
    public string? Source { get; set; }
}
