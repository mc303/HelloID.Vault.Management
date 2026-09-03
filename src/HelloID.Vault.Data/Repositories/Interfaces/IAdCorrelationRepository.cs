using HelloID.Vault.Core.Models.Ad;

namespace HelloID.Vault.Data.Repositories.Interfaces;

/// <summary>
/// Repository for AD correlation configuration, results, and match recommendations.
/// </summary>
public interface IAdCorrelationRepository
{
    /// <summary>Creates the AD correlation tables if they do not exist (idempotent, supports existing databases).</summary>
    Task EnsureTablesAsync();

    /// <summary>Loads the single-row config. Never returns null; defaults are applied when no row exists.</summary>
    Task<AdCorrelationConfig> GetConfigAsync();

    /// <summary>Saves the single-row config (upsert).</summary>
    Task SaveConfigAsync(AdCorrelationConfig config);

    /// <summary>Loads all persons flattened for correlation (includes business email).</summary>
    Task<List<CorrelatablePersonDto>> GetCorrelatablePersonsAsync();

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
    Task SetManualMatchAsync(string personId, AdUserDto adUser, string correlationAttribute);

    /// <summary>Clears the manual match for a person (sets status back to NotFound).</summary>
    Task ClearManualMatchAsync(string personId);
}
