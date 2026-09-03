using System.Text.Json.Serialization;

namespace HelloID.Vault.Core.Models.Ad;

/// <summary>
/// Persisted correlation result for a person (one row per person).
/// </summary>
public class AdCorrelationResult
{
    public string PersonId { get; set; } = string.Empty;
    public string? ExternalId { get; set; }

    public string? AdObjectGuid { get; set; }
    public string? AdDistinguishedName { get; set; }
    public string? AdSamAccountName { get; set; }
    public string? AdUserPrincipalName { get; set; }
    public string? AdDisplayName { get; set; }
    public string? AdMail { get; set; }
    public bool? AdEnabled { get; set; }

    public string? CorrelationValue { get; set; }
    public string? CorrelationAttribute { get; set; }

    [JsonIgnore]
    public AdCorrelationStatus Status { get; set; }

    /// <summary>String form of <see cref="Status"/> for persistence.</summary>
    public string StatusText
    {
        get => Status.ToString();
        set => Status = Enum.TryParse<AdCorrelationStatus>(value, true, out var s) ? s : AdCorrelationStatus.NotFound;
    }

    public string? CorrelatedAt { get; set; }

    // Display helpers (joined from persons when loading results)
    public string? PersonDisplayName { get; set; }
}

/// <summary>
/// Summary counts of a correlation run.
/// </summary>
public class AdCorrelationSummary
{
    public int TotalPersons { get; set; }
    public int Matched { get; set; }
    public int NotFound { get; set; }
    public int Ambiguous { get; set; }
    public int ManuallyMatched { get; set; }
    public int AdAccountsTotal { get; set; }
    public int OrphanedAdAccounts { get; set; }
    public DateTime RanAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// A scored match suggestion for an uncorrelated person against an orphaned AD account.
/// </summary>
public class AdMatchRecommendation
{
    public string PersonId { get; set; } = string.Empty;
    public string AdObjectGuid { get; set; } = string.Empty;

    public double ScorePercent { get; set; }

    /// <summary>JSON breakdown: [{field, adAttribute, score, weight}] per evaluated field pair.</summary>
    public string? FieldScoresJson { get; set; }

    [JsonIgnore]
    public AdRecommendationStatus Status { get; set; }

    public string StatusText
    {
        get => Status.ToString();
        set => Status = Enum.TryParse<AdRecommendationStatus>(value, true, out var s) ? s : AdRecommendationStatus.Proposed;
    }

    public string? CreatedAt { get; set; }

    // Display helpers (joined when loading)
    public string? PersonDisplayName { get; set; }
    public string? PersonExternalId { get; set; }
    public string? AdDisplayName { get; set; }
    public string? AdSamAccountName { get; set; }
    public string? AdUserPrincipalName { get; set; }
}
