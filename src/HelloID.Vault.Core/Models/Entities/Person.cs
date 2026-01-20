namespace HelloID.Vault.Core.Models.Entities;

/// <summary>
/// Represents a person in the HR system.
/// </summary>
public class Person
{
    public string PersonId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? ExternalId { get; set; }
    public string? UserName { get; set; }
    public string? Gender { get; set; }
    public string? HonorificPrefix { get; set; }
    public string? HonorificSuffix { get; set; }
    public string? BirthDate { get; set; } // ISO 8601 format string
    public string? BirthLocality { get; set; }
    public string? MaritalStatus { get; set; }
    public string? Initials { get; set; }
    public string? GivenName { get; set; }
    public string? FamilyName { get; set; }
    public string? FamilyNamePrefix { get; set; }
    public string? Convention { get; set; }
    public string? NickName { get; set; }
    public string? FamilyNamePartner { get; set; }
    public string? FamilyNamePartnerPrefix { get; set; }
    public bool Blocked { get; set; }
    public string? StatusReason { get; set; }
    public bool Excluded { get; set; }
    public bool HrExcluded { get; set; }
    public bool ManualExcluded { get; set; }
    public string? Source { get; set; }

    // Primary Manager fields
    public string? PrimaryManagerPersonId { get; set; }
    public string? PrimaryManagerUpdatedAt { get; set; }
}
