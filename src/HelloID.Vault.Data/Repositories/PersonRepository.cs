using Dapper;
using HelloID.Vault.Core.Models.Entities;
using HelloID.Vault.Core.Models.DTOs;
using HelloID.Vault.Core.Models.Filters;
using HelloID.Vault.Data.Connection;
using HelloID.Vault.Data.Repositories.Interfaces;

namespace HelloID.Vault.Data.Repositories;

/// <summary>
/// Repository implementation for Person entity using Dapper.
/// </summary>
public class PersonRepository : IPersonRepository
{
    private readonly IDatabaseConnectionFactory _connectionFactory;

    public PersonRepository(IDatabaseConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<IEnumerable<PersonListDto>> GetPagedAsync(PersonFilter filter, int page, int pageSize)
    {
        System.Diagnostics.Debug.WriteLine($"[PersonRepository] GetPagedAsync called with SearchTerm='{filter.SearchTerm}', PersonStatus='{filter.PersonStatus}', page={page}, pageSize={pageSize}");

        using var connection = _connectionFactory.CreateConnection();

        var whereClauses = new List<string>();
        var parameters = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            whereClauses.Add("(display_name LIKE @SearchTerm OR external_id LIKE @SearchTerm OR person_id LIKE @SearchTerm)");
            parameters.Add("SearchTerm", $"%{filter.SearchTerm}%");
            System.Diagnostics.Debug.WriteLine($"[PersonRepository] Added SearchTerm filter: '%{filter.SearchTerm}%'");
        }

        if (filter.Blocked.HasValue)
        {
            whereClauses.Add("blocked = @Blocked");
            parameters.Add("Blocked", filter.Blocked.Value ? 1 : 0);
        }

        if (filter.Excluded.HasValue)
        {
            whereClauses.Add("excluded = @Excluded");
            parameters.Add("Excluded", filter.Excluded.Value ? 1 : 0);
        }

        if (!string.IsNullOrWhiteSpace(filter.PersonStatus))
        {
            whereClauses.Add("person_status = @PersonStatus");
            parameters.Add("PersonStatus", filter.PersonStatus);
        }

        var whereClause = whereClauses.Count > 0 ? "WHERE " + string.Join(" AND ", whereClauses) : "";

        parameters.Add("Limit", pageSize);
        parameters.Add("Offset", (page - 1) * pageSize);

        var sql = $@"
            SELECT
                person_id AS PersonId,
                display_name AS DisplayName,
                external_id AS ExternalId,
                blocked AS Blocked,
                excluded AS Excluded,
                person_status AS PersonStatus,
                contract_count AS ContractCount,
                primary_manager_person_id AS PrimaryManagerPersonId,
                primary_manager_name AS PrimaryManagerName
            FROM person_list_view
            {whereClause}
            ORDER BY display_name
            LIMIT @Limit OFFSET @Offset";

        return await connection.QueryAsync<PersonListDto>(sql, parameters).ConfigureAwait(false);
    }

    public async Task<int> GetCountAsync(PersonFilter filter)
    {
        using var connection = _connectionFactory.CreateConnection();

        var whereClauses = new List<string>();
        var parameters = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            whereClauses.Add("(display_name LIKE @SearchTerm OR external_id LIKE @SearchTerm OR person_id LIKE @SearchTerm)");
            parameters.Add("SearchTerm", $"%{filter.SearchTerm}%");
        }

        if (filter.Blocked.HasValue)
        {
            whereClauses.Add("blocked = @Blocked");
            parameters.Add("Blocked", filter.Blocked.Value ? 1 : 0);
        }

        if (filter.Excluded.HasValue)
        {
            whereClauses.Add("excluded = @Excluded");
            parameters.Add("Excluded", filter.Excluded.Value ? 1 : 0);
        }

        if (!string.IsNullOrWhiteSpace(filter.PersonStatus))
        {
            whereClauses.Add("person_status = @PersonStatus");
            parameters.Add("PersonStatus", filter.PersonStatus);
        }

        var whereClause = whereClauses.Count > 0 ? "WHERE " + string.Join(" AND ", whereClauses) : "";

        var sql = $"SELECT COUNT(*) FROM person_list_view {whereClause}";

        return await connection.ExecuteScalarAsync<int>(sql, parameters).ConfigureAwait(false);
    }

    public async Task<Person?> GetByIdAsync(string personId)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            SELECT
                person_id AS PersonId,
                display_name AS DisplayName,
                external_id AS ExternalId,
                user_name AS UserName,
                gender AS Gender,
                honorific_prefix AS HonorificPrefix,
                honorific_suffix AS HonorificSuffix,
                birth_date AS BirthDate,
                birth_locality AS BirthLocality,
                marital_status AS MaritalStatus,
                initials AS Initials,
                given_name AS GivenName,
                family_name AS FamilyName,
                family_name_prefix AS FamilyNamePrefix,
                convention AS Convention,
                nick_name AS NickName,
                family_name_partner AS FamilyNamePartner,
                family_name_partner_prefix AS FamilyNamePartnerPrefix,
                blocked AS Blocked,
                status_reason AS StatusReason,
                excluded AS Excluded,
                hr_excluded AS HrExcluded,
                manual_excluded AS ManualExcluded,
                source AS Source,
                primary_manager_person_id AS PrimaryManagerPersonId,
                primary_manager_updated_at AS PrimaryManagerUpdatedAt
            FROM persons
            WHERE person_id = @PersonId";

        return await connection.QuerySingleOrDefaultAsync<Person>(sql, new { PersonId = personId }).ConfigureAwait(false);
    }

    public async Task<PersonDetailDto?> GetPersonDetailAsync(string personId)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            SELECT
                p.person_id AS PersonId,
                p.display_name AS DisplayName,
                p.external_id AS ExternalId,
                p.user_name AS UserName,
                p.gender AS Gender,
                p.honorific_prefix AS HonorificPrefix,
                p.honorific_suffix AS HonorificSuffix,
                p.birth_date AS BirthDate,
                p.birth_locality AS BirthLocality,
                p.marital_status AS MaritalStatus,
                p.initials AS Initials,
                p.given_name AS GivenName,
                p.family_name AS FamilyName,
                p.family_name_prefix AS FamilyNamePrefix,
                p.convention AS Convention,
                p.nick_name AS NickName,
                p.family_name_partner AS FamilyNamePartner,
                p.family_name_partner_prefix AS FamilyNamePartnerPrefix,
                p.blocked AS Blocked,
                p.status_reason AS StatusReason,
                p.excluded AS Excluded,
                p.hr_excluded AS HrExcluded,
                p.manual_excluded AS ManualExcluded,
                p.source AS Source,
                ss.display_name AS SourceDisplayName,
                v.primary_contract_id AS PrimaryContractId,
                v.primary_contract_external_id AS PrimaryContractExternalId,
                v.primary_contract_start_date AS PrimaryContractStartDate,
                v.primary_contract_end_date AS PrimaryContractEndDate,
                v.primary_contract_type_code AS PrimaryContractTypeCode,
                v.primary_contract_type_description AS PrimaryContractTypeDescription,
                v.primary_contract_fte AS PrimaryContractFte,
                v.primary_contract_hours_per_week AS PrimaryContractHoursPerWeek,
                v.primary_contract_percentage AS PrimaryContractPercentage,
                v.primary_contract_sequence AS PrimaryContractSequence,
                v.primary_contract_manager_external_id AS PrimaryContractManagerId,
                v.primary_contract_manager_name AS PrimaryContractManagerName,
                v.primary_manager_person_id AS PrimaryManagerPersonId,
                v.primary_manager_name AS PrimaryManagerName,
                v.primary_manager_external_id AS PrimaryManagerExternalId,
                v.primary_manager_updated_at AS PrimaryManagerUpdatedAt,
                v.primary_contract_location_external_id AS PrimaryContractLocationId,
                v.primary_contract_location_name AS PrimaryContractLocationName,
                v.primary_contract_cost_center_external_id AS PrimaryContractCostCenterId,
                v.primary_contract_cost_center_name AS PrimaryContractCostCenterName,
                v.primary_contract_cost_bearer_external_id AS PrimaryContractCostBearerId,
                v.primary_contract_cost_bearer_name AS PrimaryContractCostBearerName,
                v.primary_contract_employer_external_id AS PrimaryContractEmployerId,
                v.primary_contract_employer_name AS PrimaryContractEmployerName,
                v.primary_contract_team_external_id AS PrimaryContractTeamId,
                v.primary_contract_team_name AS PrimaryContractTeamName,
                v.primary_contract_department_external_id AS PrimaryContractDepartmentId,
                v.primary_contract_department_name AS PrimaryContractDepartmentName,
                v.primary_contract_department_code AS PrimaryContractDepartmentCode,
                v.primary_contract_division_external_id AS PrimaryContractDivisionId,
                v.primary_contract_division_name AS PrimaryContractDivisionName,
                v.primary_contract_title_external_id AS PrimaryContractTitleId,
                v.primary_contract_title_name AS PrimaryContractTitleName,
                v.primary_contract_organization_external_id AS PrimaryContractOrganizationId,
                v.primary_contract_organization_name AS PrimaryContractOrganizationName,
                v.primary_contact_id AS PrimaryContactId,
                v.primary_contact_type AS PrimaryContactType,
                v.primary_contact_email AS PrimaryContactEmail,
                v.primary_contact_phone_mobile AS PrimaryContactPhoneMobile,
                v.primary_contact_phone_fixed AS PrimaryContactPhoneFixed,
                v.primary_contact_address_street AS PrimaryContactAddressStreet,
                v.primary_contact_address_street_ext AS PrimaryContactAddressStreetExt,
                v.primary_contact_address_house_number AS PrimaryContactAddressHouseNumber,
                v.primary_contact_address_house_number_ext AS PrimaryContactAddressHouseNumberExt,
                v.primary_contact_address_postal AS PrimaryContactAddressPostal,
                v.primary_contact_address_locality AS PrimaryContactAddressLocality,
                v.primary_contact_address_country AS PrimaryContactAddressCountry,
                v.person_status AS PersonStatus,
                v.primary_contract_date_range AS PrimaryContractDateRange
            FROM persons p
            LEFT JOIN source_system ss ON p.source = ss.system_id
            LEFT JOIN person_details_view v ON p.person_id = v.person_id
            WHERE p.person_id = @PersonId";

        return await connection.QuerySingleOrDefaultAsync<PersonDetailDto>(sql, new { PersonId = personId }).ConfigureAwait(false);
    }

    public async Task<Person?> GetByExternalIdAsync(string externalId)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            SELECT
                person_id AS PersonId,
                display_name AS DisplayName,
                external_id AS ExternalId,
                user_name AS UserName,
                gender AS Gender,
                honorific_prefix AS HonorificPrefix,
                honorific_suffix AS HonorificSuffix,
                birth_date AS BirthDate,
                birth_locality AS BirthLocality,
                marital_status AS MaritalStatus,
                initials AS Initials,
                given_name AS GivenName,
                family_name AS FamilyName,
                family_name_prefix AS FamilyNamePrefix,
                convention AS Convention,
                nick_name AS NickName,
                family_name_partner AS FamilyNamePartner,
                family_name_partner_prefix AS FamilyNamePartnerPrefix,
                blocked AS Blocked,
                status_reason AS StatusReason,
                excluded AS Excluded,
                hr_excluded AS HrExcluded,
                manual_excluded AS ManualExcluded,
                primary_manager_person_id AS PrimaryManagerPersonId,
                primary_manager_source AS PrimaryManagerSource,
                primary_manager_updated_at AS PrimaryManagerUpdatedAt
            FROM persons
            WHERE external_id = @ExternalId";

        return await connection.QuerySingleOrDefaultAsync<Person>(sql, new { ExternalId = externalId }).ConfigureAwait(false);
    }

    public async Task<int> InsertAsync(Person person)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            INSERT INTO persons (
                person_id, display_name, external_id, user_name, gender,
                honorific_prefix, honorific_suffix, birth_date, birth_locality, marital_status,
                initials, given_name, family_name, family_name_prefix, convention,
                nick_name, family_name_partner, family_name_partner_prefix,
                blocked, status_reason, excluded, hr_excluded, manual_excluded, source,
                primary_manager_person_id, primary_manager_source, primary_manager_updated_at
            ) VALUES (
                @PersonId, @DisplayName, @ExternalId, @UserName, @Gender,
                @HonorificPrefix, @HonorificSuffix, @BirthDate, @BirthLocality, @MaritalStatus,
                @Initials, @GivenName, @FamilyName, @FamilyNamePrefix, @Convention,
                @NickName, @FamilyNamePartner, @FamilyNamePartnerPrefix,
                @Blocked, @StatusReason, @Excluded, @HrExcluded, @ManualExcluded, @Source,
                @PrimaryManagerPersonId, @PrimaryManagerSource, @PrimaryManagerUpdatedAt
            )";

        return await connection.ExecuteAsync(sql, person).ConfigureAwait(false);
    }

    public async Task<int> InsertAsync(Person person, System.Data.IDbConnection connection, System.Data.IDbTransaction transaction)
    {
        var sql = @"
            INSERT INTO persons (
                person_id, display_name, external_id, user_name, gender,
                honorific_prefix, honorific_suffix, birth_date, birth_locality, marital_status,
                initials, given_name, family_name, family_name_prefix, convention,
                nick_name, family_name_partner, family_name_partner_prefix,
                blocked, status_reason, excluded, hr_excluded, manual_excluded, source,
                primary_manager_person_id, primary_manager_source, primary_manager_updated_at
            ) VALUES (
                @PersonId, @DisplayName, @ExternalId, @UserName, @Gender,
                @HonorificPrefix, @HonorificSuffix, @BirthDate, @BirthLocality, @MaritalStatus,
                @Initials, @GivenName, @FamilyName, @FamilyNamePrefix, @Convention,
                @NickName, @FamilyNamePartner, @FamilyNamePartnerPrefix,
                @Blocked, @StatusReason, @Excluded, @HrExcluded, @ManualExcluded, @Source,
                @PrimaryManagerPersonId, @PrimaryManagerSource, @PrimaryManagerUpdatedAt
            )";

        return await connection.ExecuteAsync(sql, person, transaction).ConfigureAwait(false);
    }

    public async Task<int> UpdateAsync(Person person)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            UPDATE persons SET
                display_name = @DisplayName,
                user_name = @UserName,
                gender = @Gender,
                honorific_prefix = @HonorificPrefix,
                honorific_suffix = @HonorificSuffix,
                birth_date = @BirthDate,
                birth_locality = @BirthLocality,
                marital_status = @MaritalStatus,
                initials = @Initials,
                given_name = @GivenName,
                family_name = @FamilyName,
                family_name_prefix = @FamilyNamePrefix,
                convention = @Convention,
                nick_name = @NickName,
                family_name_partner = @FamilyNamePartner,
                family_name_partner_prefix = @FamilyNamePartnerPrefix,
                blocked = @Blocked,
                status_reason = @StatusReason,
                excluded = @Excluded,
                hr_excluded = @HrExcluded,
                manual_excluded = @ManualExcluded,
                primary_manager_person_id = @PrimaryManagerPersonId,
                primary_manager_source = @PrimaryManagerSource,
                primary_manager_updated_at = @PrimaryManagerUpdatedAt
            WHERE person_id = @PersonId";

        return await connection.ExecuteAsync(sql, person).ConfigureAwait(false);
    }

    public async Task<int> DeleteAsync(string personId)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = "DELETE FROM persons WHERE person_id = @PersonId";

        return await connection.ExecuteAsync(sql, new { PersonId = personId }).ConfigureAwait(false);
    }

    public async Task<IEnumerable<Person>> GetAllAsync()
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            SELECT
                person_id AS PersonId,
                display_name AS DisplayName,
                external_id AS ExternalId,
                user_name AS UserName,
                gender AS Gender,
                blocked AS Blocked,
                excluded AS Excluded
            FROM persons
            ORDER BY display_name";

        return await connection.QueryAsync<Person>(sql).ConfigureAwait(false);
    }

    public async Task<IEnumerable<CustomFieldDto>> GetCustomFieldsAsync(string personId)
    {
        using var connection = _connectionFactory.CreateConnection();

        // Database-specific SQL for JSON extraction
        string sql;
        if (_connectionFactory.DatabaseType == DatabaseType.PostgreSql)
        {
            // PostgreSQL: Use jsonb_extract_path_text or ->> operator
            sql = @"
                SELECT
                    s.field_key AS FieldKey,
                    s.display_name AS DisplayName,
                    'text' AS DataType,
                    jsonb_extract_path_text(p.custom_fields::jsonb, s.field_key) AS Value
                FROM custom_field_schemas s
                INNER JOIN persons p ON 1=1
                WHERE s.table_name = 'persons'
                    AND p.person_id = @PersonId
                ORDER BY s.sort_order, s.display_name";
        }
        else
        {
            // SQLite: Use json_extract
            sql = @"
                SELECT
                    s.field_key AS FieldKey,
                    s.display_name AS DisplayName,
                    'text' AS DataType,
                    json_extract(p.custom_fields, '$.' || s.field_key) AS Value
                FROM custom_field_schemas s
                INNER JOIN persons p ON 1=1
                WHERE s.table_name = 'persons'
                    AND p.person_id = @PersonId
                ORDER BY s.sort_order, s.display_name";
        }

        return await connection.QueryAsync<CustomFieldDto>(sql, new { PersonId = personId }).ConfigureAwait(false);
    }

    public async Task<PersonDetailDto?> GetPersonWithMostContractsAsync(int skip = 0)
    {
        using var connection = _connectionFactory.CreateConnection();

        // First, try to get a person with multiple contracts, ordered by contract count descending
        var sql = @"
            WITH PersonContractCounts AS (
                SELECT
                    p.person_id,
                    p.display_name,
                    p.external_id,
                    COUNT(c.contract_id) as contract_count
                FROM persons p
                LEFT JOIN contracts c ON p.person_id = c.person_id
                GROUP BY p.person_id, p.display_name, p.external_id
                HAVING COUNT(c.contract_id) > 0
                ORDER BY contract_count DESC, p.display_name
                LIMIT 1 OFFSET @Skip
            )
            SELECT
                p.person_id AS PersonId,
                p.display_name AS DisplayName,
                p.external_id AS ExternalId,
                p.user_name AS UserName,
                p.gender AS Gender,
                p.birth_date AS BirthDate,
                p.blocked AS Blocked,
                p.excluded AS Excluded
            FROM persons p
            INNER JOIN PersonContractCounts pcc ON p.person_id = pcc.person_id
            LIMIT 1";

        var person = await connection.QueryFirstOrDefaultAsync<Person?>(sql, new { Skip = skip }).ConfigureAwait(false);

        if (person == null)
            return null;

        // Now get the full PersonDetailDto with contracts
        return await GetPersonDetailAsync(person.PersonId);
    }
}
