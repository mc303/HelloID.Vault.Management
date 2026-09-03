using System.Diagnostics;
using HelloID.Vault.Core.Models.Ad;
using HelloID.Vault.Data.Connection;
using HelloID.Vault.Data.Repositories.Interfaces;

namespace HelloID.Vault.Data.Repositories.Turso;

/// <summary>
/// Turso implementation of the AD correlation repository.
/// </summary>
public class TursoAdCorrelationRepository : IAdCorrelationRepository
{
    private readonly ITursoClient _client;

    public TursoAdCorrelationRepository(ITursoClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    public async Task EnsureTablesAsync()
    {
        await _client.ExecuteScriptAsync(@"
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
            );");
    }

    public async Task<AdCorrelationConfig> GetConfigAsync()
    {
        var result = await _client.QueryAsync<AdCorrelationConfig>(@"
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
            WHERE id = 1");
        return result.Rows.FirstOrDefault() ?? new AdCorrelationConfig();
    }

    public async Task SaveConfigAsync(AdCorrelationConfig config)
    {
        var now = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
        var statements = new List<TursoStatement>
        {
            new() { Sql = "DELETE FROM ad_correlation_config WHERE id = 1", WantRows = false },
            new()
            {
                Sql = @"INSERT INTO ad_correlation_config (
                    id, ldap_host, ldap_port, use_ldaps, search_base, auth_type, username, encrypted_password,
                    correlation_attribute, vault_field, match_fields_json, min_score, max_candidates, updated_at
                ) VALUES (1, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)",
                Args =
                [
                    TursoValue.Text(config.LdapHost ?? string.Empty),
                    TursoValue.Integer(config.LdapPort),
                    TursoValue.Integer(config.UseLdaps ? 1 : 0),
                    TursoValue.Text(config.SearchBase ?? string.Empty),
                    TursoValue.Text(config.AuthType ?? "Negotiate"),
                    config.Username != null ? TursoValue.Text(config.Username) : TursoValue.Null(),
                    config.EncryptedPassword != null ? TursoValue.Text(config.EncryptedPassword) : TursoValue.Null(),
                    TursoValue.Text(config.CorrelationAttribute ?? "employeeID"),
                    TursoValue.Text(config.VaultField ?? "external_id"),
                    config.MatchFieldsJson != null ? TursoValue.Text(config.MatchFieldsJson) : TursoValue.Null(),
                    TursoValue.Integer(config.MinScore),
                    TursoValue.Integer(config.MaxCandidates),
                    TursoValue.Text(now)
                ],
                WantRows = false
            }
        };
        await _client.ExecuteTransactionAsync(statements);
        config.UpdatedAt = now;
    }

    public async Task<List<CorrelatablePersonDto>> GetCorrelatablePersonsAsync()
    {
        var result = await _client.QueryAsync<CorrelatablePersonDto>(@"
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
            FROM persons p");
        return result.Rows.ToList();
    }

    public async Task ReplaceResultsAsync(IEnumerable<AdCorrelationResult> results)
    {
        var resultList = results.ToList();

        // Preserve manual matches for persons that did not match by attribute in this run
        var manual = await _client.QueryAsync<AdCorrelationResult>(@"
            SELECT person_id AS PersonId, external_id AS ExternalId,
                ad_object_guid AS AdObjectGuid, ad_distinguished_name AS AdDistinguishedName,
                ad_sam_account_name AS AdSamAccountName, ad_user_principal_name AS AdUserPrincipalName,
                ad_display_name AS AdDisplayName, ad_mail AS AdMail, ad_enabled AS AdEnabled,
                correlation_value AS CorrelationValue, correlation_attribute AS CorrelationAttribute,
                status AS StatusText, correlated_at AS CorrelatedAt
            FROM ad_correlation_results
            WHERE status = 'ManuallyMatched'");
        var manualMatches = manual.Rows.ToDictionary(r => r.PersonId);

        var finalRows = new List<AdCorrelationResult>();
        foreach (var row in resultList)
        {
            if (row.Status == AdCorrelationStatus.NotFound && manualMatches.TryGetValue(row.PersonId, out var m))
            {
                finalRows.Add(m);
            }
            else
            {
                finalRows.Add(row);
            }
        }

        var statements = new List<TursoStatement> { new() { Sql = "DELETE FROM ad_correlation_results", WantRows = false } };
        statements.AddRange(finalRows.Select(r => new TursoStatement
        {
            Sql = @"INSERT INTO ad_correlation_results (
                person_id, external_id, ad_object_guid, ad_distinguished_name, ad_sam_account_name,
                ad_user_principal_name, ad_display_name, ad_mail, ad_enabled,
                correlation_value, correlation_attribute, status, correlated_at
            ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)",
            Args =
            [
                TursoValue.Text(r.PersonId),
                r.ExternalId != null ? TursoValue.Text(r.ExternalId) : TursoValue.Null(),
                r.AdObjectGuid != null ? TursoValue.Text(r.AdObjectGuid) : TursoValue.Null(),
                r.AdDistinguishedName != null ? TursoValue.Text(r.AdDistinguishedName) : TursoValue.Null(),
                r.AdSamAccountName != null ? TursoValue.Text(r.AdSamAccountName) : TursoValue.Null(),
                r.AdUserPrincipalName != null ? TursoValue.Text(r.AdUserPrincipalName) : TursoValue.Null(),
                r.AdDisplayName != null ? TursoValue.Text(r.AdDisplayName) : TursoValue.Null(),
                r.AdMail != null ? TursoValue.Text(r.AdMail) : TursoValue.Null(),
                r.AdEnabled.HasValue ? TursoValue.Integer(r.AdEnabled.Value ? 1 : 0) : TursoValue.Null(),
                r.CorrelationValue != null ? TursoValue.Text(r.CorrelationValue) : TursoValue.Null(),
                r.CorrelationAttribute != null ? TursoValue.Text(r.CorrelationAttribute) : TursoValue.Null(),
                TursoValue.Text(r.StatusText),
                r.CorrelatedAt != null ? TursoValue.Text(r.CorrelatedAt) : TursoValue.Null()
            ],
            WantRows = false
        }));

        await _client.ExecuteTransactionAsync(statements);
    }

    public async Task<List<AdCorrelationResult>> GetResultsAsync(string? statusFilter = null)
    {
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
            sql += " WHERE r.status = '" + statusFilter.Replace("'", "''") + "'";
        }

        sql += " ORDER BY p.display_name";

        var result = await _client.QueryAsync<AdCorrelationResult>(sql);
        return result.Rows.ToList();
    }

    public async Task ReplaceRecommendationsAsync(IEnumerable<AdMatchRecommendation> recommendations)
    {
        var deduped = recommendations
            .GroupBy(r => (r.PersonId, r.AdObjectGuid))
            .Select(g => g.OrderByDescending(r => r.ScorePercent).First())
            .ToList();

        var statements = new List<TursoStatement>
        {
            new() { Sql = "DELETE FROM ad_match_recommendations WHERE status = 'Proposed'", WantRows = false }
        };

        var now = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
        statements.AddRange(deduped.Select(r => new TursoStatement
        {
            Sql = @"INSERT OR IGNORE INTO ad_match_recommendations (
                person_id, ad_object_guid, score_percent, field_scores_json,
                ad_display_name, ad_sam_account_name, ad_user_principal_name, status, created_at
            ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)",
            Args =
            [
                TursoValue.Text(r.PersonId),
                TursoValue.Text(r.AdObjectGuid),
                TursoValue.Float(r.ScorePercent),
                r.FieldScoresJson != null ? TursoValue.Text(r.FieldScoresJson) : TursoValue.Null(),
                r.AdDisplayName != null ? TursoValue.Text(r.AdDisplayName) : TursoValue.Null(),
                r.AdSamAccountName != null ? TursoValue.Text(r.AdSamAccountName) : TursoValue.Null(),
                r.AdUserPrincipalName != null ? TursoValue.Text(r.AdUserPrincipalName) : TursoValue.Null(),
                TursoValue.Text(r.StatusText),
                TursoValue.Text(r.CreatedAt ?? now)
            ],
            WantRows = false
        }));

        await _client.ExecuteTransactionAsync(statements);
    }

    public async Task<List<AdMatchRecommendation>> GetRecommendationsAsync(string? statusFilter = null)
    {
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
            sql += " WHERE rec.status = '" + statusFilter.Replace("'", "''") + "'";
        }

        sql += " ORDER BY rec.score_percent DESC, p.display_name";

        var result = await _client.QueryAsync<AdMatchRecommendation>(sql);
        return result.Rows.ToList();
    }

    public async Task UpdateRecommendationStatusAsync(string personId, string adObjectGuid, AdRecommendationStatus status)
    {
        await _client.ExecuteAsync(
            "UPDATE ad_match_recommendations SET status = @Status WHERE person_id = @PersonId AND ad_object_guid = @AdObjectGuid",
            new { Status = status.ToString(), PersonId = personId, AdObjectGuid = adObjectGuid });
    }

    public async Task SetManualMatchAsync(string personId, AdUserDto adUser, string correlationAttribute)
    {
        var now = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
        var statements = new List<TursoStatement>
        {
            new()
            {
                Sql = @"INSERT INTO ad_correlation_results (
                    person_id, external_id, ad_object_guid, ad_distinguished_name, ad_sam_account_name,
                    ad_user_principal_name, ad_display_name, ad_mail, ad_enabled,
                    correlation_value, correlation_attribute, status, correlated_at
                ) VALUES (?, (SELECT external_id FROM persons WHERE person_id = ?), ?, ?, ?, ?, ?, ?, ?, ?, ?, 'ManuallyMatched', ?)
                ON CONFLICT(person_id) DO UPDATE SET
                    ad_object_guid = excluded.ad_object_guid,
                    ad_distinguished_name = excluded.ad_distinguished_name,
                    ad_sam_account_name = excluded.ad_sam_account_name,
                    ad_user_principal_name = excluded.ad_user_principal_name,
                    ad_display_name = excluded.ad_display_name,
                    ad_mail = excluded.ad_mail,
                    ad_enabled = excluded.ad_enabled,
                    correlation_value = excluded.correlation_value,
                    correlation_attribute = excluded.correlation_attribute,
                    status = 'ManuallyMatched',
                    correlated_at = excluded.correlated_at",
                Args =
                [
                    TursoValue.Text(personId),
                    TursoValue.Text(personId),
                    adUser.ObjectGuid != null ? TursoValue.Text(adUser.ObjectGuid) : TursoValue.Null(),
                    adUser.DistinguishedName != null ? TursoValue.Text(adUser.DistinguishedName) : TursoValue.Null(),
                    adUser.SamAccountName != null ? TursoValue.Text(adUser.SamAccountName) : TursoValue.Null(),
                    adUser.UserPrincipalName != null ? TursoValue.Text(adUser.UserPrincipalName) : TursoValue.Null(),
                    adUser.DisplayName != null ? TursoValue.Text(adUser.DisplayName) : TursoValue.Null(),
                    adUser.Mail != null ? TursoValue.Text(adUser.Mail) : TursoValue.Null(),
                    TursoValue.Integer(adUser.Enabled ? 1 : 0),
                    adUser.CorrelationValue != null ? TursoValue.Text(adUser.CorrelationValue) : TursoValue.Null(),
                    TursoValue.Text(correlationAttribute),
                    TursoValue.Text(now)
                ],
                WantRows = false
            }
        };
        await _client.ExecuteTransactionAsync(statements);
        Debug.WriteLine($"[TursoAdCorrelationRepository] SetManualMatchAsync for person {personId}");
    }

    public async Task ClearManualMatchAsync(string personId)
    {
        await _client.ExecuteAsync(
            "UPDATE ad_correlation_results SET status = 'NotFound', ad_object_guid = NULL, ad_distinguished_name = NULL, ad_sam_account_name = NULL, ad_user_principal_name = NULL, ad_display_name = NULL, ad_mail = NULL, ad_enabled = NULL WHERE person_id = @PersonId AND status = 'ManuallyMatched'",
            new { PersonId = personId });
    }
}
