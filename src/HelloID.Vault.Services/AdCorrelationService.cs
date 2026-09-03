using System.Text.Json;
using HelloID.Vault.Core.Models.Ad;
using HelloID.Vault.Data.Repositories.Interfaces;
using HelloID.Vault.Services.ActiveDirectory;
using HelloID.Vault.Services.Interfaces;

namespace HelloID.Vault.Services;

/// <summary>
/// Correlates vault persons with Active Directory accounts and generates
/// scored match recommendations for uncorrelated persons.
/// </summary>
public class AdCorrelationService : IAdCorrelationService
{
    private readonly IAdCorrelationRepository _repository;
    private readonly IActiveDirectoryService _activeDirectoryService;

    public AdCorrelationService(IAdCorrelationRepository repository, IActiveDirectoryService activeDirectoryService)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _activeDirectoryService = activeDirectoryService ?? throw new ArgumentNullException(nameof(activeDirectoryService));
    }

    public async Task<AdCorrelationSummary> RunCorrelationAsync(AdCorrelationConfig config, string? plainPassword = null, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        await _repository.EnsureTablesAsync().ConfigureAwait(false);

        progress?.Report("Fetching persons from database...");
        var persons = await _repository.GetCorrelatablePersonsAsync().ConfigureAwait(false);

        progress?.Report("Fetching AD accounts...");
        var adUsers = await _activeDirectoryService.SearchAllUsersAsync(config, plainPassword, progress, cancellationToken).ConfigureAwait(false);

        progress?.Report("Correlating...");
        var now = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");

        // Index AD users by correlation value (string, case-insensitive)
        var adByCorrelation = adUsers
            .Where(u => !string.IsNullOrWhiteSpace(u.CorrelationValue))
            .GroupBy(u => u.CorrelationValue!.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var results = new List<AdCorrelationResult>(persons.Count);
        var matchedGuids = new HashSet<string>();

        foreach (var person in persons)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var correlationValue = GetVaultFieldValue(person, config.VaultField);
            var result = new AdCorrelationResult
            {
                PersonId = person.PersonId,
                ExternalId = person.ExternalId,
                CorrelationValue = correlationValue,
                CorrelationAttribute = config.CorrelationAttribute,
                CorrelatedAt = now
            };

            if (!string.IsNullOrWhiteSpace(correlationValue) &&
                adByCorrelation.TryGetValue(correlationValue.Trim(), out var matches))
            {
                if (matches.Count == 1)
                {
                    var ad = matches[0];
                    result.Status = AdCorrelationStatus.Matched;
                    FillAdFields(result, ad);
                    matchedGuids.Add(ad.ObjectGuid);
                }
                else
                {
                    // Multiple AD accounts share the correlation value
                    result.Status = AdCorrelationStatus.Ambiguous;
                    result.AdDisplayName = $"{matches.Count} matches: " + string.Join(", ", matches.Select(m => m.DisplayName ?? m.SamAccountName));
                }
            }
            else
            {
                result.Status = AdCorrelationStatus.NotFound;
            }

            results.Add(result);
        }

        progress?.Report("Saving results...");
        await _repository.ReplaceResultsAsync(results).ConfigureAwait(false);

        var summary = new AdCorrelationSummary
        {
            TotalPersons = results.Count,
            Matched = results.Count(r => r.Status == AdCorrelationStatus.Matched),
            NotFound = results.Count(r => r.Status == AdCorrelationStatus.NotFound),
            Ambiguous = results.Count(r => r.Status == AdCorrelationStatus.Ambiguous),
            ManuallyMatched = results.Count(r => r.Status == AdCorrelationStatus.ManuallyMatched),
            AdAccountsTotal = adUsers.Count,
            OrphanedAdAccounts = adUsers.Count(u =>
                !matchedGuids.Contains(u.ObjectGuid) &&
                !string.IsNullOrWhiteSpace(u.CorrelationValue))
        };

        progress?.Report($"Done: {summary.Matched} matched, {summary.NotFound} not found, {summary.Ambiguous} ambiguous, {summary.OrphanedAdAccounts} orphaned AD accounts");
        return summary;
    }

    public async Task<int> GenerateRecommendationsAsync(AdCorrelationConfig config, string? plainPassword = null, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        await _repository.EnsureTablesAsync().ConfigureAwait(false);

        progress?.Report("Loading correlation results...");
        var allResults = await _repository.GetResultsAsync().ConfigureAwait(false);

        var unmatched = allResults
            .Where(r => r.Status is AdCorrelationStatus.NotFound or AdCorrelationStatus.Ambiguous)
            .Select(r => r.PersonId)
            .ToHashSet();

        if (unmatched.Count == 0)
        {
            progress?.Report("No uncorrelated persons - nothing to recommend");
            return 0;
        }

        progress?.Report("Fetching persons...");
        var persons = (await _repository.GetCorrelatablePersonsAsync().ConfigureAwait(false))
            .Where(p => unmatched.Contains(p.PersonId))
            .ToDictionary(p => p.PersonId);

        progress?.Report("Fetching AD accounts...");
        var adUsers = await _activeDirectoryService.SearchAllUsersAsync(config, plainPassword, progress, cancellationToken).ConfigureAwait(false);

        // Orphaned AD accounts: not matched/ManuallyMatched to any person
        var matchedGuids = allResults
            .Where(r => r.Status is AdCorrelationStatus.Matched or AdCorrelationStatus.ManuallyMatched)
            .Where(r => !string.IsNullOrEmpty(r.AdObjectGuid))
            .Select(r => r.AdObjectGuid!)
            .ToHashSet();

        var orphans = adUsers.Where(u => !matchedGuids.Contains(u.ObjectGuid)).ToList();

        // Load previously rejected pairs so they are not suggested again
        var rejected = (await _repository.GetRecommendationsAsync("Rejected").ConfigureAwait(false))
            .Select(r => (r.PersonId, r.AdObjectGuid))
            .ToHashSet();

        var matchFields = ParseMatchFields(config);

        progress?.Report($"Scoring {unmatched.Count} persons against {orphans.Count} orphaned AD accounts...");
        var now = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
        var recommendations = new List<AdMatchRecommendation>();

        foreach (var person in persons.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var scored = new List<(AdUserDto Ad, double Score, List<FieldScore> Breakdown)>();

            foreach (var ad in orphans)
            {
                if (rejected.Contains((person.PersonId, ad.ObjectGuid)))
                {
                    continue;
                }

                var (score, breakdown) = ScoreCandidate(person, ad, matchFields);
                if (score >= config.MinScore)
                {
                    scored.Add((ad, score, breakdown));
                }
            }

            foreach (var (ad, score, breakdown) in scored
                         .OrderByDescending(s => s.Score)
                         .Take(config.MaxCandidates))
            {
                recommendations.Add(new AdMatchRecommendation
                {
                    PersonId = person.PersonId,
                    AdObjectGuid = ad.ObjectGuid,
                    ScorePercent = Math.Round(score, 1),
                    FieldScoresJson = JsonSerializer.Serialize(breakdown),
                    AdDisplayName = ad.DisplayName,
                    AdSamAccountName = ad.SamAccountName,
                    AdUserPrincipalName = ad.UserPrincipalName,
                    Status = AdRecommendationStatus.Proposed,
                    CreatedAt = now
                });
            }
        }

        progress?.Report("Saving recommendations...");
        await _repository.ReplaceRecommendationsAsync(recommendations).ConfigureAwait(false);

        progress?.Report($"Generated {recommendations.Count} recommendations for {recommendations.Select(r => r.PersonId).Distinct().Count()} persons");
        return recommendations.Count;
    }

    public async Task AcceptRecommendationAsync(string personId, string adObjectGuid)
    {
        var config = await _repository.GetConfigAsync().ConfigureAwait(false);

        var adUser = await _activeDirectoryService.GetUserByGuidAsync(config, adObjectGuid).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"AD account {adObjectGuid} not found");

        await _repository.SetManualMatchAsync(personId, adUser, config.CorrelationAttribute).ConfigureAwait(false);
        await _repository.UpdateRecommendationStatusAsync(personId, adObjectGuid, AdRecommendationStatus.Accepted).ConfigureAwait(false);
    }

    public async Task RejectRecommendationAsync(string personId, string adObjectGuid)
    {
        await _repository.UpdateRecommendationStatusAsync(personId, adObjectGuid, AdRecommendationStatus.Rejected).ConfigureAwait(false);
    }

    private static List<AdMatchFieldConfig> ParseMatchFields(AdCorrelationConfig config)
    {
        if (!string.IsNullOrWhiteSpace(config.MatchFieldsJson))
        {
            try
            {
                var fields = JsonSerializer.Deserialize<List<AdMatchFieldConfig>>(config.MatchFieldsJson);
                if (fields != null && fields.Any(f => f.Enabled))
                {
                    return fields;
                }
            }
            catch (JsonException)
            {
                // Fall back to defaults
            }
        }
        return AdCorrelationConfig.DefaultMatchFields();
    }

    private static (double Score, List<FieldScore> Breakdown) ScoreCandidate(
        CorrelatablePersonDto person, AdUserDto ad, List<AdMatchFieldConfig> matchFields)
    {
        var enabled = matchFields.Where(f => f.Enabled).ToList();
        var totalWeight = enabled.Sum(f => f.Weight);

        if (totalWeight <= 0 || enabled.Count == 0)
        {
            return (0, new List<FieldScore>());
        }

        var breakdown = new List<FieldScore>();
        var weightedSum = 0.0;

        foreach (var field in enabled)
        {
            var personValue = GetVaultFieldValue(person, field.VaultField);
            var adValue = GetAdAttributeValue(ad, field.AdAttribute);

            double score;
            if (field.VaultField == "DisplayName")
            {
                score = AdMatchScorer.ScoreDisplayNames(personValue, adValue);
            }
            else
            {
                score = AdMatchScorer.ScoreStrings(personValue, adValue);
            }

            var normalizedWeight = field.Weight / totalWeight;
            weightedSum += score * normalizedWeight;

            breakdown.Add(new FieldScore
            {
                Field = field.VaultField,
                AdAttribute = field.AdAttribute,
                Score = Math.Round(score, 1),
                Weight = Math.Round(normalizedWeight, 3)
            });
        }

        return (weightedSum, breakdown);
    }

    private static string? GetVaultFieldValue(CorrelatablePersonDto person, string vaultField) => (vaultField ?? string.Empty).ToLowerInvariant() switch
    {
        "external_id" or "externalid" => person.ExternalId,
        "display_name" or "displayname" => person.DisplayName,
        "given_name" or "givenname" => person.GivenName,
        "family_name" or "familyname" => person.FamilyName,
        "nick_name" or "nickname" => person.NickName,
        "family_name_partner" or "familynamepartner" => person.FamilyNamePartner,
        "user_name" or "username" => person.UserName,
        "businessemail" => person.BusinessEmail,
        _ => null
    };

    private static string? GetAdAttributeValue(AdUserDto ad, string adAttribute) => adAttribute switch
    {
        "sAMAccountName" => ad.SamAccountName,
        "userPrincipalName" => ad.UserPrincipalName,
        "displayName" => ad.DisplayName,
        "givenName" => ad.GivenName,
        "sn" => ad.Surname,
        "mail" => ad.Mail,
        "employeeID" => ad.CorrelationValue,
        _ => ad.Attributes.TryGetValue(adAttribute, out var value) ? value : null
    };

    private static void FillAdFields(AdCorrelationResult result, AdUserDto ad)
    {
        result.AdObjectGuid = ad.ObjectGuid;
        result.AdDistinguishedName = ad.DistinguishedName;
        result.AdSamAccountName = ad.SamAccountName;
        result.AdUserPrincipalName = ad.UserPrincipalName;
        result.AdDisplayName = ad.DisplayName;
        result.AdMail = ad.Mail;
        result.AdEnabled = ad.Enabled;
    }
}

/// <summary>
/// Per-field score breakdown stored as JSON with each recommendation.
/// </summary>
public class FieldScore
{
    public string Field { get; set; } = string.Empty;
    public string AdAttribute { get; set; } = string.Empty;
    public double Score { get; set; }
    public double Weight { get; set; }
}
