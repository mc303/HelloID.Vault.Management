using Bogus;
using HelloID.Vault.Services.Anonymization.Models;

namespace HelloID.Vault.Services.Anonymization.Utilities;

/// <summary>
/// Generates consistent email domains for business emails.
/// Supports multi-employer scenarios with different domains per employer.
/// </summary>
public class EmailDomainGenerator
{
    private readonly Faker _faker;
    private readonly AnonymizationOptions _options;
    private string? _fallbackDomain;

    public EmailDomainGenerator(Faker faker, AnonymizationOptions options)
    {
        _faker = faker ?? throw new ArgumentNullException(nameof(faker));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Gets or creates business email domain for a specific employer.
    /// Each employer gets a unique domain (multi-employer mode).
    /// </summary>
    public string GetBusinessEmailDomain(string? employerExternalId, ReferenceMappingTable mappings)
    {
        // Use custom domain if provided (overrides all)
        if (!string.IsNullOrEmpty(_options.CustomBusinessEmailDomain))
        {
            return _options.CustomBusinessEmailDomain;
        }

        // Multi-employer mode: different domain per employer
        if (_options.UseMultiEmployerDomains && !string.IsNullOrEmpty(employerExternalId))
        {
            if (!mappings.EmployerEmailDomains.TryGetValue(employerExternalId, out var domain))
            {
                domain = GenerateFakeCompanyDomain();
                mappings.EmployerEmailDomains[employerExternalId] = domain;
            }
            return domain;
        }

        // Fallback: single domain for all
        return GetFallbackDomain();
    }

    /// <summary>
    /// Gets fallback domain (for persons without an employer).
    /// </summary>
    public string GetFallbackDomain()
    {
        if (!string.IsNullOrEmpty(_options.CustomBusinessEmailDomain))
        {
            return _options.CustomBusinessEmailDomain;
        }

        if (_fallbackDomain == null)
        {
            _fallbackDomain = GenerateFakeCompanyDomain();
        }

        return _fallbackDomain;
    }

    /// <summary>
    /// Gets personal email domain (can vary).
    /// </summary>
    public string GetPersonalEmailDomain()
    {
        return _faker.Internet.DomainName();
    }

    private string GenerateFakeCompanyDomain()
    {
        // Generate realistic company domain
        // Dutch: "bedrijf.nl", "organisatie.com"
        // English: "techcorp.com", "company.org"
        
        var companyName = _faker.Company.CompanyName()
            .Replace(" ", "")
            .Replace(",", "")
            .Replace(".", "")
            .ToLower();

        // Limit length to avoid unrealistic domains
        if (companyName.Length > 20)
        {
            companyName = companyName.Substring(0, 20);
        }

        var tld = _faker.PickRandom(new[] { "com", "nl", "org", "net", "io" });

        return $"{companyName}.{tld}";
    }
}
