using System.Text.Json.Serialization;

namespace HelloID.Vault.Core.Models.Ad;

/// <summary>
/// Configuration for the Active Directory correlation feature (single row, id = 1).
/// </summary>
public class AdCorrelationConfig
{
    [JsonPropertyName("ldapHost")]
    public string LdapHost { get; set; } = string.Empty;

    [JsonPropertyName("ldapPort")]
    public int LdapPort { get; set; } = 636;

    [JsonPropertyName("useLdaps")]
    public bool UseLdaps { get; set; } = true;

    [JsonPropertyName("searchBase")]
    public string SearchBase { get; set; } = string.Empty;

    [JsonPropertyName("username")]
    public string? Username { get; set; }

    /// <summary>Stored encrypted (DPAPI). Never returned in plain text to callers that load config for display.</summary>
    [JsonPropertyName("encryptedPassword")]
    public string? EncryptedPassword { get; set; }

    /// <summary>AD attribute used for correlation (default employeeID).</summary>
    [JsonPropertyName("correlationAttribute")]
    public string CorrelationAttribute { get; set; } = "employeeID";

    /// <summary>Vault persons column used for correlation: external_id, user_name, or display_name.</summary>
    [JsonPropertyName("vaultField")]
    public string VaultField { get; set; } = "external_id";

    /// <summary>
    /// When true, only persons entitled to an AD account are correlated:
    /// active contract, or a future contract starting within FutureContractDays.
    /// </summary>
    [JsonPropertyName("activeContractOnly")]
    public bool ActiveContractOnly { get; set; } = true;

    /// <summary>
    /// Include persons whose contract starts within this many days. 0 = active contracts only.
    /// </summary>
    [JsonPropertyName("futureContractDays")]
    public int FutureContractDays { get; set; } = 30;

    /// <summary>JSON list of AdMatchFieldConfig used by the recommendation engine.</summary>
    [JsonPropertyName("matchFieldsJson")]
    public string? MatchFieldsJson { get; set; }

    /// <summary>Minimum score (0-100) for a recommendation to be stored.</summary>
    [JsonPropertyName("minScore")]
    public int MinScore { get; set; } = 50;

    /// <summary>Maximum number of candidates stored per person.</summary>
    [JsonPropertyName("maxCandidates")]
    public int MaxCandidates { get; set; } = 3;

    /// <summary>Minimum score (0-100) for bulk-accepting a person's top recommendation.</summary>
    [JsonPropertyName("acceptAllThreshold")]
    public int AcceptAllThreshold { get; set; } = 95;

    [JsonPropertyName("updatedAt")]
    public string? UpdatedAt { get; set; }

    /// <summary>Default match field configuration used when none is stored yet.</summary>
    public static List<AdMatchFieldConfig> DefaultMatchFields() => new()
    {
        new() { VaultField = "FamilyName", AdAttribute = "sn", Weight = 0.25, Enabled = true },
        new() { VaultField = "GivenName", AdAttribute = "givenName", Weight = 0.25, Enabled = true },
        new() { VaultField = "DisplayName", AdAttribute = "displayName", Weight = 0.15, Enabled = true },
        new() { VaultField = "UserName", AdAttribute = "userPrincipalName", Weight = 0.15, Enabled = true },
        new() { VaultField = "BusinessEmail", AdAttribute = "mail", Weight = 0.10, Enabled = true },
        new() { VaultField = "ExternalId", AdAttribute = "employeeID", Weight = 0.10, Enabled = true }
    };
}

/// <summary>
/// A configurable field pair (vault person field vs AD attribute) with a weight
/// used by the recommendation scoring engine.
/// </summary>
public class AdMatchFieldConfig
{
    [JsonPropertyName("vaultField")]
    public string VaultField { get; set; } = string.Empty;

    [JsonPropertyName("adAttribute")]
    public string AdAttribute { get; set; } = string.Empty;

    /// <summary>Relative weight (0-1). Weights of enabled fields are normalized during scoring.</summary>
    [JsonPropertyName("weight")]
    public double Weight { get; set; }

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;
}
