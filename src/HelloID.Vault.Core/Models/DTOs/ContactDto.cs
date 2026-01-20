namespace HelloID.Vault.Core.Models.DTOs;

/// <summary>
/// DTO for displaying contact information.
/// </summary>
public class ContactDto
{
    public int ContactId { get; set; }
    public string PersonId { get; set; } = string.Empty;
    public string? PersonDisplayName { get; set; }
    public string? Type { get; set; }
    public string? Email { get; set; }
    public string? PhoneMobile { get; set; }
    public string? PhoneFixed { get; set; }
    public string? AddressStreet { get; set; }
    public string? AddressStreetExt { get; set; }
    public string? AddressHouseNumber { get; set; }
    public string? AddressHouseNumberExt { get; set; }
    public string? AddressPostal { get; set; }
    public string? AddressLocality { get; set; }
    public string? AddressCountry { get; set; }
}
