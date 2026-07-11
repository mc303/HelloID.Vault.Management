using Bogus;
using HelloID.Vault.Core.Models.Json;
using HelloID.Vault.Services.Anonymization.Models;
using HelloID.Vault.Services.Anonymization.Utilities;

namespace HelloID.Vault.Services.Anonymization.Strategies.BusinessData;

public class TeamAnonymizer
{
    private readonly Faker _faker;
    private readonly ReferenceMappingTable _mappings;

    public TeamAnonymizer(Faker faker, ReferenceMappingTable mappings)
    {
        _faker = faker ?? throw new ArgumentNullException(nameof(faker));
        _mappings = mappings ?? throw new ArgumentNullException(nameof(mappings));
    }

    public void Anonymize(VaultReference? team, AnonymizationOptions options)
    {
        if (team == null) return;
        if (!options.AnonymizeTeams) return;
        if (string.IsNullOrEmpty(team.ExternalId)) return;

        var originalExternalId = team.ExternalId;

        if (!_mappings.TeamIds.ContainsKey(originalExternalId))
        {
            _mappings.TeamIds[originalExternalId] =
                $"team-{Guid.NewGuid().ToString("N").Substring(0, 8)}";
        }

        if (!_mappings.TeamNames.ContainsKey(originalExternalId))
        {
            string name;
            int attempts = 0;
            do
            {
                name = _faker.Commerce.Department() + " Team";
                attempts++;
            } while (_mappings.UsedTeamNames.Contains(name) && attempts < 100);

            _mappings.TeamNames[originalExternalId] = name;
            _mappings.UsedTeamNames.Add(name);
        }

        team.ExternalId = _mappings.TeamIds[originalExternalId];
        team.Name = _mappings.TeamNames[originalExternalId];

        if (!string.IsNullOrEmpty(team.Code))
        {
            team.Code = _faker.Commerce.Categories(1)[0].ToUpper().Substring(0, 4);
        }
    }
}
