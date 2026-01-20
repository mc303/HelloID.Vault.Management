using HelloID.Vault.Core.Models.Entities;
using HelloID.Vault.Core.Models.DTOs;
using HelloID.Vault.Data.Repositories.Interfaces;

namespace HelloID.Vault.Services;

/// <summary>
/// Service for resolving field values from contracts based on primary contract configuration.
/// Handles both core fields and custom fields dynamically.
/// </summary>
public class PrimaryContractFieldResolver
{
    private readonly ICustomFieldRepository _customFieldRepository;

    public PrimaryContractFieldResolver(ICustomFieldRepository customFieldRepository)
    {
        _customFieldRepository = customFieldRepository ?? throw new ArgumentNullException(nameof(customFieldRepository));
    }

    /// <summary>
    /// Gets a comparable value for a contract based on field name from configuration.
/// Handles both core database fields and custom fields.
    /// </summary>
    /// <param name="contract">The contract to get value from</param>
    /// <param name="fieldName">The field name from configuration (e.g., "fte", "hours_per_week", or custom field key)</param>
    /// <returns>A comparable value for ordering</returns>
    public IComparable GetFieldValue(ContractDetailDto contract, string fieldName)
    {
        // Handle core database fields
        return fieldName.ToLowerInvariant() switch
        {
            // Contract basic fields
            "fte" => contract.Fte ?? 0.0,
            "hours_per_week" => contract.HoursPerWeek ?? 0.0,
            "percentage" => contract.Percentage ?? 0.0,
            "sequence" => contract.Sequence ?? 0,
            "start_date" => contract.StartDate ?? "",
            "end_date" => contract.EndDate ?? "",
            "contract_id" => contract.ContractId,
            "external_id" => contract.ExternalId ?? "",
            "type_code" => contract.TypeCode ?? "",
            "type_description" => contract.TypeDescription ?? "",

            // Person fields
            "person_name" => contract.PersonName ?? "",
            "person_external_id" => contract.PersonExternalId ?? "",

            // Manager fields
            "manager_person_id" => contract.ManagerPersonExternalId ?? "",
            "manager_person_name" => contract.ManagerPersonName ?? "",
            "manager_external_id" => contract.ManagerPersonExternalId ?? "",

            // Location fields
            "location_id" => contract.LocationExternalId ?? "",
            "location_external_id" => contract.LocationExternalId ?? "",
            "location_code" => contract.LocationCode ?? "",
            "location_name" => contract.LocationName ?? "",

            // Cost Center fields
            "cost_center_id" => contract.CostCenterExternalId ?? "",
            "cost_center_external_id" => contract.CostCenterExternalId ?? "",
            "cost_center_code" => contract.CostCenterCode ?? "",
            "cost_center_name" => contract.CostCenterName ?? "",

            // Cost Bearer fields
            "cost_bearer_id" => contract.CostBearerExternalId ?? "",
            "cost_bearer_external_id" => contract.CostBearerExternalId ?? "",
            "cost_bearer_code" => contract.CostBearerCode ?? "",
            "cost_bearer_name" => contract.CostBearerName ?? "",

            // Employer fields
            "employer_id" => contract.EmployerExternalId ?? "",
            "employer_external_id" => contract.EmployerExternalId ?? "",
            "employer_code" => contract.EmployerCode ?? "",
            "employer_name" => contract.EmployerName ?? "",

            // Team fields
            "team_id" => contract.TeamExternalId ?? "",
            "team_external_id" => contract.TeamExternalId ?? "",
            "team_code" => contract.TeamCode ?? "",
            "team_name" => contract.TeamName ?? "",

            // Department fields
            "department_id" => contract.DepartmentExternalId ?? "",
            "department_external_id" => contract.DepartmentExternalId ?? "",
            "department_name" => contract.DepartmentName ?? "",
            "department_code" => contract.DepartmentCode ?? "",
            "department_manager_person_id" => contract.DepartmentManagerPersonId ?? "",
            "department_manager_name" => contract.DepartmentManagerName ?? "",
            "department_parent_external_id" => contract.DepartmentParentExternalId ?? "",
            "department_parent_department_name" => contract.DepartmentParentDepartmentName ?? "",

            // Division fields
            "division_id" => contract.DivisionExternalId ?? "",
            "division_external_id" => contract.DivisionExternalId ?? "",
            "division_code" => contract.DivisionCode ?? "",
            "division_name" => contract.DivisionName ?? "",

            // Title fields
            "title_id" => contract.TitleExternalId ?? "",
            "title_external_id" => contract.TitleExternalId ?? "",
            "title_code" => contract.TitleCode ?? "",
            "title_name" => contract.TitleName ?? "",

            // Organization fields
            "organization_id" => contract.OrganizationExternalId ?? "",
            "organization_external_id" => contract.OrganizationExternalId ?? "",
            "organization_code" => contract.OrganizationCode ?? "",
            "organization_name" => contract.OrganizationName ?? "",

            // Contract status and derived fields
            "contract_status" => contract.ContractStatus ?? "",
            "contract_date_range" => contract.ContractDateRange ?? "",

            _ => GetCustomFieldValue(contract, fieldName)
        };
    }

    /// <summary>
    /// Gets value for custom fields from the contract's custom field collection.
    /// </summary>
    /// <param name="contract">The contract to get custom field value from</param>
    /// <param name="fieldKey">The custom field key</param>
    /// <returns>A comparable value for ordering</returns>
    private IComparable GetCustomFieldValue(ContractDetailDto contract, string fieldKey)
    {
        if (contract.CustomFields == null || !contract.CustomFields.Any())
            return string.Empty;

        var customField = contract.CustomFields.FirstOrDefault(cf =>
            string.Equals(cf.FieldKey, fieldKey, StringComparison.OrdinalIgnoreCase));

        if (customField == null)
            return string.Empty;

        // Try to parse as number for proper ordering
        if (double.TryParse(customField.Value, out double numericValue))
            return numericValue;

        // Return as string for other types
        return customField.Value ?? string.Empty;
    }

    /// <summary>
    /// Checks if a field name is a core database field.
    /// </summary>
    /// <param name="fieldName">The field name to check</param>
    /// <returns>True if it's a core field, false if it's a custom field</returns>
    public bool IsCoreField(string fieldName)
    {
        var coreFields = new HashSet<string>
        {
            "fte", "hours_per_week", "percentage", "sequence", "start_date", "end_date",
            "contract_id", "external_id", "type_code", "type_description",
            "manager_person_id", "manager_person_external_id",
            "location_id", "location_external_id", "location_name",
            "cost_center_id", "cost_center_external_id", "cost_center_name",
            "cost_bearer_id", "cost_bearer_external_id", "cost_bearer_name",
            "employer_id", "employer_external_id", "employer_name",
            "team_id", "team_external_id", "team_name",
            "department_id", "department_external_id", "department_name",
            "division_id", "division_external_id", "division_name",
            "title_id", "title_external_id", "title_name",
            "organization_id", "organization_external_id", "organization_name"
        };

        return coreFields.Contains(fieldName.ToLowerInvariant());
    }

    /// <summary>
    /// Gets the display name for a field name.
    /// </summary>
    /// <param name="fieldName">The field name</param>
    /// <returns>Human-readable display name</returns>
    public string GetDisplayName(string fieldName)
    {
        return fieldName.ToLowerInvariant() switch
        {
            "fte" => "FTE",
            "hours_per_week" => "Hours Per Week",
            "percentage" => "Percentage",
            "sequence" => "Sequence",
            "start_date" => "Start Date",
            "end_date" => "End Date",
            "contract_id" => "Contract ID",
            "external_id" => "External ID",
            "type_code" => "Type Code",
            "type_description" => "Type Description",
            "manager_person_id" => "Manager Person ID",
            "manager_person_external_id" => "Manager Person External ID",
            "location_id" => "Location ID",
            "location_external_id" => "Location External ID",
            "location_name" => "Location Name",
            "cost_center_id" => "Cost Center ID",
            "cost_center_external_id" => "Cost Center External ID",
            "cost_center_name" => "Cost Center Name",
            "cost_bearer_id" => "Cost Bearer ID",
            "cost_bearer_external_id" => "Cost Bearer External ID",
            "cost_bearer_name" => "Cost Bearer Name",
            "employer_id" => "Employer ID",
            "employer_external_id" => "Employer External ID",
            "employer_name" => "Employer Name",
            "team_id" => "Team ID",
            "team_external_id" => "Team External ID",
            "team_name" => "Team Name",
            "department_id" => "Department ID",
            "department_external_id" => "Department External ID",
            "department_name" => "Department Name",
            "division_id" => "Division ID",
            "division_external_id" => "Division External ID",
            "division_name" => "Division Name",
            "title_id" => "Title ID",
            "title_external_id" => "Title External ID",
            "title_name" => "Title Name",
            "organization_id" => "Organization ID",
            "organization_external_id" => "Organization External ID",
            "organization_name" => "Organization Name",
            _ => fieldName // For custom fields, use the field key as display name
        };
    }
}