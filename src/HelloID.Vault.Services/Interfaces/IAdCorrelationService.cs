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

    /// <summary>Rejects a recommendation permanently.</summary>
    Task RejectRecommendationAsync(string personId, string adObjectGuid);
}
