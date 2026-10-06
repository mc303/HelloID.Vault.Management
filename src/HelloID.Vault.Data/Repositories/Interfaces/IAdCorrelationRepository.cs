using HelloID.Vault.Core.Models.Ad;

namespace HelloID.Vault.Data.Repositories.Interfaces;

/// <summary>
/// Repository for AD correlation configuration, results, and match recommendations.
/// </summary>
public interface IAdCorrelationRepository
{
    /// <summary>Creates the AD correlation tables if they do not exist (idempotent, supports existing databases).</summary>
    Task EnsureTablesAsync();

    /// <summary>Loads persons flattened for correlation (includes business email). When activeOnly, only persons with an active contract or one starting within futureDays.</summary>
    Task<List<CorrelatablePersonDto>> GetCorrelatablePersonsAsync(bool activeOnly = true, int futureDays = 30);

    /// <summary>Replaces correlation results for a run. Existing ManuallyMatched rows are preserved for persons that did not match by attribute.</summary>
    Task ReplaceResultsAsync(IEnumerable<AdCorrelationResult> results);

    /// <summary>Loads correlation results joined with persons for display. Optional status filter.</summary>
    Task<List<AdCorrelationResult>> GetResultsAsync(string? statusFilter = null);

    /// <summary>Replaces Proposed recommendations (Accepted/Rejected history is kept).</summary>
    Task ReplaceRecommendationsAsync(IEnumerable<AdMatchRecommendation> recommendations);

    /// <summary>Loads recommendations joined with persons for display. Optional status filter.</summary>
    Task<List<AdMatchRecommendation>> GetRecommendationsAsync(string? statusFilter = null);

    /// <summary>Updates the status of a recommendation.</summary>
    Task UpdateRecommendationStatusAsync(string personId, string adObjectGuid, AdRecommendationStatus status);

    /// <summary>Writes a ManuallyMatched correlation result for a person.</summary>
    Task SetManualMatchAsync(string personId, AdUserDto adUser, string correlationAttribute, string? personValue = null);

    /// <summary>Clears the manual match for a person (sets status back to NotFound).</summary>
    Task ClearManualMatchAsync(string personId);

    /// <summary>Deletes all correlation results. Returns the number of rows removed.</summary>
    Task<int> ClearResultsAsync();

    /// <summary>Deletes ALL recommendations, including Accepted/Rejected history. Returns the number of rows removed.</summary>
    Task<int> ClearRecommendationsAsync();
}
