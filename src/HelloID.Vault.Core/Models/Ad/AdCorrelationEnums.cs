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
