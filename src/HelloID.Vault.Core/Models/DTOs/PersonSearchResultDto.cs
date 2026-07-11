namespace HelloID.Vault.Core.Models.DTOs;

/// <summary>
/// Lightweight DTO for person search results (e.g. manager dropdown).
/// </summary>
public class PersonSearchResultDto
{
    public string PersonId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? ExternalId { get; set; }
    public string? UserName { get; set; }

    public override string ToString() => DisplayName;
}
