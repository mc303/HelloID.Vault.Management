using Bogus;
using HelloID.Vault.Core.Models.Json;
using HelloID.Vault.Services.Anonymization.Models;
using HelloID.Vault.Services.Anonymization.Utilities;

namespace HelloID.Vault.Services.Anonymization.Strategies.BusinessData;

public class CostBearerAnonymizer
{
    private readonly Faker _faker;
    private readonly ReferenceMappingTable _mappings;

    public CostBearerAnonymizer(Faker faker, ReferenceMappingTable mappings)
    {
        _faker = faker ?? throw new ArgumentNullException(nameof(faker));
        _mappings = mappings ?? throw new ArgumentNullException(nameof(mappings));
    }

    public void Anonymize(VaultReference? costBearer, AnonymizationOptions options)
    {
        if (costBearer == null) return;
        if (!options.AnonymizeCostBearers) return;
        if (string.IsNullOrEmpty(costBearer.ExternalId)) return;

        var originalExternalId = costBearer.ExternalId;

        if (!_mappings.CostBearerIds.ContainsKey(originalExternalId))
        {
            _mappings.CostBearerIds[originalExternalId] =
                $"cb-{Guid.NewGuid().ToString("N").Substring(0, 8)}";
        }

        if (!_mappings.CostBearerNames.ContainsKey(originalExternalId))
        {
            string name;
            int attempts = 0;
            do
            {
                name = _faker.Company.CompanyName();
                attempts++;
            } while (_mappings.UsedCostBearerNames.Contains(name) && attempts < 100);

            _mappings.CostBearerNames[originalExternalId] = name;
            _mappings.UsedCostBearerNames.Add(name);
        }

        costBearer.ExternalId = _mappings.CostBearerIds[originalExternalId];
        costBearer.Name = _mappings.CostBearerNames[originalExternalId];

        if (!string.IsNullOrEmpty(costBearer.Code))
        {
            costBearer.Code = _faker.Company.CatchPhrase().Split(' ')[0].ToUpper();
        }
    }
}
