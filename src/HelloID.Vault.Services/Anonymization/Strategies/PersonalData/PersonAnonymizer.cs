using Bogus;
using HelloID.Vault.Core.Models.Json;
using HelloID.Vault.Services.Anonymization.Models;
using HelloID.Vault.Services.Anonymization.Utilities;

namespace HelloID.Vault.Services.Anonymization.Strategies.PersonalData;

public class PersonAnonymizer
{
    private readonly MultiLocaleFaker _multiLocaleFaker;
    private readonly Faker _faker;
    private readonly ReferenceMappingTable _mappings;
    private readonly EmailDomainGenerator _domainGenerator;
    private readonly NamePool _namePool;
    private int _externalIdCounter;
    private Queue<int>? _randomExternalIdQueue;
    private int _externalIdPadWidth;

    public PersonAnonymizer(
        MultiLocaleFaker multiLocaleFaker, 
        ReferenceMappingTable mappings, 
        EmailDomainGenerator domainGenerator,
        NamePool namePool)
    {
        _multiLocaleFaker = multiLocaleFaker ?? throw new ArgumentNullException(nameof(multiLocaleFaker));
        _faker = multiLocaleFaker.Primary;
        _mappings = mappings ?? throw new ArgumentNullException(nameof(mappings));
        _domainGenerator = domainGenerator ?? throw new ArgumentNullException(nameof(domainGenerator));
        _namePool = namePool ?? throw new ArgumentNullException(nameof(namePool));
    }

    /// <summary>
    /// Anonymizes a person's personal data.
    /// </summary>
    public void Anonymize(VaultPerson person, AnonymizationOptions options, string? primaryEmployerExternalId)
    {
        if (person == null) return;

        // Store PersonId to source name mapping for manager lookups
        var sourceNameKey = ReferenceMappingTable.CreateNameKey(
            person.Name?.GivenName, 
            person.Name?.FamilyName);
        
        if (!string.IsNullOrEmpty(person.PersonId))
        {
            _mappings.PersonIdToSourceNameKey[person.PersonId] = sourceNameKey;
        }

        // 1. Anonymize ExternalId FIRST (before other references)
        if (options.AnonymizePersonExternalIds && !string.IsNullOrEmpty(person.ExternalId))
        {
            if (!_mappings.PersonExternalIds.ContainsKey(person.ExternalId))
            {
                _mappings.PersonExternalIds[person.ExternalId] = GenerateExternalId(options);
            }
            person.ExternalId = _mappings.PersonExternalIds[person.ExternalId];
        }

        // 2. Anonymize names (mapped by source name for consistency)
        if (options.AnonymizeNames)
        {
            AnonymizePersonName(person, sourceNameKey);
        }

        // 3. Anonymize UserName
        if (options.AnonymizeUserNames && !string.IsNullOrEmpty(person.UserName))
        {
            person.UserName = _faker.Internet.UserName(
                person.Name?.GivenName,
                person.Name?.FamilyName
            );
        }

        // 4. Anonymize birth date
        if (options.AnonymizeBirthDates && person.Details?.BirthDate != null)
        {
            AnonymizeBirthDate(person.Details);
        }

        // 5. Anonymize contacts
        if (person.Contact != null &&
            (options.AnonymizeEmails || options.AnonymizePhones || options.AnonymizeAddresses))
        {
            var contactAnonymizer = new ContactAnonymizer(_faker, _domainGenerator, _mappings);
            contactAnonymizer.Anonymize(person.Contact, options, primaryEmployerExternalId, 
                person.Name?.GivenName, person.Name?.FamilyName);
        }
    }

    private void AnonymizePersonName(VaultPerson person, string sourceNameKey)
    {
        if (person.Name == null) return;

        if (!_mappings.PersonNameMappings.TryGetValue(sourceNameKey, out var nameMapping))
        {
            var fakerForPerson = _multiLocaleFaker.GetFakerForPerson();
            nameMapping = GenerateUniqueName(person.Name, fakerForPerson);
            _mappings.PersonNameMappings[sourceNameKey] = nameMapping;
        }

        person.Name.GivenName = nameMapping.GivenName;
        person.Name.FamilyName = nameMapping.FamilyName;
        person.Name.FamilyNamePartner = nameMapping.FamilyNamePartner;
        
        person.Name.Initials = nameMapping.GivenName[0].ToString();
        person.Name.NickName = nameMapping.GivenName;

        person.DisplayName = BuildDisplayName(person, nameMapping);
    }

    private PersonNameMapping GenerateUniqueName(VaultPersonName sourceName, Faker faker)
    {
        var (givenName, familyName) = _namePool.GetUniqueName();

        string? familyNamePartner = null;
        if (!string.IsNullOrEmpty(sourceName.FamilyNamePartner))
        {
            familyNamePartner = _namePool.GetUniquePartnerLastName(familyName);
        }

        return new PersonNameMapping
        {
            GivenName = givenName,
            FamilyName = familyName,
            FamilyNamePrefix = sourceName.FamilyNamePrefix,
            FamilyNamePartner = familyNamePartner,
            FamilyNamePartnerPrefix = sourceName.FamilyNamePartnerPrefix
        };
    }

    /// <summary>
    /// Builds DisplayName based on Convention field.
    /// Format: "{GivenName} {prefix?}{name} ({ExternalId})"
    /// </summary>
    private string BuildDisplayName(VaultPerson person, PersonNameMapping nameMapping)
    {
        var convention = person.Name?.Convention?.ToUpperInvariant();
        var externalId = person.ExternalId ?? "";
        
        var namePart = convention switch
        {
            "B" => BuildConventionB(nameMapping),
            "BP" => BuildConventionBP(nameMapping),
            "P" => BuildConventionP(nameMapping),
            "PB" => BuildConventionPB(nameMapping),
            _ => BuildConventionDefault(nameMapping)
        };

        return $"{namePart} ({externalId})";
    }

    /// <summary>
    /// Convention B: Use own family name only.
    /// Format: "{GivenName} {prefix?}{FamilyName}"
    /// </summary>
    private string BuildConventionB(PersonNameMapping mapping)
    {
        var familyName = FormatNameWithPrefix(mapping.FamilyNamePrefix, mapping.FamilyName);
        return $"{mapping.GivenName} {familyName}".Trim();
    }

    /// <summary>
    /// Convention BP: Own name first, then partner.
    /// Format: "{GivenName} {prefix?}{FamilyName} - {partnerPrefix?}{FamilyNamePartner}"
    /// </summary>
    private string BuildConventionBP(PersonNameMapping mapping)
    {
        var familyName = FormatNameWithPrefix(mapping.FamilyNamePrefix, mapping.FamilyName);
        
        if (string.IsNullOrEmpty(mapping.FamilyNamePartner))
        {
            return $"{mapping.GivenName} {familyName}".Trim();
        }

        var partnerName = FormatNameWithPrefix(mapping.FamilyNamePartnerPrefix, mapping.FamilyNamePartner);
        return $"{mapping.GivenName} {familyName} - {partnerName}".Trim();
    }

    /// <summary>
    /// Convention P: Use partner's family name only.
    /// Format: "{GivenName} {partnerPrefix?}{FamilyNamePartner}"
    /// </summary>
    private string BuildConventionP(PersonNameMapping mapping)
    {
        var familyName = string.IsNullOrEmpty(mapping.FamilyNamePartner)
            ? FormatNameWithPrefix(mapping.FamilyNamePrefix, mapping.FamilyName)
            : FormatNameWithPrefix(mapping.FamilyNamePartnerPrefix, mapping.FamilyNamePartner);
        
        return $"{mapping.GivenName} {familyName}".Trim();
    }

    /// <summary>
    /// Convention PB: Partner name first, then own.
    /// Format: "{GivenName} {partnerPrefix?}{FamilyNamePartner} - {prefix?}{FamilyName}"
    /// </summary>
    private string BuildConventionPB(PersonNameMapping mapping)
    {
        if (string.IsNullOrEmpty(mapping.FamilyNamePartner))
        {
            var familyName = FormatNameWithPrefix(mapping.FamilyNamePrefix, mapping.FamilyName);
            return $"{mapping.GivenName} {familyName}".Trim();
        }

        var partnerName = FormatNameWithPrefix(mapping.FamilyNamePartnerPrefix, mapping.FamilyNamePartner);
        var familyName2 = FormatNameWithPrefix(mapping.FamilyNamePrefix, mapping.FamilyName);
        return $"{mapping.GivenName} {partnerName} - {familyName2}".Trim();
    }

    /// <summary>
    /// Default: GivenName + FamilyName.
    /// Format: "{GivenName} {prefix?}{FamilyName}"
    /// </summary>
    private string BuildConventionDefault(PersonNameMapping mapping)
    {
        var familyName = FormatNameWithPrefix(mapping.FamilyNamePrefix, mapping.FamilyName);
        return $"{mapping.GivenName} {familyName}".Trim();
    }

    /// <summary>
    /// Formats a name with optional prefix (e.g., "van der Berg").
    /// </summary>
    private static string FormatNameWithPrefix(string? prefix, string name)
    {
        if (string.IsNullOrEmpty(prefix))
        {
            return name;
        }
        return $"{prefix} {name}";
    }

    private void AnonymizeBirthDate(VaultPersonDetails details)
    {
        if (details?.BirthDate == null) return;

        var originalDate = details.BirthDate.Value;
        var randomMonth = _faker.Random.Int(1, 12);
        var randomDay = _faker.Random.Int(1, DateTime.DaysInMonth(originalDate.Year, randomMonth));

        details.BirthDate = new DateTime(originalDate.Year, randomMonth, randomDay);
    }

    /// <summary>
    /// Generates an anonymized External ID based on options.
    /// Supports custom range (sequential or random) with optional padding.
    /// </summary>
    private string GenerateExternalId(AnonymizationOptions options)
    {
        if (!options.UseCustomExternalIdRange)
        {
            return $"emp-{Guid.NewGuid().ToString("N").Substring(0, 8)}";
        }

        // Initialize on first use
        if (_randomExternalIdQueue == null && options.UseRandomExternalIds)
        {
            var rng = new Random(options.Seed.GetHashCode());
            var numbers = Enumerable.Range(options.ExternalIdMin, options.ExternalIdMax - options.ExternalIdMin + 1).ToList();
            // Fisher-Yates shuffle
            for (int i = numbers.Count - 1; i > 0; i--)
            {
                var j = rng.Next(i + 1);
                (numbers[i], numbers[j]) = (numbers[j], numbers[i]);
            }
            _randomExternalIdQueue = new Queue<int>(numbers);
        }

        if (_externalIdPadWidth == 0)
        {
            _externalIdPadWidth = options.ExternalIdMax.ToString().Length;
        }

        int value;

        if (options.UseRandomExternalIds && _randomExternalIdQueue!.Count > 0)
        {
            value = _randomExternalIdQueue.Dequeue();
        }
        else
        {
            // Sequential (or random queue exhausted, fall back to sequential with wrap)
            value = _externalIdCounter;
            _externalIdCounter++;
            if (_externalIdCounter > options.ExternalIdMax)
                _externalIdCounter = options.ExternalIdMin;
            if (_externalIdCounter < options.ExternalIdMin)
                _externalIdCounter = options.ExternalIdMin;
        }

        return options.PadExternalId
            ? value.ToString($"D{_externalIdPadWidth}")
            : value.ToString();
    }
}
