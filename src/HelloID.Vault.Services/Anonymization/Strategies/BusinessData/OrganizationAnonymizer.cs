using Bogus;
using HelloID.Vault.Core.Models.Json;
using HelloID.Vault.Services.Anonymization.Models;
using HelloID.Vault.Services.Anonymization.Utilities;

namespace HelloID.Vault.Services.Anonymization.Strategies.BusinessData;

public class OrganizationAnonymizer
{
    private readonly Faker _faker;
    private readonly ReferenceMappingTable _mappings;

    public OrganizationAnonymizer(Faker faker, ReferenceMappingTable mappings)
    {
        _faker = faker ?? throw new ArgumentNullException(nameof(faker));
        _mappings = mappings ?? throw new ArgumentNullException(nameof(mappings));
    }

    public void Anonymize(VaultReference? organization, AnonymizationOptions options)
    {
        if (organization == null) return;
        if (!options.AnonymizeOrganizations) return;
        if (string.IsNullOrEmpty(organization.ExternalId)) return;

        var originalExternalId = organization.ExternalId;

        if (!_mappings.OrganizationIds.ContainsKey(originalExternalId))
        {
            _mappings.OrganizationIds[originalExternalId] =
                $"org-{Guid.NewGuid().ToString("N").Substring(0, 8)}";
        }

        if (!_mappings.OrganizationNames.ContainsKey(originalExternalId))
        {
            string name;
            int attempts = 0;
            do
            {
                name = _faker.Company.CompanyName();
                attempts++;
            } while (_mappings.UsedOrganizationNames.Contains(name) && attempts < 100);

            _mappings.OrganizationNames[originalExternalId] = name;
            _mappings.UsedOrganizationNames.Add(name);
        }

        organization.ExternalId = _mappings.OrganizationIds[originalExternalId];
        organization.Name = _mappings.OrganizationNames[originalExternalId];

        if (!string.IsNullOrEmpty(organization.Code))
        {
            organization.Code = _faker.Company.CatchPhrase().Split(' ')[0].ToUpper();
        }
    }
}
