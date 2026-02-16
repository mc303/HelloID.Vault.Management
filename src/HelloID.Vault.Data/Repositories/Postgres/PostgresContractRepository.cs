using Dapper;
using HelloID.Vault.Data.Connection;
using HelloID.Vault.Data.Repositories.Base;
using System.Data;

namespace HelloID.Vault.Data.Repositories.Postgres;

/// <summary>
/// PostgreSQL-specific implementation of ContractRepository.
/// Uses INSERT ... ON CONFLICT DO UPDATE SET for cache refresh operations.
/// </summary>
public class PostgresContractRepository : AbstractContractRepository
{
    public PostgresContractRepository(IDatabaseConnectionFactory connectionFactory)
        : base(connectionFactory)
    {
    }

    protected override async Task RefreshContractCacheItemInternalAsync(int contractId, IDbConnection connection)
    {
        // PostgreSQL: Use INSERT ... ON CONFLICT for upsert
        // Must explicitly list columns because the cache table schema must match the view
        var refreshSql = @"
            INSERT INTO contract_details_cache (
                contract_id, external_id, person_id, start_date, end_date,
                type_code, type_description, fte, hours_per_week, percentage,
                sequence, source, person_name, person_external_id,
                manager_person_external_id, manager_person_name,
                location_external_id, location_source, location_code, location_name,
                cost_center_external_id, cost_center_source, cost_center_code, cost_center_name,
                cost_bearer_external_id, cost_bearer_source, cost_bearer_code, cost_bearer_name,
                employer_external_id, employer_source, employer_code, employer_name,
                team_external_id, team_source, team_code, team_name,
                department_external_id, department_source, department_name, department_code,
                department_parent_external_id, department_manager_person_id, department_manager_name, department_parent_department_name,
                division_external_id, division_source, division_code, division_name,
                title_external_id, title_source, title_code, title_name,
                organization_external_id, organization_source, organization_code, organization_name,
                contract_status, contract_date_range, source_display_name
            )
            SELECT
                contract_id, external_id, person_id, start_date, end_date,
                type_code, type_description, fte, hours_per_week, percentage,
                sequence, source, person_name, person_external_id,
                manager_person_external_id, manager_person_name,
                location_external_id, location_source, location_code, location_name,
                cost_center_external_id, cost_center_source, cost_center_code, cost_center_name,
                cost_bearer_external_id, cost_bearer_source, cost_bearer_code, cost_bearer_name,
                employer_external_id, employer_source, employer_code, employer_name,
                team_external_id, team_source, team_code, team_name,
                department_external_id, department_source, department_name, department_code,
                department_parent_external_id, department_manager_person_id, department_manager_name, department_parent_department_name,
                division_external_id, division_source, division_code, division_name,
                title_external_id, title_source, title_code, title_name,
                organization_external_id, organization_source, organization_code, organization_name,
                contract_status, contract_date_range, source_display_name
            FROM contract_details_view
            WHERE contract_id = @ContractId
            ON CONFLICT (contract_id) DO UPDATE SET
                external_id = EXCLUDED.external_id,
                person_id = EXCLUDED.person_id,
                start_date = EXCLUDED.start_date,
                end_date = EXCLUDED.end_date,
                type_code = EXCLUDED.type_code,
                type_description = EXCLUDED.type_description,
                fte = EXCLUDED.fte,
                hours_per_week = EXCLUDED.hours_per_week,
                percentage = EXCLUDED.percentage,
                sequence = EXCLUDED.sequence,
                source = EXCLUDED.source,
                person_name = EXCLUDED.person_name,
                person_external_id = EXCLUDED.person_external_id,
                manager_person_external_id = EXCLUDED.manager_person_external_id,
                manager_person_name = EXCLUDED.manager_person_name,
                location_external_id = EXCLUDED.location_external_id,
                location_source = EXCLUDED.location_source,
                location_code = EXCLUDED.location_code,
                location_name = EXCLUDED.location_name,
                cost_center_external_id = EXCLUDED.cost_center_external_id,
                cost_center_source = EXCLUDED.cost_center_source,
                cost_center_code = EXCLUDED.cost_center_code,
                cost_center_name = EXCLUDED.cost_center_name,
                cost_bearer_external_id = EXCLUDED.cost_bearer_external_id,
                cost_bearer_source = EXCLUDED.cost_bearer_source,
                cost_bearer_code = EXCLUDED.cost_bearer_code,
                cost_bearer_name = EXCLUDED.cost_bearer_name,
                employer_external_id = EXCLUDED.employer_external_id,
                employer_source = EXCLUDED.employer_source,
                employer_code = EXCLUDED.employer_code,
                employer_name = EXCLUDED.employer_name,
                team_external_id = EXCLUDED.team_external_id,
                team_source = EXCLUDED.team_source,
                team_code = EXCLUDED.team_code,
                team_name = EXCLUDED.team_name,
                department_external_id = EXCLUDED.department_external_id,
                department_source = EXCLUDED.department_source,
                department_name = EXCLUDED.department_name,
                department_code = EXCLUDED.department_code,
                department_parent_external_id = EXCLUDED.department_parent_external_id,
                department_manager_person_id = EXCLUDED.department_manager_person_id,
                department_manager_name = EXCLUDED.department_manager_name,
                department_parent_department_name = EXCLUDED.department_parent_department_name,
                division_external_id = EXCLUDED.division_external_id,
                division_source = EXCLUDED.division_source,
                division_code = EXCLUDED.division_code,
                division_name = EXCLUDED.division_name,
                title_external_id = EXCLUDED.title_external_id,
                title_source = EXCLUDED.title_source,
                title_code = EXCLUDED.title_code,
                title_name = EXCLUDED.title_name,
                organization_external_id = EXCLUDED.organization_external_id,
                organization_source = EXCLUDED.organization_source,
                organization_code = EXCLUDED.organization_code,
                organization_name = EXCLUDED.organization_name,
                contract_status = EXCLUDED.contract_status,
                contract_date_range = EXCLUDED.contract_date_range,
                source_display_name = EXCLUDED.source_display_name";

        await connection.ExecuteAsync(refreshSql, new { ContractId = contractId }).ConfigureAwait(false);
    }
}
