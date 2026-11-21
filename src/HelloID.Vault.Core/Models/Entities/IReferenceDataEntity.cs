namespace HelloID.Vault.Core.Models.Entities;

/// <summary>
/// Shared shape of the simple reference data entities (title, team, location,
/// employer, cost center, cost bearer, division, organization).
/// </summary>
public interface IReferenceDataEntity
{
    string ExternalId { get; set; }
    string? Code { get; set; }
    string? Name { get; set; }
    string? Source { get; set; }
}
