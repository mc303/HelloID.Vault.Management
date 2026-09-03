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
            CREATE TABLE IF NOT EXISTS ad_correlation_config (
                id INTEGER PRIMARY KEY,
                ldap_host TEXT NOT NULL DEFAULT '',
                ldap_port INTEGER NOT NULL DEFAULT 636,
                use_ldaps INTEGER NOT NULL DEFAULT 1,
                search_base TEXT NOT NULL DEFAULT '',
                auth_type TEXT NOT NULL DEFAULT 'Negotiate',
                username TEXT,
                encrypted_password TEXT,
                correlation_attribute TEXT NOT NULL DEFAULT 'employeeID',
                vault_field TEXT NOT NULL DEFAULT 'external_id',
                match_fields_json TEXT,
                min_score INTEGER NOT NULL DEFAULT 50,
                max_candidates INTEGER NOT NULL DEFAULT 3,
                updated_at TEXT
            );
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
    }

    public async Task<AdCorrelationConfig> GetConfigAsync()
    {
        using var connection = _connectionFactory.CreateConnection();

        var config = await connection.QueryFirstOrDefaultAsync<AdCorrelationConfig>(@"
            SELECT
                ldap_host AS LdapHost,
                ldap_port AS LdapPort,
                use_ldaps AS UseLdaps,
                search_base AS SearchBase,
                auth_type AS AuthType,
                username AS Username,
                encrypted_password AS EncryptedPassword,
                correlation_attribute AS CorrelationAttribute,
                vault_field AS VaultField,
                match_fields_json AS MatchFieldsJson,
                min_score AS MinScore,
                max_candidates AS MaxCandidates,
                updated_at AS UpdatedAt
            FROM ad_correlation_config
            WHERE id = 1").ConfigureAwait(false);

        return config ?? new AdCorrelationConfig();
    }

    public async Task SaveConfigAsync(AdCorrelationConfig config)
    {
        using var connection = _connectionFactory.CreateConnection();

        var now = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
        await connection.ExecuteAsync(@"
            DELETE FROM ad_correlation_config WHERE id = 1;
            INSERT INTO ad_correlation_config (
                id, ldap_host, ldap_port, use_ldaps, search_base, auth_type, username, encrypted_password,
                correlation_attribute, vault_field, match_fields_json, min_score, max_candidates, updated_at
            ) VALUES (
                1, @LdapHost, @LdapPort, @UseLdaps, @SearchBase, @AuthType, @Username, @EncryptedPassword,
                @CorrelationAttribute, @VaultField, @MatchFieldsJson, @MinScore, @MaxCandidates, @UpdatedAt
            )",
            new
            {
                config.LdapHost,
                config.LdapPort,
                UseLdaps = config.UseLdaps ? 1 : 0,
                config.SearchBase,
                config.AuthType,
                config.Username,
                config.EncryptedPassword,
                config.CorrelationAttribute,
                config.VaultField,
                config.MatchFieldsJson,
                config.MinScore,
                config.MaxCandidates,
                UpdatedAt = now
            }).ConfigureAwait(false);

        config.UpdatedAt = now;
    }

    public async Task<List<CorrelatablePersonDto>> GetCorrelatablePersonsAsync()
    {
        using var connection = _connectionFactory.CreateConnection();

        var persons = await connection.QueryAsync<CorrelatablePersonDto>(@"
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
            FROM persons p").ConfigureAwait(false);

        return persons.ToList();
    }

    public async Task ReplaceResultsAsync(IEnumerable<AdCorrelationResult> results)
    {
        var resultList = results.ToList();

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
                    status AS StatusText, correlated_at AS CorrelatedAt
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
                    correlation_value, correlation_attribute, status, correlated_at
                ) VALUES (
                    @PersonId, @ExternalId, @AdObjectGuid, @AdDistinguishedName, @AdSamAccountName,
                    @AdUserPrincipalName, @AdDisplayName, @AdMail, @AdEnabled,
                    @CorrelationValue, @CorrelationAttribute, @StatusText, @CorrelatedAt
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
                r.status AS StatusText, r.correlated_at AS CorrelatedAt,
                p.display_name AS PersonDisplayName
            FROM ad_correlation_results r
            LEFT JOIN persons p ON r.person_id = p.person_id";

        if (!string.IsNullOrWhiteSpace(statusFilter))
        {
            sql += " WHERE r.status = @StatusFilter";
        }

        sql += " ORDER BY p.display_name";

        var results = await connection.QueryAsync<AdCorrelationResult>(sql, new { StatusFilter = statusFilter }).ConfigureAwait(false);
        return results.ToList();
    }

    public async Task ReplaceRecommendationsAsync(IEnumerable<AdMatchRecommendation> recommendations)
    {
        var list = recommendations.ToList();

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

    public async Task SetManualMatchAsync(string personId, AdUserDto adUser, string correlationAttribute)
    {
        using var connection = _connectionFactory.CreateConnection();

        var now = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
        await connection.ExecuteAsync(@"
            INSERT INTO ad_correlation_results (
                person_id, external_id, ad_object_guid, ad_distinguished_name, ad_sam_account_name,
                ad_user_principal_name, ad_display_name, ad_mail, ad_enabled,
                correlation_value, correlation_attribute, status, correlated_at
            ) VALUES (
                @PersonId,
                (SELECT external_id FROM persons WHERE person_id = @PersonId),
                @AdObjectGuid, @AdDistinguishedName, @AdSamAccountName,
                @AdUserPrincipalName, @AdDisplayName, @AdMail, @AdEnabled,
                @CorrelationValue, @CorrelationAttribute, 'ManuallyMatched', @CorrelatedAt
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
                correlated_at = @CorrelatedAt",
            new
            {
                PersonId = personId,
                adUser.ObjectGuid,
                adUser.DistinguishedName,
                adUser.SamAccountName,
                adUser.UserPrincipalName,
                adUser.DisplayName,
                adUser.Mail,
                AdEnabled = adUser.Enabled ? 1 : 0,
                CorrelationValue = adUser.CorrelationValue,
                CorrelationAttribute = correlationAttribute,
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
}
