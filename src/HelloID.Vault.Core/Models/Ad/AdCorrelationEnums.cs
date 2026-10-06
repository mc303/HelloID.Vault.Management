namespace HelloID.Vault.Core.Models.Ad;

/// <summary>
/// Status of a person's correlation to an Active Directory account.
/// </summary>
public enum AdCorrelationStatus
{
    /// <summary>AD account found via the configured correlation attribute.</summary>
    Matched,

    /// <summary>No AD account found for this person.</summary>
    NotFound,

    /// <summary>Multiple AD accounts matched the correlation value.</summary>
    Ambiguous,

    /// <summary>Match confirmed manually by a user via a recommendation.</summary>
    ManuallyMatched
}

/// <summary>
/// Status of a match recommendation for an uncorrelated person.
/// </summary>
public enum AdRecommendationStatus
{
    Proposed,
    Accepted,
    Rejected
}

/// <summary>
/// How a person-to-AD-account match was established.
/// </summary>
public static class AdMatchMethod
{
    public const string Attribute = "Attribute";
    public const string DisplayName = "DisplayName";
    public const string Manual = "Manual";
}

/// <summary>
/// Whether the AD account's correlation attribute holds the person's vault value.
/// </summary>
public static class AdValueStatus
{
    public const string Correct = "Correct";
    public const string Missing = "Missing";
    public const string Different = "Different";
    public const string Unreadable = "Unreadable";
}
