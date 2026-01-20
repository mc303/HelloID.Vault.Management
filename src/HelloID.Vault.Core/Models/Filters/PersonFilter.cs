namespace HelloID.Vault.Core.Models.Filters;

/// <summary>
/// Filter criteria for querying persons.
/// </summary>
public class PersonFilter
{
    public string? SearchTerm { get; set; }
    public bool? Blocked { get; set; }
    public bool? Excluded { get; set; }
    public string? PersonStatus { get; set; }
    public string? DepartmentId { get; set; }
    public string? LocationId { get; set; }
}
