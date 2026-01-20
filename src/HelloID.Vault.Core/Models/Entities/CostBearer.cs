namespace HelloID.Vault.Core.Models.Entities;

/// <summary>
/// Represents a cost bearer.
/// </summary>
public class CostBearer
{
    public string ExternalId { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string? Name { get; set; }
    public string? Source { get; set; }
}
