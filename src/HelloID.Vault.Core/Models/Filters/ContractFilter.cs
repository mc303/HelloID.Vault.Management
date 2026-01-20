namespace HelloID.Vault.Core.Models.Filters;

/// <summary>
/// Filter criteria for querying contracts.
/// </summary>
public class ContractFilter
{
    public string? SearchTerm { get; set; }
    public string? PersonId { get; set; }
    public string? TypeCode { get; set; }
    public string? DepartmentExternalId { get; set; }
    public string? DepartmentSource { get; set; }
    public DateTime? StartDateFrom { get; set; }
    public DateTime? StartDateTo { get; set; }
}
