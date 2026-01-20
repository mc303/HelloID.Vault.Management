namespace HelloID.Vault.Core.Models.DTOs;

/// <summary>
/// DTO for displaying contract information.
/// </summary>
public class ContractDto
{
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

    // Related entity IDs
    public string? ManagerPersonExternalId { get; set; }
    public string? DepartmentExternalId { get; set; }
    public string? DepartmentManagerPersonId { get; set; }

    // Related entity names (joined from other tables)
    public string? ManagerName { get; set; }
    public string? LocationName { get; set; }
    public string? DepartmentName { get; set; }
    public string? TitleName { get; set; }

    // Source system
    public string? Source { get; set; }

    // Is this the primary contract?
    public bool IsPrimary { get; set; }
}
