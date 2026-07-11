using Bogus;
using HelloID.Vault.Core.Models.Json;
using HelloID.Vault.Services.Anonymization.Models;
using HelloID.Vault.Services.Anonymization.Utilities;

namespace HelloID.Vault.Services.Anonymization.Strategies.BusinessData;

public class DivisionAnonymizer
{
    private readonly Faker _faker;
    private readonly ReferenceMappingTable _mappings;

    public DivisionAnonymizer(Faker faker, ReferenceMappingTable mappings)
    {
        _faker = faker ?? throw new ArgumentNullException(nameof(faker));
        _mappings = mappings ?? throw new ArgumentNullException(nameof(mappings));
    }

    public void Anonymize(VaultReference? division, AnonymizationOptions options)
    {
        if (division == null) return;
        if (!options.AnonymizeDivisions) return;
        if (string.IsNullOrEmpty(division.ExternalId)) return;

        var originalExternalId = division.ExternalId;

        if (!_mappings.DivisionIds.ContainsKey(originalExternalId))
        {
            _mappings.DivisionIds[originalExternalId] =
                $"div-{Guid.NewGuid().ToString("N").Substring(0, 8)}";
        }

        if (!_mappings.DivisionNames.ContainsKey(originalExternalId))
        {
            string name;
            int attempts = 0;
            do
            {
                name = _faker.Company.CompanyName() + " Division";
                attempts++;
            } while (_mappings.UsedDivisionNames.Contains(name) && attempts < 100);

            _mappings.DivisionNames[originalExternalId] = name;
            _mappings.UsedDivisionNames.Add(name);
        }

        division.ExternalId = _mappings.DivisionIds[originalExternalId];
        division.Name = _mappings.DivisionNames[originalExternalId];

        if (!string.IsNullOrEmpty(division.Code))
        {
            division.Code = _faker.Company.CatchPhrase().Split(' ')[0].ToUpper();
        }
    }
}
