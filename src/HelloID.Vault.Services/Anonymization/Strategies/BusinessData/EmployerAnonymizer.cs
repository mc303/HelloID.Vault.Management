using Bogus;
using HelloID.Vault.Core.Models.Json;
using HelloID.Vault.Services.Anonymization.Models;
using HelloID.Vault.Services.Anonymization.Utilities;

namespace HelloID.Vault.Services.Anonymization.Strategies.BusinessData;

public class EmployerAnonymizer
{
    private readonly Faker _faker;
    private readonly ReferenceMappingTable _mappings;
    private readonly EmailDomainGenerator _domainGenerator;

    public EmployerAnonymizer(Faker faker, ReferenceMappingTable mappings, EmailDomainGenerator domainGenerator)
    {
        _faker = faker ?? throw new ArgumentNullException(nameof(faker));
        _mappings = mappings ?? throw new ArgumentNullException(nameof(mappings));
        _domainGenerator = domainGenerator ?? throw new ArgumentNullException(nameof(domainGenerator));
    }

    public void Anonymize(VaultReference? employer, AnonymizationOptions options)
    {
        if (employer == null) return;

        if (!options.AnonymizeEmployers) return;

        if (string.IsNullOrEmpty(employer.ExternalId)) return;

        var originalExternalId = employer.ExternalId;

        if (!_mappings.EmployerIds.ContainsKey(originalExternalId))
        {
            _mappings.EmployerIds[originalExternalId] =
                $"emp-{Guid.NewGuid().ToString("N").Substring(0, 8)}";
        }

        if (!_mappings.EmployerNames.ContainsKey(originalExternalId))
        {
            string name;
            int attempts = 0;
            do
            {
                name = _faker.Company.CompanyName();
                attempts++;
            } while (_mappings.UsedEmployerNames.Contains(name) && attempts < 100);

            _mappings.EmployerNames[originalExternalId] = name;
            _mappings.UsedEmployerNames.Add(name);
        }

        if (options.UseMultiEmployerDomains)
        {
            _domainGenerator.GetBusinessEmailDomain(originalExternalId, _mappings);
        }

        employer.ExternalId = _mappings.EmployerIds[originalExternalId];
        employer.Name = _mappings.EmployerNames[originalExternalId];

        if (!string.IsNullOrEmpty(employer.Code))
        {
            employer.Code = _faker.Company.CatchPhrase().Split(' ')[0].ToUpper();
        }
    }
}
