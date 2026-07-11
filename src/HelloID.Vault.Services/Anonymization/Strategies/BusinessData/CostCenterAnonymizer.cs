using Bogus;
using HelloID.Vault.Core.Models.Json;
using HelloID.Vault.Services.Anonymization.Models;
using HelloID.Vault.Services.Anonymization.Utilities;

namespace HelloID.Vault.Services.Anonymization.Strategies.BusinessData;

public class CostCenterAnonymizer
{
    private readonly Faker _faker;
    private readonly ReferenceMappingTable _mappings;

    public CostCenterAnonymizer(Faker faker, ReferenceMappingTable mappings)
    {
        _faker = faker ?? throw new ArgumentNullException(nameof(faker));
        _mappings = mappings ?? throw new ArgumentNullException(nameof(mappings));
    }

    public void Anonymize(VaultReference? costCenter, AnonymizationOptions options)
    {
        if (costCenter == null) return;
        if (!options.AnonymizeCostCenters) return;
        if (string.IsNullOrEmpty(costCenter.ExternalId)) return;

        var originalExternalId = costCenter.ExternalId;

        if (!_mappings.CostCenterIds.ContainsKey(originalExternalId))
        {
            _mappings.CostCenterIds[originalExternalId] =
                $"cc-{Guid.NewGuid().ToString("N").Substring(0, 8)}";
        }

        if (!_mappings.CostCenterNames.ContainsKey(originalExternalId))
        {
            string name;
            int attempts = 0;
            do
            {
                name = _faker.Commerce.Department();
                attempts++;
            } while (_mappings.UsedCostCenterNames.Contains(name) && attempts < 100);

            _mappings.CostCenterNames[originalExternalId] = name;
            _mappings.UsedCostCenterNames.Add(name);
        }

        costCenter.ExternalId = _mappings.CostCenterIds[originalExternalId];
        costCenter.Name = _mappings.CostCenterNames[originalExternalId];

        if (!string.IsNullOrEmpty(costCenter.Code))
        {
            costCenter.Code = _faker.Commerce.Categories(1)[0].ToUpper().Substring(0, 4);
        }
    }
}
