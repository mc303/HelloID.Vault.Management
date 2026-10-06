using HelloID.Vault.Core.Models.Ad;

namespace HelloID.Vault.Services.Interfaces;

/// <summary>
/// Runs AD correlation and generates scored match recommendations for uncorrelated persons.
/// </summary>
public interface IAdCorrelationService
{
    /// <summary>Fetches AD accounts, correlates them with vault persons by the configured attribute, and persists results.</summary>
    Task<AdCorrelationSummary> RunCorrelationAsync(AdCorrelationConfig config, string? plainPassword = null, IProgress<string>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>Generates scored recommendations for uncorrelated persons against orphaned AD accounts and persists them.</summary>
    Task<int> GenerateRecommendationsAsync(AdCorrelationConfig config, string? plainPassword = null, IProgress<string>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>Accepts a recommendation: marks it Accepted and writes a ManuallyMatched correlation result.</summary>
    Task AcceptRecommendationAsync(string personId, string adObjectGuid);

    /// <summary>
    /// Bulk-accepts every Proposed recommendation whose score is at least the threshold AND
    /// is the top-scoring candidate for its person. Returns (accepted, skipped) counts.
    /// </summary>
    Task<(int Accepted, int Skipped)> BulkAcceptRecommendationsAsync(double minScore, IProgress<string>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>Rejects a recommendation permanently.</summary>
    Task RejectRecommendationAsync(string personId, string adObjectGuid);

    /// <summary>
    /// Writes the person's correlation value (vault field) to each matched AD account's
    /// correlation attribute. Skips accounts whose value already matches when readable;
    /// writes anyway when the current value is unreadable. Returns a per-outcome summary.
    /// </summary>
    Task<AdAttributeWriteSummary> WriteCorrelationAttributeAsync(AdCorrelationConfig config, string? plainPassword = null, IProgress<string>? progress = null, CancellationToken cancellationToken = default);
}
