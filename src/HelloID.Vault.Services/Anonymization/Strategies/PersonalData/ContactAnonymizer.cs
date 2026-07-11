using Bogus;
using HelloID.Vault.Core.Models.Json;
using HelloID.Vault.Services.Anonymization.Models;
using HelloID.Vault.Services.Anonymization.Utilities;

namespace HelloID.Vault.Services.Anonymization.Strategies.PersonalData;

/// <summary>
/// Anonymizes contact information including emails, phones, and addresses.
/// </summary>
public class ContactAnonymizer
{
    private readonly Faker _faker;
    private readonly EmailDomainGenerator _domainGenerator;
    private readonly ReferenceMappingTable _mappings;

    public ContactAnonymizer(Faker faker, EmailDomainGenerator domainGenerator, ReferenceMappingTable mappings)
    {
        _faker = faker ?? throw new ArgumentNullException(nameof(faker));
        _domainGenerator = domainGenerator ?? throw new ArgumentNullException(nameof(domainGenerator));
        _mappings = mappings ?? throw new ArgumentNullException(nameof(mappings));
    }

    /// <summary>
    /// Anonymizes both business and personal contact information.
    /// </summary>
    public void Anonymize(VaultPersonContact contact, AnonymizationOptions options, string? employerExternalId, string? givenName, string? familyName)
    {
        if (contact == null) return;

        // Business contact
        if (options.AnonymizeEmails || options.AnonymizePhones || options.AnonymizeAddresses)
        {
            contact.Business ??= new VaultContactInfo();
            AnonymizeContactInfo(contact.Business, options, isBusiness: true, employerExternalId, givenName, familyName);
        }

        // Personal contact
        if (options.AnonymizeEmails || options.AnonymizePhones || options.AnonymizeAddresses)
        {
            contact.Personal ??= new VaultContactInfo();
            AnonymizeContactInfo(contact.Personal, options, isBusiness: false, employerExternalId: null, givenName: null, familyName: null);
        }
    }

    private void AnonymizeContactInfo(VaultContactInfo info, AnonymizationOptions options, bool isBusiness, string? employerExternalId, string? givenName, string? familyName)
    {
        if (info == null) return;

        // Email - only anonymize if email exists
        if (options.AnonymizeEmails && !string.IsNullOrEmpty(info.Email))
        {
            if (isBusiness && !string.IsNullOrEmpty(givenName) && !string.IsNullOrEmpty(familyName))
            {
                var domain = _domainGenerator.GetBusinessEmailDomain(employerExternalId, _mappings);
                var username = $"{givenName}.{familyName}".Replace(" ", "_").ToLower();
                info.Email = $"{username}@{domain}";
            }
            else
            {
                info.Email = _faker.Internet.Email();
            }
        }

        // Phone - only anonymize if phone fields exist
        if (options.AnonymizePhones && info.Phone != null)
        {
            if (!string.IsNullOrEmpty(info.Phone.Mobile))
            {
                info.Phone.Mobile = _faker.Phone.PhoneNumber();
            }
            if (!string.IsNullOrEmpty(info.Phone.Fixed))
            {
                info.Phone.Fixed = _faker.Phone.PhoneNumber();
            }
        }

        // Address - only anonymize if address has values
        if (options.AnonymizeAddresses && info.Address != null && HasAnyAddressValue(info.Address))
        {
            AnonymizeAddress(info.Address);
        }
    }

    private void AnonymizeAddress(VaultAddress address)
    {
        if (address == null) return;

        address.Street = _faker.Address.StreetName();
        address.HouseNumber = _faker.Address.BuildingNumber();
        address.PostalCode = _faker.Address.ZipCode();
        address.Locality = _faker.Address.City();
        address.Country = _faker.Address.Country();
    }

    private bool HasAnyAddressValue(VaultAddress address)
    {
        return !string.IsNullOrEmpty(address.Street) ||
               !string.IsNullOrEmpty(address.HouseNumber) ||
               !string.IsNullOrEmpty(address.PostalCode) ||
               !string.IsNullOrEmpty(address.Locality) ||
               !string.IsNullOrEmpty(address.Country);
    }
}
