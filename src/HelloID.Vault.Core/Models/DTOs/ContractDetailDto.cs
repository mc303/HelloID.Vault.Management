namespace HelloID.Vault.Core.Models.DTOs;

/// <summary>
/// Complete DTO for displaying contract details with all related entities.
/// Maps to contract_details_view database view.
/// </summary>
public class ContractDetailDto
{
    // Contract fields
    public int ContractId { get; set; }
    public string? ExternalId { get; set; }
    public string PersonId { get; set; } = string.Empty;
    public string? StartDate { get; set; }
    public string? EndDate { get; set; }
    public string? TypeCode { get; set; }
    public string? TypeDescription { get; set; }
    public double? Fte { get; set; }
    public double? HoursPerWeek { get; set; }
    public double? Percentage { get; set; }
    public int? Sequence { get; set; }
    public string? Source { get; set; }
    public string? SourceDisplayName { get; set; }

    // Person
    public string? PersonName { get; set; }
    public string? PersonExternalId { get; set; }

    // Manager
    public string? ManagerPersonExternalId { get; set; }
    public string? ManagerPersonName { get; set; }

    // Location
    public string? LocationExternalId { get; set; }
    public string? LocationCode { get; set; }
    public string? LocationName { get; set; }

    // Cost center
    public string? CostCenterExternalId { get; set; }
    public string? CostCenterCode { get; set; }
    public string? CostCenterName { get; set; }

    // Cost bearer
    public string? CostBearerExternalId { get; set; }
    public string? CostBearerCode { get; set; }
    public string? CostBearerName { get; set; }

    // Employer
    public string? EmployerExternalId { get; set; }
    public string? EmployerCode { get; set; }
    public string? EmployerName { get; set; }

    // Team
    public string? TeamExternalId { get; set; }
    public string? TeamCode { get; set; }
    public string? TeamName { get; set; }

    // Department
    public string? DepartmentExternalId { get; set; }
    public string? DepartmentName { get; set; }
    public string? DepartmentCode { get; set; }
    public string? DepartmentParentExternalId { get; set; }
    public string? DepartmentManagerPersonId { get; set; }
    public string? DepartmentManagerName { get; set; }
    public string? DepartmentParentDepartmentName { get; set; }

    // Division
    public string? DivisionExternalId { get; set; }
    public string? DivisionCode { get; set; }
    public string? DivisionName { get; set; }

    // Title
    public string? TitleExternalId { get; set; }
    public string? TitleCode { get; set; }
    public string? TitleName { get; set; }

    // Organization
    public string? OrganizationExternalId { get; set; }
    public string? OrganizationCode { get; set; }
    public string? OrganizationName { get; set; }

    // Calculated fields
    public string ContractStatus { get; set; } = "No Dates";
    public string? ContractDateRange { get; set; }

    // Custom field values for this contract
    public List<CustomFieldDto> CustomFields { get; set; } = new();

    // Helper property to check if this is a primary contract
    public bool IsPrimary { get; set; }
}
