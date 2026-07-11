using Bogus;
using HelloID.Vault.Core.Models.Json;
using HelloID.Vault.Services.Anonymization.Models;
using HelloID.Vault.Services.Anonymization.Utilities;

namespace HelloID.Vault.Services.Anonymization.Strategies.PersonalData;

/// <summary>
/// Anonymizes manager references in contracts and departments.
/// Ensures consistency: manager references use the same anonymized name as the person they reference.
/// </summary>
public class ManagerAnonymizer
{
    private readonly Faker _faker;
    private readonly ReferenceMappingTable _mappings;

    public ManagerAnonymizer(Faker faker, ReferenceMappingTable mappings)
    {
        _faker = faker ?? throw new ArgumentNullException(nameof(faker));
        _mappings = mappings ?? throw new ArgumentNullException(nameof(mappings));
    }

    /// <summary>
    /// Anonymizes a manager reference.
    /// Looks up the person by PersonId to use the same anonymized name.
    /// </summary>
    public void Anonymize(VaultManagerReference? manager, AnonymizationOptions options)
    {
        if (manager == null) return;

        // Check if this is an empty manager reference (GUID zeros)
        if (manager.PersonId == "00000000-0000-0000-0000-000000000000")
        {
            return; // Leave empty managers as-is
        }

        // Anonymize ExternalId using SAME mapping as persons
        if (options.AnonymizePersonExternalIds && !string.IsNullOrEmpty(manager.ExternalId))
        {
            if (!_mappings.PersonExternalIds.TryGetValue(manager.ExternalId, out var anonId))
            {
                // Manager might not be in persons list, create new mapping
                anonId = $"emp-{Guid.NewGuid().ToString("N").Substring(0, 8)}";
                _mappings.PersonExternalIds[manager.ExternalId] = anonId;
            }
            manager.ExternalId = anonId;
        }

        // Try to find the person's anonymized name by PersonId
        PersonNameMapping? nameMapping = null;
        
        if (!string.IsNullOrEmpty(manager.PersonId) && 
            _mappings.PersonIdToSourceNameKey.TryGetValue(manager.PersonId, out var sourceNameKey))
        {
            _mappings.PersonNameMappings.TryGetValue(sourceNameKey, out nameMapping);
        }

        // Anonymize DisplayName
        if (options.AnonymizeNames && !string.IsNullOrEmpty(manager.DisplayName))
        {
            if (nameMapping != null)
            {
                // Use the same anonymized name as the person
                manager.DisplayName = BuildManagerDisplayName(nameMapping, manager.ExternalId);
            }
            else
            {
                // Manager not in persons list, generate new name
                var givenName = _faker.Name.FirstName();
                var familyName = _faker.Name.LastName();
                
                // Ensure different names
                int attempts = 0;
                while (string.Equals(familyName, givenName, StringComparison.OrdinalIgnoreCase) && attempts < 20)
                {
                    familyName = _faker.Name.LastName();
                    attempts++;
                }
                
                manager.DisplayName = $"{givenName} {familyName} ({manager.ExternalId})";
            }
        }

        // Anonymize email
        if (options.AnonymizeEmails && !string.IsNullOrEmpty(manager.Email))
        {
            if (nameMapping != null)
            {
                // Use consistent email based on name
                manager.Email = _faker.Internet.Email(
                    nameMapping.GivenName, 
                    nameMapping.FamilyName
                );
            }
            else
            {
                manager.Email = _faker.Internet.Email();
            }
        }

        // PersonId is kept as-is (GUID, no personal info)
    }

    /// <summary>
    /// Builds manager DisplayName matching person format.
    /// </summary>
    private string BuildManagerDisplayName(PersonNameMapping mapping, string? externalId)
    {
        var familyName = FormatNameWithPrefix(mapping.FamilyNamePrefix, mapping.FamilyName);
        return $"{mapping.GivenName} {familyName} ({externalId})".Trim();
    }

    private static string FormatNameWithPrefix(string? prefix, string name)
    {
        if (string.IsNullOrEmpty(prefix))
        {
            return name;
        }
        return $"{prefix} {name}";
    }
}
