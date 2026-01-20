namespace HelloID.Vault.Core.Models.Entities;

/// <summary>
/// Represents a division.
/// </summary>
public class Division
{
    public string ExternalId { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string? Name { get; set; }
    public string? Source { get; set; }
}
