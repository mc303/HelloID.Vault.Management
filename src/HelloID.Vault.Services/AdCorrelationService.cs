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
    private readonly HelloID.Vault.Services.Security.IEncryptionService? _encryptionService;
    private readonly IUserPreferencesService? _userPreferencesService;

    public AdCorrelationService(
        IAdCorrelationRepository repository,
        IActiveDirectoryService activeDirectoryService,
        IUserPreferencesService? userPreferencesService = null,
        HelloID.Vault.Services.Security.IEncryptionService? encryptionService = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _activeDirectoryService = activeDirectoryService ?? throw new ArgumentNullException(nameof(activeDirectoryService));
        _userPreferencesService = userPreferencesService;
        _encryptionService = encryptionService;
    }

    private AdCorrelationConfig BuildConfigFromPreferences()
    {
        if (_userPreferencesService == null)
        {
            return new AdCorrelationConfig();
        }
        return new AdCorrelationConfig
        {
            LdapHost = _userPreferencesService.AdLdapHost ?? string.Empty,
            LdapPort = _userPreferencesService.AdUseLdaps ? 636 : 389,
            UseLdaps = _userPreferencesService.AdUseLdaps,
            SearchBase = _userPreferencesService.AdSearchBase ?? string.Empty,
            Username = _userPreferencesService.AdUsername,
            EncryptedPassword = _userPreferencesService.AdEncryptedPassword,
            CorrelationAttribute = _userPreferencesService.AdCorrelationAttribute,
            VaultField = _userPreferencesService.AdVaultField,
            ActiveContractOnly = _userPreferencesService.AdActiveContractOnly,
            FutureContractDays = _userPreferencesService.AdFutureContractDays,
            MatchFieldsJson = _userPreferencesService.AdMatchFieldsJson,
            MinScore = _userPreferencesService.AdMinScore,
            MaxCandidates = _userPreferencesService.AdMaxCandidates,
            AcceptAllThreshold = _userPreferencesService.AdAcceptAllThreshold
        };
    }

    /// <summary>
    /// Uses the caller-provided password when given, otherwise decrypts the stored
    /// (DPAPI) LDAP password so no code path can hit AD unauthenticated.
    /// </summary>
    private string? ResolveLdapPassword(AdCorrelationConfig config, string? plainPassword)
    {
        if (!string.IsNullOrEmpty(plainPassword))
        {
            return plainPassword;
        }
        if (_encryptionService == null || string.IsNullOrEmpty(config.EncryptedPassword))
        {
            return null;
        }
        try
        {
            return _encryptionService.Decrypt(config.EncryptedPassword);
        }
        catch
        {
            return null; // Unreadable (different machine/user) - caller reports the bind failure
        }
    }

    public async Task<AdCorrelationSummary> RunCorrelationAsync(AdCorrelationConfig config, string? plainPassword = null, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        await _repository.EnsureTablesAsync().ConfigureAwait(false);
        plainPassword = ResolveLdapPassword(config, plainPassword);

        progress?.Report("Fetching persons from database...");
        var persons = await _repository.GetCorrelatablePersonsAsync(config.ActiveContractOnly, config.FutureContractDays).ConfigureAwait(false);

        progress?.Report("Fetching AD accounts...");
        var adUsers = await _activeDirectoryService.SearchAllUsersAsync(config, plainPassword, progress, cancellationToken).ConfigureAwait(false);

        progress?.Report("Correlating...");
        var now = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");

        // Index AD users by correlation value (string, case-insensitive)
        var adByCorrelation = adUsers
            .Where(u => !string.IsNullOrWhiteSpace(u.CorrelationValue))
            .GroupBy(u => u.CorrelationValue!.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        System.Diagnostics.Debug.WriteLine($"[AdCorrelation] vaultField='{config.VaultField}' <-> adAttribute='{config.CorrelationAttribute}'");
        System.Diagnostics.Debug.WriteLine($"[AdCorrelation] {persons.Count} persons, {adUsers.Count} AD users, {adByCorrelation.Count} AD users with non-empty {config.CorrelationAttribute}");

        // Log sample correlation values from both sides for diagnostics
        foreach (var sample in persons.Where(p => !string.IsNullOrWhiteSpace(GetVaultFieldValue(p, config.VaultField))).Take(5))
        {
            var value = GetVaultFieldValue(sample, config.VaultField);
            System.Diagnostics.Debug.WriteLine($"[AdCorrelation] Person sample: {sample.DisplayName} | {config.VaultField}='{value}' | match in AD index: {adByCorrelation.ContainsKey(value!.Trim())}");
        }
        foreach (var sample in adByCorrelation.Values.SelectMany(v => v).Take(5))
        {
            System.Diagnostics.Debug.WriteLine($"[AdCorrelation] AD sample: {sample.SamAccountName ?? "?"} | {config.CorrelationAttribute}='{sample.CorrelationValue}'");
        }

        var results = new List<AdCorrelationResult>(persons.Count);
        var matchedGuids = new HashSet<string>();

        // Fallback index by normalized display name: some LDAP endpoints never return
        // certain attributes (e.g. employeeID) regardless of encryption or rights.
        // Person display_name and AD displayName are identical ("Name (emp-...)") in
        // provisioned environments, making an exact fallback safe and self-verifying.
        var adByDisplayName = adUsers
            .Select(u => (User: u, Name: ResolveDisplayName(u)))
            .Where(x => !string.IsNullOrWhiteSpace(x.Name))
            .GroupBy(x => HelloID.Vault.Services.ActiveDirectory.AdMatchScorer.Normalize(x.Name!), StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Select(x => x.User).ToList());

        var fallbackEnabled = adByCorrelation.Count == 0 && adByDisplayName.Count > 0;
        if (fallbackEnabled)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[AdCorrelation] FALLBACK ENABLED: no AD user returned '{config.CorrelationAttribute}' - correlating by exact display name match instead ({adByDisplayName.Count} AD users indexed)");
        }
        int fallbackMatches = 0;

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

            var matched = false;
            if (!string.IsNullOrWhiteSpace(correlationValue) &&
                adByCorrelation.TryGetValue(correlationValue.Trim(), out var matches))
            {
                if (matches.Count == 1)
                {
                    var ad = matches[0];
                    result.Status = AdCorrelationStatus.Matched;
                    result.MatchMethod = AdMatchMethod.Attribute;
                    result.ValueStatus = AdValueStatus.Correct;
                    FillAdFields(result, ad);
                    matchedGuids.Add(ad.ObjectGuid);
                    matched = true;
                }
                else
                {
                    // Multiple AD accounts share the correlation value
                    result.Status = AdCorrelationStatus.Ambiguous;
                    result.AdDisplayName = $"{matches.Count} matches: " + string.Join(", ", matches.Select(m => m.DisplayName ?? m.SamAccountName));
                    matched = true;
                }
            }

            if (!matched && fallbackEnabled)
            {
                var nameKey = HelloID.Vault.Services.ActiveDirectory.AdMatchScorer.Normalize(person.DisplayName);
                if (nameKey.Length > 0 && adByDisplayName.TryGetValue(nameKey, out var byName))
                {
                    if (byName.Count == 1)
                    {
                        var ad = byName[0];
                        result.Status = AdCorrelationStatus.Matched;
                        result.MatchMethod = AdMatchMethod.DisplayName;
                        result.ValueStatus = EvaluateValueStatus(ad.CorrelationValue, personValue: GetVaultFieldValue(person, config.VaultField));
                        FillAdFields(result, ad);
                        result.CorrelationAttribute = config.CorrelationAttribute + " (fallback: displayName)";
                        result.CorrelationValue = person.DisplayName;
                        matchedGuids.Add(ad.ObjectGuid);
                        fallbackMatches++;
                        matched = true;
                    }
                    else
                    {
                        result.Status = AdCorrelationStatus.Ambiguous;
                        result.AdDisplayName = $"{byName.Count} name matches: " + string.Join(", ", byName.Select(m => m.DisplayName ?? m.SamAccountName));
                        matched = true;
                    }
                }
            }

            if (!matched)
            {
                result.Status = AdCorrelationStatus.NotFound;
            }

            results.Add(result);
        }

        if (fallbackMatches > 0)
        {
            System.Diagnostics.Debug.WriteLine($"[AdCorrelation] Fallback matched {fallbackMatches} persons by display name");
        }

        progress?.Report("Saving results...");
        await _repository.ReplaceResultsAsync(results).ConfigureAwait(false);

        System.Diagnostics.Debug.WriteLine(
            $"[AdCorrelation] Results: {results.Count(r => r.Status == AdCorrelationStatus.Matched)} matched, " +
            $"{results.Count(r => r.Status == AdCorrelationStatus.NotFound)} notFound, " +
            $"{results.Count(r => r.Status == AdCorrelationStatus.Ambiguous)} ambiguous");

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
        plainPassword = ResolveLdapPassword(config, plainPassword);

        progress?.Report("Loading correlation results...");
        var allResults = await _repository.GetResultsAsync().ConfigureAwait(false);

        var unmatched = allResults
            .Where(r => r.Status is AdCorrelationStatus.NotFound or AdCorrelationStatus.Ambiguous)
            .Select(r => r.PersonId)
            .ToHashSet();

        System.Diagnostics.Debug.WriteLine($"[AdRec] GenerateRecommendationsAsync: {allResults.Count} stored results -> {unmatched.Count} unmatched persons");

        if (unmatched.Count == 0)
        {
            progress?.Report("No uncorrelated persons - nothing to recommend");
            return 0;
        }

        progress?.Report("Fetching persons...");
        var persons = (await _repository.GetCorrelatablePersonsAsync(config.ActiveContractOnly, config.FutureContractDays).ConfigureAwait(false))
            .Where(p => unmatched.Contains(p.PersonId))
            .ToDictionary(p => p.PersonId);
        System.Diagnostics.Debug.WriteLine($"[AdRec] Loaded {persons.Count} unmatched person records from DB");

        progress?.Report("Fetching AD accounts...");
        var adUsers = await _activeDirectoryService.SearchAllUsersAsync(config, plainPassword, progress, cancellationToken).ConfigureAwait(false);

        // Orphaned AD accounts: not matched/ManuallyMatched to any person
        var matchedGuids = allResults
            .Where(r => r.Status is AdCorrelationStatus.Matched or AdCorrelationStatus.ManuallyMatched)
            .Where(r => !string.IsNullOrEmpty(r.AdObjectGuid))
            .Select(r => r.AdObjectGuid!)
            .ToHashSet();

        var orphans = adUsers.Where(u => !matchedGuids.Contains(u.ObjectGuid)).ToList();
        System.Diagnostics.Debug.WriteLine($"[AdRec] {adUsers.Count} AD users, {matchedGuids.Count} already matched GUIDs -> {orphans.Count} orphaned AD accounts as candidates");

        // Load previously decided pairs (rejected AND accepted) so they are not
        // suggested again - the table's composite primary key allows a pair only once.
        var decidedPairs = (await _repository.GetRecommendationsAsync().ConfigureAwait(false))
            .Where(r => r.Status is AdRecommendationStatus.Rejected or AdRecommendationStatus.Accepted)
            .Select(r => (r.PersonId, r.AdObjectGuid))
            .ToHashSet();
        System.Diagnostics.Debug.WriteLine($"[AdRec] {decidedPairs.Count} previously decided (accepted/rejected) pairs will be skipped");

        var matchFields = ParseMatchFields(config);
        System.Diagnostics.Debug.WriteLine($"[AdRec] Match fields: {string.Join(", ", matchFields.Select(f => $"{f.VaultField}<->{f.AdAttribute} (w={f.Weight}, on={f.Enabled})"))} | minScore={config.MinScore}, maxCandidates={config.MaxCandidates}");

        progress?.Report($"Scoring {unmatched.Count} persons against {orphans.Count} orphaned AD accounts...");
        var now = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
        var recommendations = new List<AdMatchRecommendation>();

        foreach (var person in persons.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var scored = new List<(AdUserDto Ad, double Score, List<FieldScore> Breakdown)>();

            foreach (var ad in orphans)
            {
                if (decidedPairs.Contains((person.PersonId, ad.ObjectGuid)))
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

                // Log top candidates with per-field breakdown for the first persons
                if (recommendations.Count <= 15)
                {
                    var fields = string.Join(", ", breakdown.Select(b => $"{b.Field}:{b.Score:F0}"));
                    System.Diagnostics.Debug.WriteLine($"[AdRec] Candidate: '{person.DisplayName}' <-> '{ad.DisplayName}' score={score:F1} [{fields}]");
                }
            }

            if (scored.Count == 0 && persons.Count <= 100)
            {
                // Log why a person got no candidates (only for small sets to avoid spam)
                System.Diagnostics.Debug.WriteLine($"[AdRec] No candidate >= {config.MinScore} for '{person.DisplayName}'");
            }
        }

        progress?.Report("Saving recommendations...");
        await _repository.ReplaceRecommendationsAsync(recommendations).ConfigureAwait(false);

        progress?.Report($"Generated {recommendations.Count} recommendations for {recommendations.Select(r => r.PersonId).Distinct().Count()} persons");
        return recommendations.Count;
    }

    public async Task AcceptRecommendationAsync(string personId, string adObjectGuid)
    {
        var config = BuildConfigFromPreferences();
        var password = ResolveLdapPassword(config, null);

        var adUser = await _activeDirectoryService.GetUserByGuidAsync(config, adObjectGuid, password).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"AD account {adObjectGuid} not found");

        // The person's vault value for the value status evaluation
        var persons = await _repository.GetCorrelatablePersonsAsync(activeOnly: false).ConfigureAwait(false);
        var personValue = persons.FirstOrDefault(p => p.PersonId == personId) is { } p
            ? GetVaultFieldValue(p, config.VaultField)
            : null;

        await _repository.SetManualMatchAsync(personId, adUser, config.CorrelationAttribute, personValue).ConfigureAwait(false);
        await _repository.UpdateRecommendationStatusAsync(personId, adObjectGuid, AdRecommendationStatus.Accepted).ConfigureAwait(false);
    }

    public async Task RejectRecommendationAsync(string personId, string adObjectGuid)
    {
        await _repository.UpdateRecommendationStatusAsync(personId, adObjectGuid, AdRecommendationStatus.Rejected).ConfigureAwait(false);
    }

    public async Task<(int Accepted, int Skipped)> BulkAcceptRecommendationsAsync(double minScore, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        await _repository.EnsureTablesAsync().ConfigureAwait(false);

        var proposed = await _repository.GetRecommendationsAsync("Proposed").ConfigureAwait(false);

        // Only the TOP-scoring candidate per person qualifies - never bulk-accept
        // the 2nd/3rd suggestion of a person whose best candidate also qualifies.
        var topPerPerson = proposed
            .GroupBy(r => r.PersonId)
            .Select(g => g.OrderByDescending(r => r.ScorePercent).First())
            .Where(r => r.ScorePercent >= minScore)
            .ToList();

        var accepted = 0;
        var skipped = 0;
        var processed = 0;

        foreach (var recommendation in topPerPerson)
        {
            cancellationToken.ThrowIfCancellationRequested();
            processed++;
            progress?.Report($"[{processed}/{topPerPerson.Count}] {recommendation.PersonDisplayName} ({recommendation.ScorePercent:0.0}%)...");

            try
            {
                await AcceptRecommendationAsync(recommendation.PersonId, recommendation.AdObjectGuid).ConfigureAwait(false);
                accepted++;
            }
            catch (Exception ex)
            {
                skipped++;
                System.Diagnostics.Debug.WriteLine($"[AdRec] Bulk accept failed for {recommendation.PersonDisplayName}: {ex.Message}");
            }
        }

        progress?.Report($"Bulk accept done: {accepted} accepted, {skipped} failed");
        return (accepted, skipped);
    }

    public async Task<AdAttributeWriteSummary> WriteCorrelationAttributeAsync(AdCorrelationConfig config, string? plainPassword = null, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        await _repository.EnsureTablesAsync().ConfigureAwait(false);
        plainPassword = ResolveLdapPassword(config, plainPassword);

        progress?.Report("Loading matched results...");
        var matchedResults = (await _repository.GetResultsAsync().ConfigureAwait(false))
            .Where(r => r.Status is AdCorrelationStatus.Matched or AdCorrelationStatus.ManuallyMatched
                        && !string.IsNullOrWhiteSpace(r.AdObjectGuid))
            .ToList();

        var summary = new AdAttributeWriteSummary { TotalMatched = matchedResults.Count };

        if (matchedResults.Count == 0)
        {
            progress?.Report("No matched persons - nothing to write");
            return summary;
        }

        progress?.Report("Loading persons...");
        var persons = (await _repository.GetCorrelatablePersonsAsync(config.ActiveContractOnly, config.FutureContractDays).ConfigureAwait(false))
            .ToDictionary(p => p.PersonId);

        // Fail-closed audit: AD may not be modified unless the audit log is writable.
        // Opening throws when the log cannot be created or written to.
        var identity = string.IsNullOrWhiteSpace(config.Username) ? "integrated(process identity)" : config.Username;
        using var audit = HelloID.Vault.Services.ActiveDirectory.AdAuditLogger.Open();
        audit.TryWrite($"CONTEXT | host={config.LdapHost}:{config.LdapPort} ldaps={config.UseLdaps} identity={identity} attribute={config.CorrelationAttribute} sourceField={config.VaultField} accounts={summary.TotalMatched}");

        var processed = 0;
        foreach (var result in matchedResults)
        {
            cancellationToken.ThrowIfCancellationRequested();
            processed++;

            if (!persons.TryGetValue(result.PersonId, out var person))
            {
                summary.SkippedNoValue++;
                continue;
            }

            var personValue = GetVaultFieldValue(person, config.VaultField);
            if (string.IsNullOrWhiteSpace(personValue))
            {
                summary.SkippedNoValue++;
                System.Diagnostics.Debug.WriteLine($"[AdWrite] SKIP (no {config.VaultField}): {person.DisplayName}");
                continue;
            }
            personValue = personValue.Trim();

            progress?.Report($"[{processed}/{summary.TotalMatched}] {person.DisplayName}...");

            // Hybrid read-check: read the account's current attribute value first
            var user = await _activeDirectoryService.GetUserByGuidAsync(config, result.AdObjectGuid!, plainPassword).ConfigureAwait(false);
            if (user == null)
            {
                summary.Failed++;
                summary.Errors.Add($"{person.DisplayName}: AD account not found");
                System.Diagnostics.Debug.WriteLine($"[AdWrite] FAILED (not found): {person.DisplayName}");
                audit.TryWrite($"FAILED   | {person.DisplayName} | account {result.AdObjectGuid} not found");
                continue;
            }

            var currentValue = user.CorrelationValue?.Trim();
            if (!string.IsNullOrWhiteSpace(currentValue) &&
                string.Equals(currentValue, personValue, StringComparison.OrdinalIgnoreCase))
            {
                summary.AlreadyCorrect++;
                System.Diagnostics.Debug.WriteLine($"[AdWrite] SKIP (already correct): {user.SamAccountName} = '{currentValue}'");
                audit.TryWrite($"SKIP     | {user.SamAccountName} | {config.CorrelationAttribute}: '{currentValue}' (already correct) | by {identity}");
                continue;
            }

            // Audit BEFORE the modification: a modification is never without a record
            var oldDisplay = string.IsNullOrWhiteSpace(currentValue) ? (currentValue == null ? "<unreadable>" : "''") : $"'{currentValue}'";
            if (!audit.TryWrite($"PENDING  | {user.SamAccountName} | {config.CorrelationAttribute}: {oldDisplay} -> '{personValue}' | by {identity}"))
            {
                summary.Failed++;
                summary.Errors.Add("AUDIT FAILURE - run halted before any further modification. Check the audit log file permissions.");
                System.Diagnostics.Debug.WriteLine("[AdWrite] *** AUDIT WRITE FAILED - halting run (AD not modified for this account) ***");
                break;
            }

            System.Diagnostics.Debug.WriteLine($"[AdWrite] MODIFY '{user.SamAccountName}' (UPN={user.UserPrincipalName ?? "?"}, guid={user.ObjectGuid})");
            System.Diagnostics.Debug.WriteLine($"[AdWrite]   {config.CorrelationAttribute}: {oldDisplay} -> '{personValue}'");

            var (success, message) = await _activeDirectoryService
                .SetUserAttributeAsync(config, result.AdObjectGuid!, config.CorrelationAttribute, personValue, plainPassword)
                .ConfigureAwait(false);

            if (success)
            {
                if (string.IsNullOrWhiteSpace(currentValue))
                {
                    summary.UnreadableWritten++;
                }
                else
                {
                    summary.Written++;
                }
                audit.TryWrite($"MODIFIED | {user.SamAccountName} | {config.CorrelationAttribute}: {oldDisplay} -> '{personValue}' | by {identity} | Success");
            }
            else
            {
                summary.Failed++;
                summary.Errors.Add($"{person.DisplayName}: {message}");
                audit.TryWrite($"FAILED   | {user.SamAccountName} | {config.CorrelationAttribute} -> '{personValue}' | by {identity} | {message}");
            }
        }

        audit.TryWrite($"RUN-END   | {summary.TotalMatched} processed - {summary.Written} written, {summary.AlreadyCorrect} already correct, {summary.UnreadableWritten} unreadable-written, {summary.SkippedNoValue} skipped, {summary.Failed} failed");
        progress?.Report($"Done: {summary.Written} written, {summary.AlreadyCorrect} already correct, {summary.UnreadableWritten} unreadable-written, {summary.SkippedNoValue} skipped, {summary.Failed} failed");
        return summary;
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

    private static string? ResolveDisplayName(AdUserDto ad)
    {
        if (!string.IsNullOrWhiteSpace(ad.DisplayName))
        {
            return ad.DisplayName;
        }
        return ad.Attributes.TryGetValue("cn", out var cn) ? cn : null;
    }

    /// <summary>
    /// Compares the AD account's current correlation value against the person's
    /// vault value: Correct, Missing (empty), Different, or Unreadable (withheld).
    /// </summary>
    private static string EvaluateValueStatus(string? adValue, string? personValue)
    {
        if (adValue == null)
        {
            return AdValueStatus.Unreadable;
        }
        if (string.IsNullOrWhiteSpace(adValue))
        {
            return AdValueStatus.Missing;
        }
        return string.Equals(adValue.Trim(), personValue?.Trim(), StringComparison.OrdinalIgnoreCase)
            ? AdValueStatus.Correct
            : AdValueStatus.Different;
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
