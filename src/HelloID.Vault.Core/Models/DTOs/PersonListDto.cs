namespace HelloID.Vault.Core.Models.DTOs;

/// <summary>
/// Lightweight DTO for displaying persons in list views.
/// </summary>
public class PersonListDto
{
    public string PersonId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? ExternalId { get; set; }
    public bool Blocked { get; set; }
    public bool Excluded { get; set; }
    public string? PersonStatus { get; set; }
    public int ContractCount { get; set; }

    // Primary Manager fields
    public string? PrimaryManagerPersonId { get; set; }
    public string? PrimaryManagerName { get; set; }
    public string? PrimaryManagerSource { get; set; }

    public string? StatusBadge => GetStatusBadge();

    private string GetStatusBadge()
    {
        if (Blocked) return "Blocked";
        if (Excluded) return "Excluded";
        return PersonStatus ?? "Active";
    }
}
