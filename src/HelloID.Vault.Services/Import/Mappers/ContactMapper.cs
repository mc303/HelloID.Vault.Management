using HelloID.Vault.Core.Models.Entities;
using HelloID.Vault.Core.Models.Json;

namespace HelloID.Vault.Services.Import.Mappers;

/// <summary>
/// Maps VaultContactInfo objects to Contact entities during import.
/// </summary>
public static class ContactMapper
{
    /// <summary>
    /// Maps a VaultContactInfo to a Contact entity for a specific person.
    /// </summary>
    public static Contact Map(string personId, string type, VaultContactInfo contactInfo)
    {
        return new Contact
        {
            PersonId = personId,
            Type = type,
            Email = contactInfo.Email,
            PhoneMobile = contactInfo.Phone?.Mobile,
            PhoneFixed = contactInfo.Phone?.Fixed,
            AddressStreet = contactInfo.Address?.Street,
            AddressHouseNumber = contactInfo.Address?.HouseNumber,
            AddressPostal = contactInfo.Address?.PostalCode,
            AddressLocality = contactInfo.Address?.Locality,
            AddressCountry = contactInfo.Address?.Country
        };
    }

    /// <summary>
    /// Checks if a contact contains any actual data.
    /// Returns true if ALL fields are null or empty (contact should be skipped).
    /// Returns false if at least ONE field has data (contact should be inserted).
    /// </summary>
    public static bool IsEmpty(VaultContactInfo contactInfo)
    {
        // Contact is considered empty if ALL fields are null or empty
        return string.IsNullOrWhiteSpace(contactInfo.Email) &&
               string.IsNullOrWhiteSpace(contactInfo.Phone?.Mobile) &&
               string.IsNullOrWhiteSpace(contactInfo.Phone?.Fixed) &&
               string.IsNullOrWhiteSpace(contactInfo.Address?.Street) &&
               string.IsNullOrWhiteSpace(contactInfo.Address?.HouseNumber) &&
               string.IsNullOrWhiteSpace(contactInfo.Address?.PostalCode) &&
               string.IsNullOrWhiteSpace(contactInfo.Address?.Locality) &&
               string.IsNullOrWhiteSpace(contactInfo.Address?.Country);
    }
}
