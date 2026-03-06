using System.Data;
using System.Diagnostics;
using HelloID.Vault.Core.Models.Entities;
using HelloID.Vault.Core.Models.DTOs;
using HelloID.Vault.Core.Models.Filters;
using HelloID.Vault.Data.Connection;
using HelloID.Vault.Data.Repositories.Interfaces;

namespace HelloID.Vault.Data.Repositories.Turso;

public class TursoContractRepository : IContractRepository
{
    private readonly ITursoClient _client;

    public TursoContractRepository(ITursoClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    public async Task<IEnumerable<ContractDto>> GetPagedAsync(ContractFilter filter, int page, int pageSize)
    {
        Debug.WriteLine($"[TursoContractRepository] GetPagedAsync: page={page}");

        var whereClauses = new List<string>();
        var parameters = new Dictionary<string, object?>();

        if (!string.IsNullOrWhiteSpace(filter.PersonId))
        {
            whereClauses.Add("c.person_id = ?");
            parameters["PersonId"] = filter.PersonId;
        }

        if (!string.IsNullOrWhiteSpace(filter.TypeCode))
        {
            whereClauses.Add("c.type_code = ?");
            parameters["TypeCode"] = filter.TypeCode;
        }

        if (!string.IsNullOrWhiteSpace(filter.DepartmentExternalId))
        {
            whereClauses.Add("c.department_external_id = ? AND c.department_source = ?");
            parameters["DepartmentExternalId"] = filter.DepartmentExternalId;
            parameters["DepartmentSource"] = filter.DepartmentSource;
        }

        var whereClause = whereClauses.Count > 0 ? "WHERE " + string.Join(" AND ", whereClauses) : "";
        var offset = (page - 1) * pageSize;

        parameters["Limit"] = pageSize;
        parameters["Offset"] = offset;

        var sql = $@"
            SELECT
                c.contract_id AS ContractId,
                c.external_id AS ExternalId,
                c.person_id AS PersonId,
                c.start_date AS StartDate,
                c.end_date AS EndDate,
                c.type_code AS TypeCode,
                c.type_description AS TypeDescription,
                c.fte AS Fte,
                c.hours_per_week AS HoursPerWeek,
                c.percentage AS Percentage,
                m.display_name AS ManagerName,
                l.name AS LocationName,
                d.display_name AS DepartmentName,
                t.name AS TitleName
            FROM contracts c
            LEFT JOIN persons m ON c.manager_person_external_id = m.person_id
            LEFT JOIN locations l ON c.location_external_id = l.external_id AND c.location_source = l.source
            LEFT JOIN departments d ON c.department_external_id = d.external_id AND c.department_source = d.source
            LEFT JOIN titles t ON c.title_external_id = t.external_id AND c.title_source = t.source
            {whereClause}
            ORDER BY c.start_date DESC
            LIMIT ? OFFSET ?";

        var result = await _client.QueryAsync<ContractDto>(sql, parameters);
        return result.Rows;
    }

    public async Task<(IEnumerable<ContractDetailDto> items, int totalCount)> GetPagedDetailsAsync(ContractFilter filter, int page, int pageSize)
    {
        Debug.WriteLine($"[TursoContractRepository] GetPagedDetailsAsync: page={page}");

        var whereClauses = new List<string>();
        var parameters = new Dictionary<string, object?>();

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            whereClauses.Add(@"(person_id LIKE ? OR person_name LIKE ? OR external_id LIKE ? OR type_description LIKE ?)");
            var searchTerm = $"%{filter.SearchTerm}%";
            parameters["SearchTerm1"] = searchTerm;
            parameters["SearchTerm2"] = searchTerm;
            parameters["SearchTerm3"] = searchTerm;
            parameters["SearchTerm4"] = searchTerm;
        }

        if (!string.IsNullOrWhiteSpace(filter.PersonId))
        {
            whereClauses.Add("person_id = ?");
            parameters["PersonId"] = filter.PersonId;
        }

        if (!string.IsNullOrWhiteSpace(filter.TypeCode))
        {
            whereClauses.Add("type_code = ?");
            parameters["TypeCode"] = filter.TypeCode;
        }

        if (!string.IsNullOrWhiteSpace(filter.DepartmentExternalId))
        {
            whereClauses.Add("department_external_id = ? AND department_source = ?");
            parameters["DepartmentExternalId"] = filter.DepartmentExternalId;
            parameters["DepartmentSource"] = filter.DepartmentSource;
        }

        var whereClause = whereClauses.Count > 0 ? "WHERE " + string.Join(" AND ", whereClauses) : "";

        var countSql = $"SELECT COUNT(*) AS Count FROM contract_details_view {whereClause}";
        var countResult = await _client.QueryFirstOrDefaultAsync<CountResult>(countSql, parameters);
        var totalCount = countResult?.Count ?? 0;

        var offset = (page - 1) * pageSize;
        parameters["Limit"] = pageSize;
        parameters["Offset"] = offset;

        var sql = $@"
            SELECT
                c.contract_id AS ContractId,
                c.external_id AS ExternalId,
                c.person_id AS PersonId,
                c.start_date AS StartDate,
                c.end_date AS EndDate,
                c.type_code AS TypeCode,
                c.type_description AS TypeDescription,
                c.fte AS Fte,
                c.hours_per_week AS HoursPerWeek,
                c.percentage AS Percentage,
                c.sequence AS Sequence,
                c.person_name AS PersonName,
                c.person_external_id AS PersonExternalId,
                c.manager_person_external_id AS ManagerPersonExternalId,
                c.manager_person_name AS ManagerPersonName,
                c.location_external_id AS LocationExternalId,
                c.location_code AS LocationCode,
                c.location_name AS LocationName,
                c.cost_center_external_id AS CostCenterExternalId,
                c.cost_center_code AS CostCenterCode,
                c.cost_center_name AS CostCenterName,
                c.cost_bearer_external_id AS CostBearerExternalId,
                c.cost_bearer_code AS CostBearerCode,
                c.cost_bearer_name AS CostBearerName,
                c.employer_external_id AS EmployerExternalId,
                c.employer_code AS EmployerCode,
                c.employer_name AS EmployerName,
                c.team_external_id AS TeamExternalId,
                c.team_code AS TeamCode,
                c.team_name AS TeamName,
                c.department_external_id AS DepartmentExternalId,
                c.department_name AS DepartmentName,
                c.department_code AS DepartmentCode,
                c.department_parent_external_id AS DepartmentParentExternalId,
                c.department_manager_person_id AS DepartmentManagerPersonId,
                c.department_manager_name AS DepartmentManagerName,
                c.department_parent_department_name AS DepartmentParentDepartmentName,
                c.division_external_id AS DivisionExternalId,
                c.division_code AS DivisionCode,
                c.division_name AS DivisionName,
                c.title_external_id AS TitleExternalId,
                c.title_code AS TitleCode,
                c.title_name AS TitleName,
                c.organization_external_id AS OrganizationExternalId,
                c.organization_code AS OrganizationCode,
                c.organization_name AS OrganizationName,
                c.contract_status AS ContractStatus,
                c.contract_date_range AS ContractDateRange,
                c.source AS Source,
                ss.display_name AS SourceDisplayName
            FROM contract_details_view c
            LEFT JOIN source_system ss ON c.source = ss.system_id
            {whereClause}
            ORDER BY c.start_date DESC
            LIMIT ? OFFSET ?";

        var result = await _client.QueryAsync<ContractDetailDto>(sql, parameters);
        return (result.Rows, totalCount);
    }

    public async Task<IEnumerable<ContractDto>> GetByPersonIdAsync(string personId)
    {
        Debug.WriteLine($"[TursoContractRepository] GetByPersonIdAsync: {personId}");
        var sql = @"
            SELECT
                c.contract_id AS ContractId,
                c.external_id AS ExternalId,
                c.person_id AS PersonId,
                c.start_date AS StartDate,
                c.end_date AS EndDate,
                c.type_code AS TypeCode,
                c.type_description AS TypeDescription,
                c.fte AS Fte,
                c.hours_per_week AS HoursPerWeek,
                c.percentage AS Percentage,
                m.display_name AS ManagerName,
                l.name AS LocationName,
                d.display_name AS DepartmentName,
                t.name AS TitleName
            FROM contracts c
            LEFT JOIN persons m ON c.manager_person_external_id = m.person_id
            LEFT JOIN locations l ON c.location_external_id = l.external_id AND c.location_source = l.source
            LEFT JOIN departments d ON c.department_external_id = d.external_id AND c.department_source = d.source
            LEFT JOIN titles t ON c.title_external_id = t.external_id AND c.title_source = t.source
            WHERE c.person_id = ?
            ORDER BY c.start_date DESC";

        var result = await _client.QueryAsync<ContractDto>(sql, new { personId });
        return result.Rows;
    }

    public async Task<Contract?> GetByIdAsync(int contractId)
    {
        Debug.WriteLine($"[TursoContractRepository] GetByIdAsync: {contractId}");
        var sql = @"
            SELECT
                contract_id AS ContractId,
                external_id AS ExternalId,
                person_id AS PersonId,
                start_date AS StartDate,
                end_date AS EndDate,
                type_code AS TypeCode,
                type_description AS TypeDescription,
                fte AS Fte,
                hours_per_week AS HoursPerWeek,
                percentage AS Percentage,
                sequence AS Sequence,
                manager_person_external_id AS ManagerPersonExternalId,
                location_external_id AS LocationExternalId,
                cost_center_external_id AS CostCenterExternalId,
                cost_bearer_external_id AS CostBearerExternalId,
                employer_external_id AS EmployerExternalId,
                team_external_id AS TeamExternalId,
                department_external_id AS DepartmentExternalId,
                division_external_id AS DivisionExternalId,
                title_external_id AS TitleExternalId,
                organization_external_id AS OrganizationExternalId,
                source AS Source,
                location_source AS LocationSource,
                cost_center_source AS CostCenterSource,
                cost_bearer_source AS CostBearerSource,
                employer_source AS EmployerSource,
                team_source AS TeamSource,
                department_source AS DepartmentSource,
                division_source AS DivisionSource,
                title_source AS TitleSource,
                organization_source AS OrganizationSource
            FROM contracts
            WHERE contract_id = ?";

        return await _client.QueryFirstOrDefaultAsync<Contract>(sql, new { contractId });
    }

    public async Task<int> InsertAsync(Contract contract)
    {
        Debug.WriteLine($"[TursoContractRepository] InsertAsync: {contract.ExternalId}");
        var sql = @"
            INSERT INTO contracts (
                external_id, person_id, start_date, end_date, type_code, type_description,
                fte, hours_per_week, percentage, sequence,
                manager_person_external_id, location_external_id, cost_center_external_id, cost_bearer_external_id,
                employer_external_id, team_external_id, department_external_id, division_external_id, title_external_id, organization_external_id, source,
                location_source, cost_center_source, cost_bearer_source, employer_source,
                team_source, department_source, division_source, title_source, organization_source
            ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)";

        return await _client.ExecuteAsync(sql, contract);
    }

    public async Task<int> InsertAsync(Contract contract, IDbConnection connection, IDbTransaction transaction)
    {
        return await InsertAsync(contract);
    }

    public async Task<int> UpdateAsync(Contract contract)
    {
        Debug.WriteLine($"[TursoContractRepository] UpdateAsync: {contract.ContractId}");

        var managerId = contract.ManagerPersonExternalId == "00000000-0000-0000-0000-000000000000"
            ? null
            : contract.ManagerPersonExternalId;

        var sql = @"
            UPDATE contracts SET
                external_id = ?,
                person_id = ?,
                start_date = ?,
                end_date = ?,
                type_code = ?,
                type_description = ?,
                fte = ?,
                hours_per_week = ?,
                percentage = ?,
                sequence = ?,
                manager_person_external_id = ?,
                location_external_id = ?,
                cost_center_external_id = ?,
                cost_bearer_external_id = ?,
                employer_external_id = ?,
                team_external_id = ?,
                department_external_id = ?,
                division_external_id = ?,
                title_external_id = ?,
                organization_external_id = ?,
                location_source = ?,
                cost_center_source = ?,
                cost_bearer_source = ?,
                employer_source = ?,
                team_source = ?,
                department_source = ?,
                division_source = ?,
                title_source = ?,
                organization_source = ?
            WHERE contract_id = ?";

        return await _client.ExecuteAsync(sql, new
        {
            contract.ExternalId,
            contract.PersonId,
            contract.StartDate,
            contract.EndDate,
            contract.TypeCode,
            contract.TypeDescription,
            contract.Fte,
            contract.HoursPerWeek,
            contract.Percentage,
            contract.Sequence,
            ManagerPersonExternalId = managerId,
            contract.LocationExternalId,
            contract.CostCenterExternalId,
            contract.CostBearerExternalId,
            contract.EmployerExternalId,
            contract.TeamExternalId,
            contract.DepartmentExternalId,
            contract.DivisionExternalId,
            contract.TitleExternalId,
            contract.OrganizationExternalId,
            contract.LocationSource,
            contract.CostCenterSource,
            contract.CostBearerSource,
            contract.EmployerSource,
            contract.TeamSource,
            contract.DepartmentSource,
            contract.DivisionSource,
            contract.TitleSource,
            contract.OrganizationSource,
            contract.ContractId
        });
    }

    public async Task<int> DeleteAsync(int contractId)
    {
        Debug.WriteLine($"[TursoContractRepository] DeleteAsync: {contractId}");
        var sql = "DELETE FROM contracts WHERE contract_id = ?";
        return await _client.ExecuteAsync(sql, new { contractId });
    }

    public async Task<int> GetCountAsync(ContractFilter filter)
    {
        var whereClauses = new List<string>();
        var parameters = new Dictionary<string, object?>();

        if (!string.IsNullOrWhiteSpace(filter.PersonId))
        {
            whereClauses.Add("person_id = ?");
            parameters["PersonId"] = filter.PersonId;
        }

        if (!string.IsNullOrWhiteSpace(filter.TypeCode))
        {
            whereClauses.Add("type_code = ?");
            parameters["TypeCode"] = filter.TypeCode;
        }

        var whereClause = whereClauses.Count > 0 ? "WHERE " + string.Join(" AND ", whereClauses) : "";
        var sql = $"SELECT COUNT(*) AS Count FROM contracts {whereClause}";

        var result = await _client.QueryFirstOrDefaultAsync<CountResult>(sql, parameters);
        return result?.Count ?? 0;
    }

    public async Task<ContractJsonDto?> GetJsonViewByIdAsync(int contractId)
    {
        Debug.WriteLine($"[TursoContractRepository] GetJsonViewByIdAsync: {contractId}");
        var sql = @"
            SELECT
                external_id AS ExternalId,
                person_id AS PersonId,
                start_date AS StartDate,
                end_date AS EndDate,
                type_code AS TypeCode,
                type_description AS TypeDescription,
                fte AS Fte,
                hours_per_week AS HoursPerWeek,
                percentage AS Percentage,
                sequence AS Sequence,
                manager_person_external_id AS ManagerPersonExternalId,
                manager_external_id AS ManagerExternalId,
                manager_display_name AS ManagerDisplayName,
                manager_email AS ManagerEmail,
                location_external_id AS LocationExternalId,
                location_code AS LocationCode,
                location_name AS LocationName,
                cost_center_external_id AS CostCenterExternalId,
                cost_center_code AS CostCenterCode,
                cost_center_name AS CostCenterName,
                cost_bearer_external_id AS CostBearerExternalId,
                cost_bearer_code AS CostBearerCode,
                cost_bearer_name AS CostBearerName,
                employer_external_id AS EmployerExternalId,
                employer_code AS EmployerCode,
                employer_name AS EmployerName,
                team_external_id AS TeamExternalId,
                team_code AS TeamCode,
                team_name AS TeamName,
                department_external_id AS DepartmentExternalId,
                department_display_name AS DepartmentDisplayName,
                division_external_id AS DivisionExternalId,
                division_code AS DivisionCode,
                division_name AS DivisionName,
                title_external_id AS TitleExternalId,
                title_code AS TitleCode,
                title_name AS TitleName,
                organization_external_id AS OrganizationExternalId,
                organization_code AS OrganizationCode,
                organization_name AS OrganizationName
            FROM contract_json_view
            WHERE contract_id = ?";

        var result = await _client.QueryFirstOrDefaultAsync<ContractJsonData>(sql, new { contractId });

        if (result == null)
            return null;

        return new ContractJsonDto
        {
            Context = new Context { InConditions = false },
            ExternalId = result.ExternalId,
            StartDate = result.StartDate,
            EndDate = result.EndDate,
            Type = new ContractType
            {
                Code = result.TypeCode,
                Description = result.TypeDescription
            },
            Details = new ContractDetails
            {
                Fte = result.Fte,
                HoursPerWeek = result.HoursPerWeek,
                Percentage = result.Percentage,
                Sequence = result.Sequence
            },
            Location = new HelloID.Vault.Core.Models.DTOs.Location { ExternalId = result.LocationExternalId, Code = result.LocationCode, Name = result.LocationName },
            CostCenter = new HelloID.Vault.Core.Models.DTOs.CostCenter { ExternalId = result.CostCenterExternalId, Code = result.CostCenterCode, Name = result.CostCenterName },
            CostBearer = new HelloID.Vault.Core.Models.DTOs.CostBearer { ExternalId = result.CostBearerExternalId, Code = result.CostBearerCode, Name = result.CostBearerName },
            Employer = new HelloID.Vault.Core.Models.DTOs.Employer { ExternalId = result.EmployerExternalId, Code = result.EmployerCode, Name = result.EmployerName },
            Manager = new Manager { PersonId = result.ManagerPersonExternalId, ExternalId = result.ManagerExternalId, DisplayName = result.ManagerDisplayName, Email = result.ManagerEmail },
            Team = new HelloID.Vault.Core.Models.DTOs.Team { ExternalId = result.TeamExternalId, Code = result.TeamCode, Name = result.TeamName },
            Department = new ContractDepartment { ExternalId = result.DepartmentExternalId, DisplayName = result.DepartmentDisplayName },
            Division = new HelloID.Vault.Core.Models.DTOs.Division { ExternalId = result.DivisionExternalId, Code = result.DivisionCode, Name = result.DivisionName },
            Title = new HelloID.Vault.Core.Models.DTOs.Title { ExternalId = result.TitleExternalId, Code = result.TitleCode, Name = result.TitleName },
            Organization = new HelloID.Vault.Core.Models.DTOs.Organization { ExternalId = result.OrganizationExternalId, Code = result.OrganizationCode, Name = result.OrganizationName }
        };
    }

    public async Task<IEnumerable<ContractDetailDto>> GetAllDetailsAsync()
    {
        Debug.WriteLine("[TursoContractRepository] GetAllDetailsAsync");
        var sql = @"
            SELECT
                c.contract_id AS ContractId,
                c.external_id AS ExternalId,
                c.person_id AS PersonId,
                c.person_name AS PersonName,
                c.person_external_id AS PersonExternalId,
                c.start_date AS StartDate,
                c.end_date AS EndDate,
                c.type_code AS TypeCode,
                c.type_description AS TypeDescription,
                c.fte AS Fte,
                c.hours_per_week AS HoursPerWeek,
                c.percentage AS Percentage,
                c.sequence AS Sequence,
                c.manager_person_external_id AS ManagerPersonExternalId,
                c.manager_person_name AS ManagerPersonName,
                c.location_external_id AS LocationExternalId,
                c.location_code AS LocationCode,
                c.location_name AS LocationName,
                c.cost_center_external_id AS CostCenterExternalId,
                c.cost_center_code AS CostCenterCode,
                c.cost_center_name AS CostCenterName,
                c.cost_bearer_external_id AS CostBearerExternalId,
                c.cost_bearer_code AS CostBearerCode,
                c.cost_bearer_name AS CostBearerName,
                c.employer_external_id AS EmployerExternalId,
                c.employer_code AS EmployerCode,
                c.employer_name AS EmployerName,
                c.team_external_id AS TeamExternalId,
                c.team_code AS TeamCode,
                c.team_name AS TeamName,
                c.department_external_id AS DepartmentExternalId,
                c.department_name AS DepartmentName,
                c.department_code AS DepartmentCode,
                c.department_parent_external_id AS DepartmentParentExternalId,
                c.department_manager_person_id AS DepartmentManagerPersonId,
                c.department_manager_name AS DepartmentManagerName,
                c.department_parent_department_name AS DepartmentParentDepartmentName,
                c.division_external_id AS DivisionExternalId,
                c.division_code AS DivisionCode,
                c.division_name AS DivisionName,
                c.title_external_id AS TitleExternalId,
                c.title_code AS TitleCode,
                c.title_name AS TitleName,
                c.organization_external_id AS OrganizationExternalId,
                c.organization_code AS OrganizationCode,
                c.organization_name AS OrganizationName,
                c.contract_status AS ContractStatus,
                c.contract_date_range AS ContractDateRange,
                c.source AS Source,
                ss.display_name AS SourceDisplayName
            FROM contract_details_view c
            LEFT JOIN source_system ss ON c.source = ss.system_id
            ORDER BY c.start_date DESC";

        var result = await _client.QueryAsync<ContractDetailDto>(sql);
        return result.Rows;
    }

    public async Task<IEnumerable<ContractDetailDto>> GetDetailsByPersonIdAsync(string personId)
    {
        Debug.WriteLine($"[TursoContractRepository] GetDetailsByPersonIdAsync: {personId}");
        var sql = @"
            SELECT
                c.contract_id AS ContractId,
                c.external_id AS ExternalId,
                c.person_id AS PersonId,
                c.person_name AS PersonName,
                c.person_external_id AS PersonExternalId,
                c.start_date AS StartDate,
                c.end_date AS EndDate,
                c.type_code AS TypeCode,
                c.type_description AS TypeDescription,
                c.fte AS Fte,
                c.hours_per_week AS HoursPerWeek,
                c.percentage AS Percentage,
                c.sequence AS Sequence,
                c.manager_person_external_id AS ManagerPersonExternalId,
                c.manager_person_name AS ManagerPersonName,
                c.location_external_id AS LocationExternalId,
                c.location_code AS LocationCode,
                c.location_name AS LocationName,
                c.cost_center_external_id AS CostCenterExternalId,
                c.cost_center_code AS CostCenterCode,
                c.cost_center_name AS CostCenterName,
                c.cost_bearer_external_id AS CostBearerExternalId,
                c.cost_bearer_code AS CostBearerCode,
                c.cost_bearer_name AS CostBearerName,
                c.employer_external_id AS EmployerExternalId,
                c.employer_code AS EmployerCode,
                c.employer_name AS EmployerName,
                c.team_external_id AS TeamExternalId,
                c.team_code AS TeamCode,
                c.team_name AS TeamName,
                c.department_external_id AS DepartmentExternalId,
                c.department_name AS DepartmentName,
                c.department_code AS DepartmentCode,
                c.department_parent_external_id AS DepartmentParentExternalId,
                c.department_manager_person_id AS DepartmentManagerPersonId,
                c.department_manager_name AS DepartmentManagerName,
                c.department_parent_department_name AS DepartmentParentDepartmentName,
                c.division_external_id AS DivisionExternalId,
                c.division_code AS DivisionCode,
                c.division_name AS DivisionName,
                c.title_external_id AS TitleExternalId,
                c.title_code AS TitleCode,
                c.title_name AS TitleName,
                c.organization_external_id AS OrganizationExternalId,
                c.organization_code AS OrganizationCode,
                c.organization_name AS OrganizationName,
                c.contract_status AS ContractStatus,
                c.contract_date_range AS ContractDateRange,
                c.source AS Source,
                ss.display_name AS SourceDisplayName
            FROM contract_details_view c
            LEFT JOIN source_system ss ON c.source = ss.system_id
            WHERE c.person_id = ?
            ORDER BY c.start_date DESC";

        var result = await _client.QueryAsync<ContractDetailDto>(sql, new { personId });
        return result.Rows;
    }

    public async Task<IEnumerable<ContractDetailDto>> GetAllFromCacheAsync()
    {
        Debug.WriteLine("[TursoContractRepository] GetAllFromCacheAsync");
        var sql = @"
            SELECT
                c.contract_id AS ContractId,
                c.external_id AS ExternalId,
                c.person_id AS PersonId,
                c.person_name AS PersonName,
                c.person_external_id AS PersonExternalId,
                c.start_date AS StartDate,
                c.end_date AS EndDate,
                c.type_code AS TypeCode,
                c.type_description AS TypeDescription,
                c.fte AS Fte,
                c.hours_per_week AS HoursPerWeek,
                c.percentage AS Percentage,
                c.sequence AS Sequence,
                c.manager_person_external_id AS ManagerPersonExternalId,
                c.manager_person_name AS ManagerPersonName,
                c.location_external_id AS LocationExternalId,
                c.location_code AS LocationCode,
                c.location_name AS LocationName,
                c.cost_center_external_id AS CostCenterExternalId,
                c.cost_center_code AS CostCenterCode,
                c.cost_center_name AS CostCenterName,
                c.cost_bearer_external_id AS CostBearerExternalId,
                c.cost_bearer_code AS CostBearerCode,
                c.cost_bearer_name AS CostBearerName,
                c.employer_external_id AS EmployerExternalId,
                c.employer_code AS EmployerCode,
                c.employer_name AS EmployerName,
                c.team_external_id AS TeamExternalId,
                c.team_code AS TeamCode,
                c.team_name AS TeamName,
                c.department_external_id AS DepartmentExternalId,
                c.department_name AS DepartmentName,
                c.department_code AS DepartmentCode,
                c.department_parent_external_id AS DepartmentParentExternalId,
                c.department_manager_person_id AS DepartmentManagerPersonId,
                c.department_manager_name AS DepartmentManagerName,
                c.department_parent_department_name AS DepartmentParentDepartmentName,
                c.division_external_id AS DivisionExternalId,
                c.division_code AS DivisionCode,
                c.division_name AS DivisionName,
                c.title_external_id AS TitleExternalId,
                c.title_code AS TitleCode,
                c.title_name AS TitleName,
                c.organization_external_id AS OrganizationExternalId,
                c.organization_code AS OrganizationCode,
                c.organization_name AS OrganizationName,
                c.contract_status AS ContractStatus,
                c.contract_date_range AS ContractDateRange,
                c.source AS Source,
                ss.display_name AS SourceDisplayName
            FROM contract_details_cache c
            LEFT JOIN source_system ss ON c.source = ss.system_id
            ORDER BY c.start_date DESC";

        var result = await _client.QueryAsync<ContractDetailDto>(sql);
        return result.Rows;
    }

    public async Task RebuildCacheAsync()
    {
        Debug.WriteLine("[TursoContractRepository] RebuildCacheAsync");
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        await _client.ExecuteAsync("DELETE FROM contract_details_cache");
        await _client.ExecuteAsync("INSERT INTO contract_details_cache SELECT * FROM contract_details_view");

        var countResult = await _client.QueryFirstOrDefaultAsync<CountResult>("SELECT COUNT(*) AS Count FROM contract_details_cache");
        var rowCount = countResult?.Count ?? 0;

        stopwatch.Stop();

        var updateMetadataSql = @"
            UPDATE cache_metadata
            SET last_refreshed = ?,
                row_count = ?,
                refresh_duration_ms = ?
            WHERE cache_name = ?";

        await _client.ExecuteAsync(updateMetadataSql, new
        {
            LastRefreshed = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
            RowCount = rowCount,
            RefreshDurationMs = stopwatch.ElapsedMilliseconds,
            CacheName = "contract_details_cache"
        });
    }

    public async Task RefreshContractCacheItemAsync(int contractId)
    {
        Debug.WriteLine($"[TursoContractRepository] RefreshContractCacheItemAsync: {contractId}");
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        await _client.ExecuteAsync(
            "INSERT OR REPLACE INTO contract_details_cache SELECT * FROM contract_details_view WHERE contract_id = ?",
            new { contractId });

        stopwatch.Stop();

        var updateMetadataSql = @"
            UPDATE cache_metadata
            SET last_refreshed = ?,
                refresh_duration_ms = ?
            WHERE cache_name = ?";

        await _client.ExecuteAsync(updateMetadataSql, new
        {
            LastRefreshed = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
            RefreshDurationMs = stopwatch.ElapsedMilliseconds,
            CacheName = "contract_details_cache"
        });
    }

    public async Task<CacheMetadata> GetCacheMetadataAsync()
    {
        Debug.WriteLine("[TursoContractRepository] GetCacheMetadataAsync");
        var sql = @"
            SELECT
                cache_name AS CacheName,
                last_refreshed AS LastRefreshed,
                row_count AS RowCount,
                refresh_duration_ms AS RefreshDurationMs
            FROM cache_metadata
            WHERE cache_name = ?";

        var result = await _client.QueryFirstOrDefaultAsync<CacheMetadata>(sql, new { CacheName = "contract_details_cache" });

        return result ?? new CacheMetadata
        {
            CacheName = "contract_details_cache",
            LastRefreshed = DateTime.MinValue,
            RowCount = 0,
            RefreshDurationMs = 0
        };
    }
}

internal class ContractJsonData
{
    public string? ExternalId { get; set; }
    public string? PersonId { get; set; }
    public string? StartDate { get; set; }
    public string? EndDate { get; set; }
    public string? TypeCode { get; set; }
    public string? TypeDescription { get; set; }
    public double? Fte { get; set; }
    public double? HoursPerWeek { get; set; }
    public double? Percentage { get; set; }
    public int? Sequence { get; set; }
    public string? ManagerPersonExternalId { get; set; }
    public string? ManagerExternalId { get; set; }
    public string? ManagerDisplayName { get; set; }
    public string? ManagerEmail { get; set; }
    public string? LocationExternalId { get; set; }
    public string? LocationCode { get; set; }
    public string? LocationName { get; set; }
    public string? CostCenterExternalId { get; set; }
    public string? CostCenterCode { get; set; }
    public string? CostCenterName { get; set; }
    public string? CostBearerExternalId { get; set; }
    public string? CostBearerCode { get; set; }
    public string? CostBearerName { get; set; }
    public string? EmployerExternalId { get; set; }
    public string? EmployerCode { get; set; }
    public string? EmployerName { get; set; }
    public string? TeamExternalId { get; set; }
    public string? TeamCode { get; set; }
    public string? TeamName { get; set; }
    public string? DepartmentExternalId { get; set; }
    public string? DepartmentDisplayName { get; set; }
    public string? DivisionExternalId { get; set; }
    public string? DivisionCode { get; set; }
    public string? DivisionName { get; set; }
    public string? TitleExternalId { get; set; }
    public string? TitleCode { get; set; }
    public string? TitleName { get; set; }
    public string? OrganizationExternalId { get; set; }
    public string? OrganizationCode { get; set; }
    public string? OrganizationName { get; set; }
}
