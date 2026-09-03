namespace HelloID.Vault.Core.Models.Ad;

/// <summary>
/// Active Directory user object retrieved via LDAP (minimal attribute set).
/// </summary>
public class AdUserDto
{
    public string ObjectGuid { get; set; } = string.Empty;
    public string DistinguishedName { get; set; } = string.Empty;
    public string? SamAccountName { get; set; }
    public string? UserPrincipalName { get; set; }
    public string? DisplayName { get; set; }
    public string? GivenName { get; set; }
    public string? Surname { get; set; }
    public string? Mail { get; set; }
    public bool Enabled { get; set; } = true;

    /// <summary>Value of the configured correlation attribute (e.g. employeeID).</summary>
    public string? CorrelationValue { get; set; }

    /// <summary>Optional dynamic attributes requested at search time (attribute name -> value).</summary>
    public Dictionary<string, string?> Attributes { get; set; } = new();
}

/// <summary>
/// A person from the vault database, flattened for correlation and matching.
/// </summary>
public class CorrelatablePersonDto
{
    public string PersonId { get; set; } = string.Empty;
    public string? ExternalId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? GivenName { get; set; }
    public string? FamilyName { get; set; }
    public string? NickName { get; set; }
    public string? FamilyNamePartner { get; set; }
    public string? UserName { get; set; }
    public string? BusinessEmail { get; set; }
}
