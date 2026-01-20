using Dapper;
using HelloID.Vault.Core.Models.Entities;
using HelloID.Vault.Core.Models.DTOs;
using HelloID.Vault.Core.Models.Filters;
using HelloID.Vault.Data.Connection;
using HelloID.Vault.Data.Repositories.Interfaces;
using System.Diagnostics;

namespace HelloID.Vault.Data.Repositories;

/// <summary>
/// Repository implementation for Contract entity using Dapper.
/// </summary>
public class ContractRepository : IContractRepository
{
    private readonly ISqliteConnectionFactory _connectionFactory;

    public ContractRepository(ISqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<IEnumerable<ContractDto>> GetPagedAsync(ContractFilter filter, int page, int pageSize)
    {
        using var connection = _connectionFactory.CreateConnection();

        var whereClauses = new List<string>();
        var parameters = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(filter.PersonId))
        {
            whereClauses.Add("c.person_id = @PersonId");
            parameters.Add("PersonId", filter.PersonId);
        }

        if (!string.IsNullOrWhiteSpace(filter.TypeCode))
        {
            whereClauses.Add("c.type_code = @TypeCode");
            parameters.Add("TypeCode", filter.TypeCode);
        }

        if (!string.IsNullOrWhiteSpace(filter.DepartmentExternalId))
        {
            whereClauses.Add("c.department_external_id = @DepartmentExternalId AND c.department_source = @DepartmentSource");
            parameters.Add("DepartmentExternalId", filter.DepartmentExternalId);
            parameters.Add("DepartmentSource", filter.DepartmentSource);
        }

        var whereClause = whereClauses.Count > 0 ? "WHERE " + string.Join(" AND ", whereClauses) : "";

        parameters.Add("Limit", pageSize);
        parameters.Add("Offset", (page - 1) * pageSize);

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
            LIMIT @Limit OFFSET @Offset";

        return await connection.QueryAsync<ContractDto>(sql, parameters);
    }

    public async Task<(IEnumerable<ContractDetailDto> items, int totalCount)> GetPagedDetailsAsync(ContractFilter filter, int page, int pageSize)
    {
        using var connection = _connectionFactory.CreateConnection();

        var whereClauses = new List<string>();
        var parameters = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            whereClauses.Add(@"(person_id LIKE @SearchTerm
                OR person_name LIKE @SearchTerm
                OR external_id LIKE @SearchTerm
                OR type_description LIKE @SearchTerm)");
            parameters.Add("SearchTerm", $"%{filter.SearchTerm}%");
        }

        if (!string.IsNullOrWhiteSpace(filter.PersonId))
        {
            whereClauses.Add("person_id = @PersonId");
            parameters.Add("PersonId", filter.PersonId);
        }

        if (!string.IsNullOrWhiteSpace(filter.TypeCode))
        {
            whereClauses.Add("type_code = @TypeCode");
            parameters.Add("TypeCode", filter.TypeCode);
        }

        if (!string.IsNullOrWhiteSpace(filter.DepartmentExternalId))
        {
            whereClauses.Add("department_external_id = @DepartmentExternalId AND department_source = @DepartmentSource");
            parameters.Add("DepartmentExternalId", filter.DepartmentExternalId);
            parameters.Add("DepartmentSource", filter.DepartmentSource);
        }

        var whereClause = whereClauses.Count > 0 ? "WHERE " + string.Join(" AND ", whereClauses) : "";

        // Get total count
        var countSql = $"SELECT COUNT(*) FROM contract_details_view {whereClause}";
        var totalCount = await connection.ExecuteScalarAsync<int>(countSql, parameters);

        // Get paged data
        parameters.Add("Limit", pageSize);
        parameters.Add("Offset", (page - 1) * pageSize);

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
            LIMIT @Limit OFFSET @Offset";

        var items = await connection.QueryAsync<ContractDetailDto>(sql, parameters);

        return (items, totalCount);
    }

    public async Task<IEnumerable<ContractDto>> GetByPersonIdAsync(string personId)
    {
        using var connection = _connectionFactory.CreateConnection();

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
            WHERE c.person_id = @PersonId
            ORDER BY c.start_date DESC";

        return await connection.QueryAsync<ContractDto>(sql, new { PersonId = personId });
    }

    public async Task<Contract?> GetByIdAsync(int contractId)
    {
        using var connection = _connectionFactory.CreateConnection();

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
            WHERE contract_id = @ContractId";

        return await connection.QuerySingleOrDefaultAsync<Contract>(sql, new { ContractId = contractId });
    }

    public async Task<int> InsertAsync(Contract contract)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            INSERT INTO contracts (
                external_id, person_id, start_date, end_date, type_code, type_description,
                fte, hours_per_week, percentage, sequence,
                manager_person_external_id, location_external_id, cost_center_external_id, cost_bearer_external_id,
                employer_external_id, team_external_id, department_external_id, division_external_id, title_external_id, organization_external_id, source,
                location_source, cost_center_source, cost_bearer_source, employer_source,
                team_source, department_source, division_source, title_source, organization_source
            ) VALUES (
                @ExternalId, @PersonId, @StartDate, @EndDate, @TypeCode, @TypeDescription,
                @Fte, @HoursPerWeek, @Percentage, @Sequence,
                @ManagerPersonExternalId, @LocationExternalId, @CostCenterExternalId, @CostBearerExternalId,
                @EmployerExternalId, @TeamExternalId, @DepartmentExternalId, @DivisionExternalId, @TitleExternalId, @OrganizationExternalId, @Source,
                @LocationSource, @CostCenterSource, @CostBearerSource, @EmployerSource,
                @TeamSource, @DepartmentSource, @DivisionSource, @TitleSource, @OrganizationSource
            )";

        return await connection.ExecuteAsync(sql, contract);
    }

    public async Task<int> InsertAsync(Contract contract, System.Data.IDbConnection connection, System.Data.IDbTransaction transaction)
    {
        var sql = @"
            INSERT INTO contracts (
                external_id, person_id, start_date, end_date, type_code, type_description,
                fte, hours_per_week, percentage, sequence,
                manager_person_external_id, location_external_id, cost_center_external_id, cost_bearer_external_id,
                employer_external_id, team_external_id, department_external_id, division_external_id, title_external_id, organization_external_id, source,
                location_source, cost_center_source, cost_bearer_source, employer_source,
                team_source, department_source, division_source, title_source, organization_source
            ) VALUES (
                @ExternalId, @PersonId, @StartDate, @EndDate, @TypeCode, @TypeDescription,
                @Fte, @HoursPerWeek, @Percentage, @Sequence,
                @ManagerPersonExternalId, @LocationExternalId, @CostCenterExternalId, @CostBearerExternalId,
                @EmployerExternalId, @TeamExternalId, @DepartmentExternalId, @DivisionExternalId, @TitleExternalId, @OrganizationExternalId, @Source,
                @LocationSource, @CostCenterSource, @CostBearerSource, @EmployerSource,
                @TeamSource, @DepartmentSource, @DivisionSource, @TitleSource, @OrganizationSource
            )";

        return await connection.ExecuteAsync(sql, contract, transaction);
    }

    public async Task<int> UpdateAsync(Contract contract)
    {
        using var connection = _connectionFactory.CreateConnection();

        Debug.WriteLine($"[ContractRepository] UpdateAsync - Contract ID: {contract.ContractId}, External ID: {contract.ExternalId}");
        Debug.WriteLine($"  Manager: {contract.ManagerPersonExternalId}");
        Debug.WriteLine($"  Department: {contract.DepartmentExternalId}");
        Debug.WriteLine($"  Location: {contract.LocationExternalId}");
        Debug.WriteLine($"  Title: {contract.TitleExternalId}");
        Debug.WriteLine($"  Organization: {contract.OrganizationExternalId}");

        // Treat empty GUID as null for FK validation
        var managerId = contract.ManagerPersonExternalId == "00000000-0000-0000-0000-000000000000"
            ? null
            : contract.ManagerPersonExternalId;

        // Validate FK references before update
        var errors = new List<string>();

        if (!string.IsNullOrWhiteSpace(managerId))
        {
            var exists = await connection.QuerySingleOrDefaultAsync<int>(
                "SELECT COUNT(*) FROM persons WHERE person_id = @ExternalId",
                new { ExternalId = managerId });
            if (exists == 0)
            {
                Debug.WriteLine($"[ContractRepository] Manager FK VALIDATION - Looking for person_id '{managerId}', found: {exists > 0}");
                errors.Add($"Manager person '{managerId}' not found");
            }
            else
            {
                Debug.WriteLine($"[ContractRepository] Manager FK VALIDATION - Looking for person_id '{managerId}', found: {exists > 0} (valid)");
            }
        }

        if (!string.IsNullOrWhiteSpace(contract.DepartmentExternalId))
        {
            var exists = await connection.QuerySingleOrDefaultAsync<int>(
                "SELECT COUNT(*) FROM departments WHERE external_id = @ExternalId AND source = @Source",
                new { ExternalId = contract.DepartmentExternalId, Source = contract.DepartmentSource });
            if (exists == 0)
                errors.Add($"Department '{contract.DepartmentExternalId}' not found");
        }

        if (!string.IsNullOrWhiteSpace(contract.LocationExternalId))
        {
            var exists = await connection.QuerySingleOrDefaultAsync<int>(
                "SELECT COUNT(*) FROM locations WHERE external_id = @ExternalId AND source = @Source",
                new { ExternalId = contract.LocationExternalId, Source = contract.LocationSource });
            if (exists == 0)
                errors.Add($"Location '{contract.LocationExternalId}' not found");
        }

        if (!string.IsNullOrWhiteSpace(contract.TitleExternalId))
        {
            var exists = await connection.QuerySingleOrDefaultAsync<int>(
                "SELECT COUNT(*) FROM titles WHERE external_id = @ExternalId AND source = @Source",
                new { ExternalId = contract.TitleExternalId, Source = contract.TitleSource });
            if (exists == 0)
                errors.Add($"Title '{contract.TitleExternalId}' not found");
        }

        if (!string.IsNullOrWhiteSpace(contract.OrganizationExternalId))
        {
            var exists = await connection.QuerySingleOrDefaultAsync<int>(
                "SELECT COUNT(*) FROM organizations WHERE external_id = @ExternalId AND source = @Source",
                new { ExternalId = contract.OrganizationExternalId, Source = contract.OrganizationSource });
            if (exists == 0)
                errors.Add($"Organization '{contract.OrganizationExternalId}' not found");
        }

        if (errors.Any())
        {
            foreach (var error in errors)
            {
                Debug.WriteLine($"[ContractRepository] FK VALIDATION ERROR: {error}");
            }
        }

        var sql = @"
            UPDATE contracts SET
                external_id = @ExternalId,
                person_id = @PersonId,
                start_date = @StartDate,
                end_date = @EndDate,
                type_code = @TypeCode,
                type_description = @TypeDescription,
                fte = @Fte,
                hours_per_week = @HoursPerWeek,
                percentage = @Percentage,
                sequence = @Sequence,
                manager_person_external_id = @ManagerPersonExternalIdFixed,
                location_external_id = @LocationExternalId,
                cost_center_external_id = @CostCenterExternalId,
                cost_bearer_external_id = @CostBearerExternalId,
                employer_external_id = @EmployerExternalId,
                team_external_id = @TeamExternalId,
                department_external_id = @DepartmentExternalId,
                division_external_id = @DivisionExternalId,
                title_external_id = @TitleExternalId,
                organization_external_id = @OrganizationExternalId,
                location_source = @LocationSource,
                cost_center_source = @CostCenterSource,
                cost_bearer_source = @CostBearerSource,
                employer_source = @EmployerSource,
                team_source = @TeamSource,
                department_source = @DepartmentSource,
                division_source = @DivisionSource,
                title_source = @TitleSource,
                organization_source = @OrganizationSource
            WHERE contract_id = @ContractId";

        var parameters = new
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
            ManagerPersonExternalIdFixed = managerId,
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
        };

        var rowsAffected = await connection.ExecuteAsync(sql, parameters);
        Debug.WriteLine($"[ContractRepository] Update completed: {rowsAffected} rows affected");
        return rowsAffected;
    }

    public async Task<int> DeleteAsync(int contractId)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = "DELETE FROM contracts WHERE contract_id = @ContractId";

        return await connection.ExecuteAsync(sql, new { ContractId = contractId });
    }

    public async Task<int> GetCountAsync(ContractFilter filter)
    {
        using var connection = _connectionFactory.CreateConnection();

        var whereClauses = new List<string>();
        var parameters = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(filter.PersonId))
        {
            whereClauses.Add("person_id = @PersonId");
            parameters.Add("PersonId", filter.PersonId);
        }

        if (!string.IsNullOrWhiteSpace(filter.TypeCode))
        {
            whereClauses.Add("type_code = @TypeCode");
            parameters.Add("TypeCode", filter.TypeCode);
        }

        var whereClause = whereClauses.Count > 0 ? "WHERE " + string.Join(" AND ", whereClauses) : "";

        var sql = $"SELECT COUNT(*) FROM contracts {whereClause}";

        return await connection.ExecuteScalarAsync<int>(sql, parameters);
    }

    public async Task<ContractJsonDto?> GetJsonViewByIdAsync(int contractId)
    {
        using var connection = _connectionFactory.CreateConnection();

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
            WHERE contract_id = @ContractId";

        var result = await connection.QuerySingleOrDefaultAsync<dynamic>(sql, new { ContractId = contractId });

        if (result == null)
            return null;

        return new ContractJsonDto
        {
            Context = new Context { InConditions = false },
            ExternalId = result.ExternalId,
            StartDate = result.StartDate,
            EndDate = result.EndDate,
            Type = new HelloID.Vault.Core.Models.DTOs.ContractType
            {
                Code = result.TypeCode,
                Description = result.TypeDescription
            },
            Details = new HelloID.Vault.Core.Models.DTOs.ContractDetails
            {
                Fte = result.Fte,
                HoursPerWeek = result.HoursPerWeek,
                Percentage = result.Percentage,
                Sequence = result.Sequence != null ? (int?)result.Sequence : null
            },
            Location = new HelloID.Vault.Core.Models.DTOs.Location
            {
                ExternalId = result.LocationExternalId,
                Code = result.LocationCode,
                Name = result.LocationName
            },
            CostCenter = new HelloID.Vault.Core.Models.DTOs.CostCenter
            {
                ExternalId = result.CostCenterExternalId,
                Code = result.CostCenterCode,
                Name = result.CostCenterName
            },
            CostBearer = new HelloID.Vault.Core.Models.DTOs.CostBearer
            {
                ExternalId = result.CostBearerExternalId,
                Code = result.CostBearerCode,
                Name = result.CostBearerName
            },
            Employer = new HelloID.Vault.Core.Models.DTOs.Employer
            {
                ExternalId = result.EmployerExternalId,
                Code = result.EmployerCode,
                Name = result.EmployerName
            },
            Manager = new HelloID.Vault.Core.Models.DTOs.Manager
            {
                PersonId = result.ManagerPersonExternalId,
                ExternalId = result.ManagerExternalId,
                DisplayName = result.ManagerDisplayName,
                Email = result.ManagerEmail
            },
            Team = new HelloID.Vault.Core.Models.DTOs.Team
            {
                ExternalId = result.TeamExternalId,
                Code = result.TeamCode,
                Name = result.TeamName
            },
            Department = new ContractDepartment
            {
                ExternalId = result.DepartmentExternalId,
                DisplayName = result.DepartmentDisplayName
            },
            Division = new HelloID.Vault.Core.Models.DTOs.Division
            {
                ExternalId = result.DivisionExternalId,
                Code = result.DivisionCode,
                Name = result.DivisionName
            },
            Title = new HelloID.Vault.Core.Models.DTOs.Title
            {
                ExternalId = result.TitleExternalId,
                Code = result.TitleCode,
                Name = result.TitleName
            },
            Organization = new HelloID.Vault.Core.Models.DTOs.Organization
            {
                ExternalId = result.OrganizationExternalId,
                Code = result.OrganizationCode,
                Name = result.OrganizationName
            }
        };
    }

    public async Task<IEnumerable<ContractDetailDto>> GetAllDetailsAsync()
    {
        using var connection = _connectionFactory.CreateConnection();

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

        return await connection.QueryAsync<ContractDetailDto>(sql);
    }

    public async Task<IEnumerable<ContractDetailDto>> GetAllFromCacheAsync()
    {
        using var connection = _connectionFactory.CreateConnection();

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

        return await connection.QueryAsync<ContractDetailDto>(sql);
    }

    public async Task RebuildCacheAsync()
    {
        using var connection = _connectionFactory.CreateConnection();

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Delete existing cache data
        await connection.ExecuteAsync("DELETE FROM contract_details_cache");

        // Rebuild cache from view
        var insertSql = @"
            INSERT INTO contract_details_cache
            SELECT * FROM contract_details_view";

        await connection.ExecuteAsync(insertSql);

        // Update metadata
        var rowCount = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM contract_details_cache");

        stopwatch.Stop();

        var updateMetadataSql = @"
            UPDATE cache_metadata
            SET last_refreshed = @LastRefreshed,
                row_count = @RowCount,
                refresh_duration_ms = @RefreshDurationMs
            WHERE cache_name = @CacheName";

        await connection.ExecuteAsync(updateMetadataSql, new
        {
            CacheName = "contract_details_cache",
            LastRefreshed = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
            RowCount = rowCount,
            RefreshDurationMs = stopwatch.ElapsedMilliseconds
        });
    }

    public async Task RefreshContractCacheItemAsync(int contractId)
    {
        using var connection = _connectionFactory.CreateConnection();

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Incremental refresh: REPLACE single contract row
        var refreshSql = @"
            INSERT OR REPLACE INTO contract_details_cache
            SELECT * FROM contract_details_view WHERE contract_id = @ContractId";

        await connection.ExecuteAsync(refreshSql, new { ContractId = contractId });

        // Update metadata with incremental refresh duration
        var updateMetadataSql = @"
            UPDATE cache_metadata
            SET last_refreshed = @LastRefreshed,
                refresh_duration_ms = @RefreshDurationMs
            WHERE cache_name = @CacheName";

        stopwatch.Stop();

        await connection.ExecuteAsync(updateMetadataSql, new
        {
            CacheName = "contract_details_cache",
            LastRefreshed = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
            RefreshDurationMs = stopwatch.ElapsedMilliseconds
        });
    }

    public async Task<CacheMetadata> GetCacheMetadataAsync()
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            SELECT
                cache_name AS CacheName,
                last_refreshed AS LastRefreshed,
                row_count AS RowCount,
                refresh_duration_ms AS RefreshDurationMs
            FROM cache_metadata
            WHERE cache_name = @CacheName";

        var result = await connection.QuerySingleOrDefaultAsync<CacheMetadata>(sql, new { CacheName = "contract_details_cache" });

        return result ?? new CacheMetadata
        {
            CacheName = "contract_details_cache",
            LastRefreshed = DateTime.MinValue,
            RowCount = 0,
            RefreshDurationMs = 0
        };
    }
}
