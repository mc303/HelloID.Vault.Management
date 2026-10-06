using Dapper;
using HelloID.Vault.Core.Models.Ad;
using HelloID.Vault.Data.Connection;
using HelloID.Vault.Data.Repositories.Interfaces;

namespace HelloID.Vault.Data.Repositories;

/// <summary>
/// Dapper-based AD correlation repository (SQLite and PostgreSQL).
/// Tables are ensured idempotently so existing databases are migrated on first use.
/// </summary>
public class AdCorrelationRepository : IAdCorrelationRepository
{
    private readonly IDatabaseConnectionFactory _connectionFactory;

    public AdCorrelationRepository(IDatabaseConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task EnsureTablesAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(@"
                        CREATE TABLE IF NOT EXISTS ad_correlation_results (
                person_id TEXT PRIMARY KEY,
                external_id TEXT,
                ad_object_guid TEXT,
                ad_distinguished_name TEXT,
                ad_sam_account_name TEXT,
                ad_user_principal_name TEXT,
                ad_display_name TEXT,
                ad_mail TEXT,
                ad_enabled INTEGER,
                correlation_value TEXT,
                correlation_attribute TEXT,
                status TEXT NOT NULL,
                match_method TEXT,
                value_status TEXT,
                correlated_at TEXT
            );
            CREATE TABLE IF NOT EXISTS ad_match_recommendations (
                person_id TEXT NOT NULL,
                ad_object_guid TEXT NOT NULL,
                score_percent REAL NOT NULL,
                field_scores_json TEXT,
                ad_display_name TEXT,
                ad_sam_account_name TEXT,
                ad_user_principal_name TEXT,
                status TEXT NOT NULL DEFAULT 'Proposed',
                created_at TEXT,
                PRIMARY KEY (person_id, ad_object_guid)
            );").ConfigureAwait(false);

        // Migrate existing databases: add columns introduced after initial creation.
        // Duplicate-column errors are ignored (column already exists).
        foreach (var migration in new[]
        {
            "ALTER TABLE ad_correlation_results ADD COLUMN match_method TEXT",
            "ALTER TABLE ad_correlation_results ADD COLUMN value_status TEXT",
        })
        {
            try
            {
                await connection.ExecuteAsync(migration).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex.Message.Contains("duplicate column", StringComparison.OrdinalIgnoreCase))
            {
                // Column already exists - nothing to do
            }
        }
    }

    public async Task<List<CorrelatablePersonDto>> GetCorrelatablePersonsAsync(bool activeOnly = true, int futureDays = 30)
    {
        using var connection = _connectionFactory.CreateConnection();

        var today = DateTime.Today.ToString("yyyy-MM-dd");
        var todayPlusX = DateTime.Today.AddDays(Math.Max(0, futureDays)).ToString("yyyy-MM-dd");

        var sql = @"
            SELECT
                p.person_id AS PersonId,
                p.external_id AS ExternalId,
                p.display_name AS DisplayName,
                p.given_name AS GivenName,
                p.family_name AS FamilyName,
                p.nick_name AS NickName,
                p.family_name_partner AS FamilyNamePartner,
                p.user_name AS UserName,
                (SELECT c.email FROM contacts c WHERE c.person_id = p.person_id AND c.type = 'Business' AND c.email IS NOT NULL LIMIT 1) AS BusinessEmail
            FROM persons p";

        if (activeOnly)
        {
            sql += @"
            WHERE EXISTS (
                SELECT 1 FROM contracts ct
                WHERE ct.person_id = p.person_id
                  AND (
                      ((ct.start_date IS NULL OR ct.start_date <= @Today)
                       AND (ct.end_date IS NULL OR ct.end_date >= @Today))
                      OR (ct.start_date > @Today AND ct.start_date <= @TodayPlusX)
                  )
            )";
        }

        System.Diagnostics.Debug.WriteLine($"[AdRepo] QUERY persons (correlatable): activeOnly={activeOnly}, futureDays={futureDays}, today='{today}'");

        var persons = await connection.QueryAsync<CorrelatablePersonDto>(sql, new { Today = today, TodayPlusX = todayPlusX }).ConfigureAwait(false);

        var list = persons.ToList();
        System.Diagnostics.Debug.WriteLine($"[AdRepo] RESULT: {list.Count} persons loaded" +
            $" ({list.Count(p => !string.IsNullOrWhiteSpace(p.ExternalId))} with external_id," +
            $" {list.Count(p => !string.IsNullOrWhiteSpace(p.BusinessEmail))} with business email)");
        foreach (var sample in list.Take(3))
        {
            System.Diagnostics.Debug.WriteLine($"[AdRepo] Person row: id={sample.PersonId} | external_id='{sample.ExternalId}' | display='{sample.DisplayName}' | given='{sample.GivenName}' | family='{sample.FamilyName}' | user='{sample.UserName}' | mail='{sample.BusinessEmail}'");
        }
        return list;
    }

    public async Task ReplaceResultsAsync(IEnumerable<AdCorrelationResult> results)
    {
        var resultList = results.ToList();
        System.Diagnostics.Debug.WriteLine($"[AdRepo] ReplaceResultsAsync: {resultList.Count} rows incoming " +
            $"(matched={resultList.Count(r => r.Status == AdCorrelationStatus.Matched)}, " +
            $"notFound={resultList.Count(r => r.Status == AdCorrelationStatus.NotFound)}, " +
            $"ambiguous={resultList.Count(r => r.Status == AdCorrelationStatus.Ambiguous)})");

        using var connection = _connectionFactory.CreateConnection();
        using var transaction = connection.BeginTransaction();

        try
        {
            // Preserve manual matches for persons that did not match by attribute in this run
            var manualMatches = (await connection.QueryAsync<AdCorrelationResult>(@"
                SELECT person_id AS PersonId, external_id AS ExternalId,
                    ad_object_guid AS AdObjectGuid, ad_distinguished_name AS AdDistinguishedName,
                    ad_sam_account_name AS AdSamAccountName, ad_user_principal_name AS AdUserPrincipalName,
                    ad_display_name AS AdDisplayName, ad_mail AS AdMail, ad_enabled AS AdEnabled,
                    correlation_value AS CorrelationValue, correlation_attribute AS CorrelationAttribute,
                    status AS StatusText, match_method AS MatchMethod, value_status AS ValueStatus,
                    correlated_at AS CorrelatedAt
                FROM ad_correlation_results
                WHERE status = 'ManuallyMatched'", transaction: transaction).ConfigureAwait(false))
                .ToDictionary(r => r.PersonId);

            await connection.ExecuteAsync("DELETE FROM ad_correlation_results", transaction: transaction).ConfigureAwait(false);

            var finalRows = new List<AdCorrelationResult>();
            foreach (var row in resultList)
            {
                if (row.Status == AdCorrelationStatus.NotFound &&
                    manualMatches.TryGetValue(row.PersonId, out var manual))
                {
                    finalRows.Add(manual);
                }
                else
                {
                    finalRows.Add(row);
                }
            }

            await connection.ExecuteAsync(@"
                INSERT INTO ad_correlation_results (
                    person_id, external_id, ad_object_guid, ad_distinguished_name, ad_sam_account_name,
                    ad_user_principal_name, ad_display_name, ad_mail, ad_enabled,
                    correlation_value, correlation_attribute, status, match_method, value_status, correlated_at
                ) VALUES (
                    @PersonId, @ExternalId, @AdObjectGuid, @AdDistinguishedName, @AdSamAccountName,
                    @AdUserPrincipalName, @AdDisplayName, @AdMail, @AdEnabled,
                    @CorrelationValue, @CorrelationAttribute, @StatusText, @MatchMethod, @ValueStatus, @CorrelatedAt
                )", finalRows, transaction: transaction).ConfigureAwait(false);

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<List<AdCorrelationResult>> GetResultsAsync(string? statusFilter = null)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            SELECT
                r.person_id AS PersonId, r.external_id AS ExternalId,
                r.ad_object_guid AS AdObjectGuid, r.ad_distinguished_name AS AdDistinguishedName,
                r.ad_sam_account_name AS AdSamAccountName, r.ad_user_principal_name AS AdUserPrincipalName,
                r.ad_display_name AS AdDisplayName, r.ad_mail AS AdMail, r.ad_enabled AS AdEnabled,
                r.correlation_value AS CorrelationValue, r.correlation_attribute AS CorrelationAttribute,
                r.status AS StatusText, r.match_method AS MatchMethod, r.value_status AS ValueStatus,
                r.correlated_at AS CorrelatedAt,
                p.display_name AS PersonDisplayName
            FROM ad_correlation_results r
            LEFT JOIN persons p ON r.person_id = p.person_id";

        if (!string.IsNullOrWhiteSpace(statusFilter))
        {
            sql += " WHERE r.status = @StatusFilter";
        }

        sql += " ORDER BY p.display_name";

        System.Diagnostics.Debug.WriteLine($"[AdRepo] QUERY ad_correlation_results (filter='{statusFilter ?? "<all>"}')");

        var results = await connection.QueryAsync<AdCorrelationResult>(sql, new { StatusFilter = statusFilter }).ConfigureAwait(false);
        var resultList = results.ToList();
        System.Diagnostics.Debug.WriteLine($"[AdRepo] RESULT: {resultList.Count} rows " +
            $"(matched={resultList.Count(r => r.Status == AdCorrelationStatus.Matched)}, " +
            $"notFound={resultList.Count(r => r.Status == AdCorrelationStatus.NotFound)}, " +
            $"ambiguous={resultList.Count(r => r.Status == AdCorrelationStatus.Ambiguous)}, " +
            $"manual={resultList.Count(r => r.Status == AdCorrelationStatus.ManuallyMatched)})");
        return resultList;
    }

    public async Task ReplaceRecommendationsAsync(IEnumerable<AdMatchRecommendation> recommendations)
    {
        var list = recommendations.ToList();
        System.Diagnostics.Debug.WriteLine($"[AdRepo] ReplaceRecommendationsAsync: {list.Count} incoming for {list.Select(r => r.PersonId).Distinct().Count()} persons");

        using var connection = _connectionFactory.CreateConnection();
        using var transaction = connection.BeginTransaction();

        try
        {
            // Only replace Proposed rows; keep Accepted/Rejected history
            await connection.ExecuteAsync("DELETE FROM ad_match_recommendations WHERE status = 'Proposed'", transaction: transaction).ConfigureAwait(false);

            if (list.Count > 0)
            {
                // Deduplicate (person, ad account) pairs - plain INSERT for SQLite/PostgreSQL compatibility
                var deduped = list
                    .GroupBy(r => (r.PersonId, r.AdObjectGuid))
                    .Select(g => g.OrderByDescending(r => r.ScorePercent).First())
                    .ToList();

                await connection.ExecuteAsync(@"
                    INSERT INTO ad_match_recommendations (
                        person_id, ad_object_guid, score_percent, field_scores_json,
                        ad_display_name, ad_sam_account_name, ad_user_principal_name, status, created_at
                    ) VALUES (
                        @PersonId, @AdObjectGuid, @ScorePercent, @FieldScoresJson,
                        @AdDisplayName, @AdSamAccountName, @AdUserPrincipalName, @StatusText, @CreatedAt
                    )", deduped, transaction: transaction).ConfigureAwait(false);
            }

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<List<AdMatchRecommendation>> GetRecommendationsAsync(string? statusFilter = null)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            SELECT
                rec.person_id AS PersonId, rec.ad_object_guid AS AdObjectGuid,
                rec.score_percent AS ScorePercent, rec.field_scores_json AS FieldScoresJson,
                rec.ad_display_name AS AdDisplayName, rec.ad_sam_account_name AS AdSamAccountName,
                rec.ad_user_principal_name AS AdUserPrincipalName,
                rec.status AS StatusText, rec.created_at AS CreatedAt,
                p.display_name AS PersonDisplayName, p.external_id AS PersonExternalId
            FROM ad_match_recommendations rec
            LEFT JOIN persons p ON rec.person_id = p.person_id";

        if (!string.IsNullOrWhiteSpace(statusFilter))
        {
            sql += " WHERE rec.status = @StatusFilter";
        }

        sql += " ORDER BY rec.score_percent DESC, p.display_name";

        var results = await connection.QueryAsync<AdMatchRecommendation>(sql, new { StatusFilter = statusFilter }).ConfigureAwait(false);
        return results.ToList();
    }

    public async Task UpdateRecommendationStatusAsync(string personId, string adObjectGuid, AdRecommendationStatus status)
    {
        using var connection = _connectionFactory.CreateConnection();

        await connection.ExecuteAsync(
            "UPDATE ad_match_recommendations SET status = @Status WHERE person_id = @PersonId AND ad_object_guid = @AdObjectGuid",
            new { Status = status.ToString(), PersonId = personId, AdObjectGuid = adObjectGuid }).ConfigureAwait(false);
    }

    public async Task SetManualMatchAsync(string personId, AdUserDto adUser, string correlationAttribute, string? personValue = null)
    {
        using var connection = _connectionFactory.CreateConnection();

        var now = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
        await connection.ExecuteAsync(@"
            INSERT INTO ad_correlation_results (
                person_id, external_id, ad_object_guid, ad_distinguished_name, ad_sam_account_name,
                ad_user_principal_name, ad_display_name, ad_mail, ad_enabled,
                correlation_value, correlation_attribute, status, match_method, value_status, correlated_at
            ) VALUES (
                @PersonId,
                (SELECT external_id FROM persons WHERE person_id = @PersonId),
                @AdObjectGuid, @AdDistinguishedName, @AdSamAccountName,
                @AdUserPrincipalName, @AdDisplayName, @AdMail, @AdEnabled,
                @CorrelationValue, @CorrelationAttribute, 'ManuallyMatched', 'Manual', @ValueStatus, @CorrelatedAt
            )
            ON CONFLICT(person_id) DO UPDATE SET
                ad_object_guid = @AdObjectGuid,
                ad_distinguished_name = @AdDistinguishedName,
                ad_sam_account_name = @AdSamAccountName,
                ad_user_principal_name = @AdUserPrincipalName,
                ad_display_name = @AdDisplayName,
                ad_mail = @AdMail,
                ad_enabled = @AdEnabled,
                correlation_value = @CorrelationValue,
                correlation_attribute = @CorrelationAttribute,
                status = 'ManuallyMatched',
                match_method = 'Manual',
                value_status = @ValueStatus,
                correlated_at = @CorrelatedAt",
            new
            {
                PersonId = personId,
                AdObjectGuid = adUser.ObjectGuid,
                AdDistinguishedName = adUser.DistinguishedName,
                AdSamAccountName = adUser.SamAccountName,
                AdUserPrincipalName = adUser.UserPrincipalName,
                AdDisplayName = adUser.DisplayName,
                AdMail = adUser.Mail,
                AdEnabled = adUser.Enabled ? 1 : 0,
                CorrelationValue = adUser.CorrelationValue,
                CorrelationAttribute = correlationAttribute,
                ValueStatus = adUser.CorrelationValue == null ? "Unreadable"
                    : string.IsNullOrWhiteSpace(adUser.CorrelationValue) ? "Missing"
                    : string.Equals(adUser.CorrelationValue.Trim(), personValue?.Trim(), StringComparison.OrdinalIgnoreCase) ? "Correct"
                    : "Different",
                CorrelatedAt = now
            }).ConfigureAwait(false);
    }

    public async Task ClearManualMatchAsync(string personId)
    {
        using var connection = _connectionFactory.CreateConnection();

        await connection.ExecuteAsync(
            "UPDATE ad_correlation_results SET status = 'NotFound', ad_object_guid = NULL, ad_distinguished_name = NULL, ad_sam_account_name = NULL, ad_user_principal_name = NULL, ad_display_name = NULL, ad_mail = NULL, ad_enabled = NULL WHERE person_id = @PersonId AND status = 'ManuallyMatched'",
            new { PersonId = personId }).ConfigureAwait(false);
    }

    public async Task<int> ClearResultsAsync()
    {
        using var connection = _connectionFactory.CreateConnection();

        var rows = await connection.ExecuteAsync("DELETE FROM ad_correlation_results").ConfigureAwait(false);
        System.Diagnostics.Debug.WriteLine($"[AdRepo] ClearResultsAsync: {rows} rows deleted");
        return rows;
    }

    public async Task<int> ClearRecommendationsAsync()
    {
        using var connection = _connectionFactory.CreateConnection();

        var rows = await connection.ExecuteAsync("DELETE FROM ad_match_recommendations").ConfigureAwait(false);
        System.Diagnostics.Debug.WriteLine($"[AdRepo] ClearRecommendationsAsync: {rows} rows deleted");
        return rows;
    }
}
