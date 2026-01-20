namespace HelloID.Vault.Core.Models.Entities;

/// <summary>
/// Represents an employment contract.
/// </summary>
public class Contract
{
    public int ContractId { get; set; }
    public string? ExternalId { get; set; }
    public string PersonId { get; set; } = string.Empty;
    public string? StartDate { get; set; } // ISO 8601 format
    public string? EndDate { get; set; } // ISO 8601 format
    public string? TypeCode { get; set; }
    public string? TypeDescription { get; set; }
    public double? Fte { get; set; }
    public double? HoursPerWeek { get; set; }
    public double? Percentage { get; set; }
    public int? Sequence { get; set; }
    public string? ManagerPersonExternalId { get; set; }
    public string? LocationExternalId { get; set; }
    public string? CostCenterExternalId { get; set; }
    public string? CostBearerExternalId { get; set; }
    public string? EmployerExternalId { get; set; }
    public string? TeamExternalId { get; set; }
    public string? DepartmentExternalId { get; set; }
    public string? DivisionExternalId { get; set; }
    public string? TitleExternalId { get; set; }
    public string? OrganizationExternalId { get; set; }
    public string? Source { get; set; }
    public string? LocationSource { get; set; }
    public string? CostCenterSource { get; set; }
    public string? CostBearerSource { get; set; }
    public string? EmployerSource { get; set; }
    public string? TeamSource { get; set; }
    public string? DepartmentSource { get; set; }
    public string? DivisionSource { get; set; }
    public string? TitleSource { get; set; }
    public string? OrganizationSource { get; set; }
}
