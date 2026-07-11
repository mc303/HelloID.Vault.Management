using System.Diagnostics;
using Bogus;
using HelloID.Vault.Core.Models.Json;
using HelloID.Vault.Services.Anonymization.Models;
using HelloID.Vault.Services.Anonymization.Utilities;

namespace HelloID.Vault.Services.Anonymization.Strategies.BusinessData;

public class LocationAnonymizer
{
    private readonly Faker _faker;
    private readonly ReferenceMappingTable _mappings;

    public LocationAnonymizer(Faker faker, ReferenceMappingTable mappings)
    {
        _faker = faker ?? throw new ArgumentNullException(nameof(faker));
        _mappings = mappings ?? throw new ArgumentNullException(nameof(mappings));
    }

    public void Anonymize(VaultReference? location, AnonymizationOptions options)
    {
        if (location == null)
        {
            if (options.VerboseLogging)
                Debug.WriteLine("[LocationAnonymizer] location is NULL, skipping");
            return;
        }

        if (options.VerboseLogging)
            Debug.WriteLine($"[LocationAnonymizer] Called with ExternalId='{location.ExternalId}', Name='{location.Name}'");

        if (!options.AnonymizeLocations)
        {
            if (options.VerboseLogging)
                Debug.WriteLine("[LocationAnonymizer] AnonymizeLocations is FALSE, skipping");
            return;
        }

        if (string.IsNullOrEmpty(location.ExternalId) && string.IsNullOrEmpty(location.Name))
        {
            if (options.VerboseLogging)
                Debug.WriteLine("[LocationAnonymizer] Both ExternalId and Name are null/empty, skipping");
            return;
        }

        var originalExternalId = location.ExternalId;
        var originalName = location.Name;

        if (string.IsNullOrEmpty(originalExternalId))
        {
            originalExternalId = originalName ?? string.Empty;
            if (options.VerboseLogging)
                Debug.WriteLine($"[LocationAnonymizer] Using Name as key: '{originalExternalId}'");
        }

        if (options.VerboseLogging)
            Debug.WriteLine($"[LocationAnonymizer] Processing location with key='{originalExternalId}'");

        if (!_mappings.LocationIds.ContainsKey(originalExternalId))
        {
            _mappings.LocationIds[originalExternalId] =
                $"loc-{Guid.NewGuid().ToString("N").Substring(0, 8)}";
            if (options.VerboseLogging)
                Debug.WriteLine($"[LocationAnonymizer] Created NEW ID mapping: '{originalExternalId}' -> '{_mappings.LocationIds[originalExternalId]}'");
        }
        else
        {
            if (options.VerboseLogging)
                Debug.WriteLine($"[LocationAnonymizer] Found EXISTING ID mapping: '{originalExternalId}' -> '{_mappings.LocationIds[originalExternalId]}'");
        }

        if (!_mappings.LocationNames.ContainsKey(originalExternalId))
        {
            string city;
            string name;
            int attempts = 0;
            do
            {
                city = _faker.Address.City();
                name = $"Locatie {city}";
                attempts++;
            } while (_mappings.UsedLocationNames.Contains(name) && attempts < 100);

            _mappings.LocationNames[originalExternalId] = name;
            _mappings.UsedLocationNames.Add(name);
            if (options.VerboseLogging)
                Debug.WriteLine($"[LocationAnonymizer] Created NEW name mapping: '{originalExternalId}' -> '{name}' (attempts: {attempts})");
        }
        else
        {
            if (options.VerboseLogging)
                Debug.WriteLine($"[LocationAnonymizer] Found EXISTING name mapping: '{originalExternalId}' -> '{_mappings.LocationNames[originalExternalId]}'");
        }

        if (!string.IsNullOrEmpty(location.ExternalId))
        {
            location.ExternalId = _mappings.LocationIds[originalExternalId];
        }

        location.Name = _mappings.LocationNames[originalExternalId];
        if (options.VerboseLogging)
            Debug.WriteLine($"[LocationAnonymizer] RESULT: ExternalId='{location.ExternalId}', Name='{location.Name}'");

        if (!string.IsNullOrEmpty(location.Code))
        {
            location.Code = _faker.Random.String2(4, "ABCDEFGHIJKLMNOPQRSTUVWXYZ").ToUpper();
        }
    }
}
