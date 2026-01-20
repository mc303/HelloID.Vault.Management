using System.Text.Json.Serialization;

namespace HelloID.Vault.Core.Models.Json;

/// <summary>
/// Person object from vault.json.
/// </summary>
public class VaultPerson
{
    [JsonPropertyName("PersonId")]
    public string PersonId { get; set; } = string.Empty;

    [JsonPropertyName("PersonVersion")]
    public string? PersonVersion { get; set; }

    [JsonPropertyName("DisplayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("ExternalId")]
    public string? ExternalId { get; set; }

    [JsonPropertyName("UserName")]
    public string? UserName { get; set; }

    [JsonPropertyName("Location")]
    public VaultReference? Location { get; set; }

    [JsonPropertyName("Details")]
    public VaultPersonDetails? Details { get; set; }

    [JsonPropertyName("Name")]
    public VaultPersonName? Name { get; set; }

    [JsonPropertyName("Status")]
    public VaultPersonStatus? Status { get; set; }

    [JsonPropertyName("Contact")]
    public VaultPersonContact? Contact { get; set; }

    [JsonPropertyName("Excluded")]
    public bool Excluded { get; set; }

    [JsonPropertyName("ExclusionDetails")]
    public VaultExclusionDetails? ExclusionDetails { get; set; }

    [JsonPropertyName("PrimaryManager")]
    public VaultManagerReference? PrimaryManager { get; set; }

    [JsonPropertyName("Source")]
    public VaultSource? Source { get; set; }

    [JsonPropertyName("Custom")]
    public Dictionary<string, object?>? Custom { get; set; }

    [JsonPropertyName("Contracts")]
    public List<VaultContract> Contracts { get; set; } = new();

    [JsonPropertyName("PrimaryContract")]
    public VaultContract? PrimaryContract { get; set; }
}

public class VaultPersonDetails
{
    [JsonPropertyName("Gender")]
    public string? Gender { get; set; }

    [JsonPropertyName("HonorificPrefix")]
    public string? HonorificPrefix { get; set; }

    [JsonPropertyName("HonorificSuffix")]
    public string? HonorificSuffix { get; set; }

    [JsonPropertyName("BirthDate")]
    public DateTime? BirthDate { get; set; }

    [JsonPropertyName("BirthLocality")]
    public string? BirthLocality { get; set; }

    [JsonPropertyName("MaritalStatus")]
    public string? MaritalStatus { get; set; }
}

public class VaultPersonName
{
    [JsonPropertyName("Initials")]
    public string? Initials { get; set; }

    [JsonPropertyName("GivenName")]
    public string? GivenName { get; set; }

    [JsonPropertyName("NickName")]
    public string? NickName { get; set; }

    [JsonPropertyName("FamilyName")]
    public string? FamilyName { get; set; }

    [JsonPropertyName("FamilyNamePrefix")]
    public string? FamilyNamePrefix { get; set; }

    [JsonPropertyName("FamilyNamePartner")]
    public string? FamilyNamePartner { get; set; }

    [JsonPropertyName("FamilyNamePartnerPrefix")]
    public string? FamilyNamePartnerPrefix { get; set; }

    [JsonPropertyName("Convention")]
    public string? Convention { get; set; }
}

public class VaultPersonStatus
{
    [JsonPropertyName("Blocked")]
    public bool Blocked { get; set; }

    [JsonPropertyName("Reason")]
    public string? Reason { get; set; }
}

public class VaultPersonContact
{
    [JsonPropertyName("Personal")]
    public VaultContactInfo? Personal { get; set; }

    [JsonPropertyName("Business")]
    public VaultContactInfo? Business { get; set; }
}

public class VaultContactInfo
{
    [JsonPropertyName("Address")]
    public VaultAddress? Address { get; set; }

    [JsonPropertyName("Phone")]
    public VaultPhone? Phone { get; set; }

    [JsonPropertyName("Email")]
    public string? Email { get; set; }
}

public class VaultAddress
{
    [JsonPropertyName("Street")]
    public string? Street { get; set; }

    [JsonPropertyName("HouseNumber")]
    public string? HouseNumber { get; set; }

    [JsonPropertyName("PostalCode")]
    public string? PostalCode { get; set; }

    [JsonPropertyName("Locality")]
    public string? Locality { get; set; }

    [JsonPropertyName("Country")]
    public string? Country { get; set; }
}

public class VaultPhone
{
    [JsonPropertyName("Mobile")]
    public string? Mobile { get; set; }

    [JsonPropertyName("Fixed")]
    public string? Fixed { get; set; }
}

public class VaultExclusionDetails
{
    [JsonPropertyName("Hr")]
    public bool Hr { get; set; }

    [JsonPropertyName("Manual")]
    public bool Manual { get; set; }
}

public class VaultSource
{
    [JsonPropertyName("SystemId")]
    public string? SystemId { get; set; }

    [JsonPropertyName("DisplayName")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("IdentificationKey")]
    public string? IdentificationKey { get; set; }
}
