using System.Diagnostics;
using Bogus;
using HelloID.Vault.Core.Models.Json;
using HelloID.Vault.Services.Anonymization.Models;
using HelloID.Vault.Services.Anonymization.Utilities;

namespace HelloID.Vault.Services.Anonymization.Strategies.BusinessData;

public class DepartmentAnonymizer
{
    private readonly Faker _faker;
    private readonly ReferenceMappingTable _mappings;

    public DepartmentAnonymizer(Faker faker, ReferenceMappingTable mappings)
    {
        _faker = faker ?? throw new ArgumentNullException(nameof(faker));
        _mappings = mappings ?? throw new ArgumentNullException(nameof(mappings));
    }

    public void Anonymize(VaultDepartmentReference? department, AnonymizationOptions options)
    {
        if (department == null)
        {
            if (options.VerboseLogging)
                Debug.WriteLine("[DepartmentAnonymizer] department is NULL, skipping");
            return;
        }

        if (options.VerboseLogging)
            Debug.WriteLine($"[DepartmentAnonymizer] Called with ExternalId='{department.ExternalId}', DisplayName='{department.DisplayName}'");

        if (!options.AnonymizeDepartments)
        {
            if (options.VerboseLogging)
                Debug.WriteLine("[DepartmentAnonymizer] AnonymizeDepartments is FALSE, skipping");
            return;
        }

        if (string.IsNullOrEmpty(department.ExternalId))
        {
            if (options.VerboseLogging)
                Debug.WriteLine("[DepartmentAnonymizer] ExternalId is null/empty, skipping");
            return;
        }

        var originalExternalId = department.ExternalId;
        if (options.VerboseLogging)
            Debug.WriteLine($"[DepartmentAnonymizer] Processing department with ExternalId='{originalExternalId}'");

        if (!_mappings.DepartmentIds.ContainsKey(originalExternalId))
        {
            _mappings.DepartmentIds[originalExternalId] =
                $"dept-{Guid.NewGuid().ToString("N").Substring(0, 8)}";
            if (options.VerboseLogging)
                Debug.WriteLine($"[DepartmentAnonymizer] Created NEW ID mapping: '{originalExternalId}' -> '{_mappings.DepartmentIds[originalExternalId]}'");
        }
        else
        {
            if (options.VerboseLogging)
                Debug.WriteLine($"[DepartmentAnonymizer] Found EXISTING ID mapping: '{originalExternalId}' -> '{_mappings.DepartmentIds[originalExternalId]}'");
        }

        if (!_mappings.DepartmentNames.ContainsKey(originalExternalId))
        {
            string name;
            int attempts = 0;
            do
            {
                name = _faker.Commerce.Department();
                attempts++;
            } while (_mappings.UsedDepartmentNames.Contains(name) && attempts < 100);

            _mappings.DepartmentNames[originalExternalId] = name;
            _mappings.UsedDepartmentNames.Add(name);
            if (options.VerboseLogging)
                Debug.WriteLine($"[DepartmentAnonymizer] Created NEW name mapping: '{originalExternalId}' -> '{name}' (attempts: {attempts}, used names count: {_mappings.UsedDepartmentNames.Count})");
        }
        else
        {
            if (options.VerboseLogging)
                Debug.WriteLine($"[DepartmentAnonymizer] Found EXISTING name mapping: '{originalExternalId}' -> '{_mappings.DepartmentNames[originalExternalId]}'");
        }

        department.ExternalId = _mappings.DepartmentIds[originalExternalId];
        department.DisplayName = _mappings.DepartmentNames[originalExternalId];
        if (options.VerboseLogging)
            Debug.WriteLine($"[DepartmentAnonymizer] RESULT: ExternalId='{department.ExternalId}', DisplayName='{department.DisplayName}'");

        if (!string.IsNullOrEmpty(department.Code))
        {
            department.Code = _faker.Commerce.Categories(1)[0].ToUpper().Substring(0, Math.Min(4, department.DisplayName?.Length ?? 4));
        }

        if (department.Manager != null && (options.AnonymizeNames || options.AnonymizePersonExternalIds))
        {
            var managerAnonymizer = new PersonalData.ManagerAnonymizer(_faker, _mappings);
            managerAnonymizer.Anonymize(department.Manager, options);
        }
    }
}
