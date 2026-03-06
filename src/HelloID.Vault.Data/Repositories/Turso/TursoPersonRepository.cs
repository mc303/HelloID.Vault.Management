using System.Data;
using System.Diagnostics;
using HelloID.Vault.Core.Models.Entities;
using HelloID.Vault.Core.Models.DTOs;
using HelloID.Vault.Core.Models.Filters;
using HelloID.Vault.Data.Connection;
using HelloID.Vault.Data.Repositories.Interfaces;

namespace HelloID.Vault.Data.Repositories.Turso;

public class TursoPersonRepository : IPersonRepository
{
    private readonly ITursoClient _client;

    public TursoPersonRepository(ITursoClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    public async Task<IEnumerable<PersonListDto>> GetPagedAsync(PersonFilter filter, int page, int pageSize)
    {
        var whereClauses = new List<string>();
        var parameters = new Dictionary<string, object?>();

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            whereClauses.Add("(display_name LIKE ? OR external_id LIKE ? OR person_id LIKE ?)");
            parameters["SearchTerm1"] = $"%{filter.SearchTerm}%";
            parameters["SearchTerm2"] = $"%{filter.SearchTerm}%";
            parameters["SearchTerm3"] = $"%{filter.SearchTerm}%";
        }

        if (filter.Blocked.HasValue)
        {
            whereClauses.Add("blocked = ?");
            parameters["Blocked"] = filter.Blocked.Value ? 1 : 0;
        }

        if (filter.Excluded.HasValue)
        {
            whereClauses.Add("excluded = ?");
            parameters["Excluded"] = filter.Excluded.Value ? 1 : 0;
        }

        if (!string.IsNullOrWhiteSpace(filter.PersonStatus))
        {
            whereClauses.Add("person_status = ?");
            parameters["PersonStatus"] = filter.PersonStatus;
        }

        var whereClause = whereClauses.Count > 0 ? "WHERE " + string.Join(" AND ", whereClauses) : "";
        var offset = (page - 1) * pageSize;

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
            LIMIT ? OFFSET ?";

        parameters["Limit"] = pageSize;
        parameters["Offset"] = offset;

        var result = await _client.QueryAsync<PersonListDto>(sql, parameters);
        return result.Rows;
    }

    public async Task<int> GetCountAsync(PersonFilter filter)
    {
        var whereClauses = new List<string>();
        var parameters = new Dictionary<string, object?>();

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            whereClauses.Add("(display_name LIKE ? OR external_id LIKE ? OR person_id LIKE ?)");
            parameters["SearchTerm1"] = $"%{filter.SearchTerm}%";
            parameters["SearchTerm2"] = $"%{filter.SearchTerm}%";
            parameters["SearchTerm3"] = $"%{filter.SearchTerm}%";
        }

        if (filter.Blocked.HasValue)
        {
            whereClauses.Add("blocked = ?");
            parameters["Blocked"] = filter.Blocked.Value ? 1 : 0;
        }

        if (filter.Excluded.HasValue)
        {
            whereClauses.Add("excluded = ?");
            parameters["Excluded"] = filter.Excluded.Value ? 1 : 0;
        }

        if (!string.IsNullOrWhiteSpace(filter.PersonStatus))
        {
            whereClauses.Add("person_status = ?");
            parameters["PersonStatus"] = filter.PersonStatus;
        }

        var whereClause = whereClauses.Count > 0 ? "WHERE " + string.Join(" AND ", whereClauses) : "";
        var sql = $"SELECT COUNT(*) AS Count FROM person_list_view {whereClause}";

        var result = await _client.QueryFirstOrDefaultAsync<CountResult>(sql, parameters);
        return result?.Count ?? 0;
    }

    public async Task<Person?> GetByIdAsync(string personId)
    {
        Debug.WriteLine($"[TursoPersonRepository] GetByIdAsync: {personId}");
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
            WHERE person_id = ?";

        return await _client.QueryFirstOrDefaultAsync<Person>(sql, new { personId });
    }

    public async Task<PersonDetailDto?> GetPersonDetailAsync(string personId)
    {
        Debug.WriteLine($"[TursoPersonRepository] GetPersonDetailAsync: {personId}");
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
            WHERE p.person_id = ?";

        return await _client.QueryFirstOrDefaultAsync<PersonDetailDto>(sql, new { personId });
    }

    public async Task<Person?> GetByExternalIdAsync(string externalId)
    {
        Debug.WriteLine($"[TursoPersonRepository] GetByExternalIdAsync: {externalId}");
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
            WHERE external_id = ?";

        return await _client.QueryFirstOrDefaultAsync<Person>(sql, new { externalId });
    }

    public async Task<int> InsertAsync(Person person)
    {
        Debug.WriteLine($"[TursoPersonRepository] InsertAsync: {person.PersonId}");
        var sql = @"
            INSERT INTO persons (
                person_id, display_name, external_id, user_name, gender,
                honorific_prefix, honorific_suffix, birth_date, birth_locality, marital_status,
                initials, given_name, family_name, family_name_prefix, convention,
                nick_name, family_name_partner, family_name_partner_prefix,
                blocked, status_reason, excluded, hr_excluded, manual_excluded, source,
                primary_manager_person_id, primary_manager_source, primary_manager_updated_at
            ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)";

        return await _client.ExecuteAsync(sql, person);
    }

    public async Task<int> InsertAsync(Person person, IDbConnection connection, IDbTransaction transaction)
    {
        return await InsertAsync(person);
    }

    public async Task<int> UpdateAsync(Person person)
    {
        Debug.WriteLine($"[TursoPersonRepository] UpdateAsync: {person.PersonId}");
        var sql = @"
            UPDATE persons SET
                display_name = ?,
                user_name = ?,
                gender = ?,
                honorific_prefix = ?,
                honorific_suffix = ?,
                birth_date = ?,
                birth_locality = ?,
                marital_status = ?,
                initials = ?,
                given_name = ?,
                family_name = ?,
                family_name_prefix = ?,
                convention = ?,
                nick_name = ?,
                family_name_partner = ?,
                family_name_partner_prefix = ?,
                blocked = ?,
                status_reason = ?,
                excluded = ?,
                hr_excluded = ?,
                manual_excluded = ?,
                primary_manager_person_id = ?,
                primary_manager_source = ?,
                primary_manager_updated_at = ?
            WHERE person_id = ?";

        // Use anonymous object with only the parameters needed, in correct order
        return await _client.ExecuteAsync(sql, new
        {
            person.DisplayName,
            person.UserName,
            person.Gender,
            person.HonorificPrefix,
            person.HonorificSuffix,
            person.BirthDate,
            person.BirthLocality,
            person.MaritalStatus,
            person.Initials,
            person.GivenName,
            person.FamilyName,
            person.FamilyNamePrefix,
            person.Convention,
            person.NickName,
            person.FamilyNamePartner,
            person.FamilyNamePartnerPrefix,
            person.Blocked,
            person.StatusReason,
            person.Excluded,
            person.HrExcluded,
            person.ManualExcluded,
            person.PrimaryManagerPersonId,
            person.PrimaryManagerSource,
            person.PrimaryManagerUpdatedAt,
            person.PersonId
        });
    }

    public async Task<int> DeleteAsync(string personId)
    {
        Debug.WriteLine($"[TursoPersonRepository] DeleteAsync: {personId}");
        var sql = "DELETE FROM persons WHERE person_id = ?";
        return await _client.ExecuteAsync(sql, new { personId });
    }

    public async Task<IEnumerable<Person>> GetAllAsync()
    {
        Debug.WriteLine("[TursoPersonRepository] GetAllAsync");
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

        var result = await _client.QueryAsync<Person>(sql);
        return result.Rows;
    }

    public async Task<IEnumerable<CustomFieldDto>> GetCustomFieldsAsync(string personId)
    {
        Debug.WriteLine($"[TursoPersonRepository] GetCustomFieldsAsync: {personId}");
        var sql = @"
            SELECT
                s.field_key AS FieldKey,
                s.display_name AS DisplayName,
                'text' AS DataType,
                json_extract(p.custom_fields, '$.' || s.field_key) AS Value
            FROM custom_field_schemas s
            INNER JOIN persons p ON 1=1
            WHERE s.table_name = 'persons'
                AND p.person_id = ?
            ORDER BY s.sort_order, s.display_name";

        var result = await _client.QueryAsync<CustomFieldDto>(sql, new { personId });
        return result.Rows;
    }

    public async Task<PersonDetailDto?> GetPersonWithMostContractsAsync(int skip = 0)
    {
        Debug.WriteLine($"[TursoPersonRepository] GetPersonWithMostContractsAsync: skip={skip}");
        
        var sql = @"
            SELECT
                p.person_id AS PersonId,
                p.display_name AS DisplayName,
                p.external_id AS ExternalId
            FROM persons p
            LEFT JOIN contracts c ON p.person_id = c.person_id
            GROUP BY p.person_id, p.display_name, p.external_id
            HAVING COUNT(c.contract_id) > 0
            ORDER BY COUNT(c.contract_id) DESC, p.display_name
            LIMIT 1 OFFSET ?";

        var person = await _client.QueryFirstOrDefaultAsync<PersonSummary>(sql, new { skip });

        if (person == null)
            return null;

        return await GetPersonDetailAsync(person.PersonId);
    }
}

internal class PersonSummary
{
    public string PersonId { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? ExternalId { get; set; }
}
