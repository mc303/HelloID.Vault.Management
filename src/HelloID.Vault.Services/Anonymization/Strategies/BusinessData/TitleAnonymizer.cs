using Bogus;
using HelloID.Vault.Core.Models.Json;
using HelloID.Vault.Services.Anonymization.Models;
using HelloID.Vault.Services.Anonymization.Utilities;

namespace HelloID.Vault.Services.Anonymization.Strategies.BusinessData;

public class TitleAnonymizer
{
    private readonly Faker _faker;
    private readonly ReferenceMappingTable _mappings;

    public TitleAnonymizer(Faker faker, ReferenceMappingTable mappings)
    {
        _faker = faker ?? throw new ArgumentNullException(nameof(faker));
        _mappings = mappings ?? throw new ArgumentNullException(nameof(mappings));
    }

    public void Anonymize(VaultReference? title, AnonymizationOptions options)
    {
        if (title == null) return;
        if (!options.AnonymizeTitles) return;
        if (string.IsNullOrEmpty(title.ExternalId)) return;

        var originalExternalId = title.ExternalId;

        if (!_mappings.TitleIds.ContainsKey(originalExternalId))
        {
            _mappings.TitleIds[originalExternalId] =
                $"title-{Guid.NewGuid().ToString("N").Substring(0, 8)}";
        }

        if (!_mappings.TitleNames.ContainsKey(originalExternalId))
        {
            string name;
            int attempts = 0;
            do
            {
                name = _faker.Name.JobTitle();
                attempts++;
            } while (_mappings.UsedTitleNames.Contains(name) && attempts < 100);

            _mappings.TitleNames[originalExternalId] = name;
            _mappings.UsedTitleNames.Add(name);
        }

        title.ExternalId = _mappings.TitleIds[originalExternalId];
        title.Name = _mappings.TitleNames[originalExternalId];

        if (!string.IsNullOrEmpty(title.Code))
        {
            title.Code = _faker.Name.JobDescriptor().ToUpper();
        }
    }
}
