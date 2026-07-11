using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bogus;
using HelloID.Vault.Core.Models.Json;
using HelloID.Vault.Services.Anonymization.Models;
using HelloID.Vault.Services.Anonymization.Strategies.BusinessData;
using HelloID.Vault.Services.Anonymization.Strategies.PersonalData;
using HelloID.Vault.Services.Anonymization.Utilities;
using HelloID.Vault.Services.Interfaces;

namespace HelloID.Vault.Services.Anonymization;

/// <summary>
/// Main service for anonymizing vault.json files.
/// Orchestrates all anonymizers and ensures consistency.
/// </summary>
public class VaultAnonymizerService : IVaultAnonymizerService
{
    public async Task<AnonymizationResult> AnonymizeAsync(
        string inputFilePath,
        string outputFilePath,
        AnonymizationOptions options,
        IProgress<AnonymizationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new AnonymizationResult();

        try
        {
            // Phase 1: Load (5%)
            progress?.Report(new AnonymizationProgress
            {
                CurrentPhase = "Loading vault.json...",
                ProcessedItems = 0,
                TotalItems = 100
            });

            var vault = await LoadVaultJsonAsync(inputFilePath);

            if (vault.Persons == null || vault.Persons.Count == 0)
            {
                return new AnonymizationResult
                {
                    Success = false,
                    ErrorMessage = "No persons found in vault.json"
                };
            }

            int totalPersonsInFile = vault.Persons.Count;
            List<VaultPerson> selectedPersons;
            int personsSelected = 0;
            int managersIncluded = 0;

            if (options.MaxPersonsToImport > 0 && vault.Persons.Count > options.MaxPersonsToImport)
            {
                progress?.Report(new AnonymizationProgress
                {
                    CurrentPhase = "Selecting person subset...",
                    ProcessedItems = 3,
                    TotalItems = 100
                });

                var (subset, selected, managers) = SelectPersonSubset(vault.Persons, options.MaxPersonsToImport, options.Seed);
                selectedPersons = subset;
                personsSelected = selected;
                managersIncluded = managers;
                vault.Persons = selectedPersons;

                result.PersonsSelected = personsSelected;
                result.ManagersIncluded = managersIncluded;
                result.SeedUsed = options.Seed;
            }
            else
            {
                selectedPersons = vault.Persons;
                result.PersonsSelected = vault.Persons.Count;
                result.ManagersIncluded = 0;
                result.SeedUsed = options.MaxPersonsToImport > 0 ? options.Seed : null;
            }

            // Initialize utilities
            // Always use European pool for all modes to get more diverse names
            var multiLocaleFaker = FakerFactory.CreateEuropeanPoolFaker(options.Locale);
            var faker = multiLocaleFaker.Primary;
            var mappings = new ReferenceMappingTable();
            var domainGenerator = new EmailDomainGenerator(faker, options);
            var namePool = new NamePool(multiLocaleFaker, options.NameSharingMode);

            // Phase 2: Build reference mappings (15%)
            progress?.Report(new AnonymizationProgress
            {
                CurrentPhase = "Building reference mappings...",
                ProcessedItems = 5,
                TotalItems = 100
            });

            BuildReferenceMappings(vault, mappings, faker, options);

            // Initialize anonymizers
            var personAnonymizer = new PersonAnonymizer(multiLocaleFaker, mappings, domainGenerator, namePool);
            var managerAnonymizer = new ManagerAnonymizer(faker, mappings);
            var departmentAnonymizer = new DepartmentAnonymizer(faker, mappings);
            var locationAnonymizer = new LocationAnonymizer(faker, mappings);
            var employerAnonymizer = new EmployerAnonymizer(faker, mappings, domainGenerator);
            var costCenterAnonymizer = new CostCenterAnonymizer(faker, mappings);
            var costBearerAnonymizer = new CostBearerAnonymizer(faker, mappings);
            var teamAnonymizer = new TeamAnonymizer(faker, mappings);
            var divisionAnonymizer = new DivisionAnonymizer(faker, mappings);
            var titleAnonymizer = new TitleAnonymizer(faker, mappings);
            var organizationAnonymizer = new OrganizationAnonymizer(faker, mappings);

            // Phase 3: Anonymize persons (40%)
            int totalContracts = vault.Persons.Sum(p => p.Contracts.Count);
            int processedContracts = 0;

            for (int i = 0; i < vault.Persons.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var person = vault.Persons[i];
                var primaryEmployerId = GetPrimaryEmployerId(person);

                string? domainUsed = null;
                if (!string.IsNullOrEmpty(primaryEmployerId))
                {
                    domainUsed = domainGenerator.GetBusinessEmailDomain(primaryEmployerId, mappings);
                }
                else
                {
                    domainUsed = domainGenerator.GetFallbackDomain();
                }

                progress?.Report(new AnonymizationProgress
                {
                    CurrentPhase = $"Anonymizing person {i + 1} of {vault.Persons.Count}",
                    CurrentItem = person.DisplayName,
                    ProcessedItems = 20 + (i * 40 / vault.Persons.Count),
                    TotalItems = 100,
                    BusinessDomainUsed = domainUsed
                });

                // Anonymize person data
                personAnonymizer.Anonymize(person, options, primaryEmployerId);
                result.PersonsAnonymized++;

                // Anonymize person-level location
                if (person.Location != null)
                {
                    if (options.VerboseLogging)
                        Debug.WriteLine($"[VaultAnonymizerService] Anonymizing person.Location for '{person.DisplayName}' - ExternalId='{person.Location.ExternalId}'");
                    locationAnonymizer.Anonymize(person.Location, options);
                }

                // Anonymize primary manager
                if (person.PrimaryManager != null)
                {
                    managerAnonymizer.Anonymize(person.PrimaryManager, options);
                    result.ManagersAnonymized++;
                }

                // Anonymize all contracts
                // Note: PrimaryContract is already included in person.Contracts list
                foreach (var contract in person.Contracts)
                {
                    processedContracts++;

                    // Report progress every 100 contracts
                    if (processedContracts % 100 == 0)
                    {
                        progress?.Report(new AnonymizationProgress
                        {
                            CurrentPhase = $"Processing contracts ({processedContracts}/{totalContracts})",
                            ProcessedItems = 20 + (i * 40 / vault.Persons.Count),
                            TotalItems = 100
                        });
                    }

                    AnonymizeContract(contract, options,
                        departmentAnonymizer, locationAnonymizer, employerAnonymizer,
                        costCenterAnonymizer, costBearerAnonymizer, teamAnonymizer,
                        divisionAnonymizer, titleAnonymizer, organizationAnonymizer,
                        managerAnonymizer);
                }
            }

            // Phase 4: Anonymize reference data (35%)
            progress?.Report(new AnonymizationProgress
            {
                CurrentPhase = "Anonymizing reference data...",
                ProcessedItems = 60,
                TotalItems = 100
            });

            AnonymizeReferenceData(vault, options, departmentAnonymizer, mappings, faker);

            // Phase 5: Save (5%)
            progress?.Report(new AnonymizationProgress
            {
                CurrentPhase = "Saving anonymized file...",
                ProcessedItems = 95,
                TotalItems = 100
            });

            await SaveVaultJsonAsync(vault, outputFilePath);

            // Build result
            result.Success = true;
            result.OutputFilePath = outputFilePath;
            result.Duration = stopwatch.Elapsed;
            result.EmployerDomains = mappings.EmployerEmailDomains;
            result.FallbackDomain = domainGenerator.GetFallbackDomain();

            // Calculate statistics
            result.BusinessEmailsAnonymized = CountBusinessEmails(vault);
            result.PersonalEmailsAnonymized = CountPersonalEmails(vault);
            result.DepartmentsAnonymized = mappings.DepartmentIds.Count;
            result.LocationsAnonymized = mappings.LocationIds.Count;
            result.EmployersAnonymized = mappings.EmployerIds.Count;
            result.CostCentersAnonymized = mappings.CostCenterIds.Count;
            result.CostBearersAnonymized = mappings.CostBearerIds.Count;
            result.TeamsAnonymized = mappings.TeamIds.Count;
            result.DivisionsAnonymized = mappings.DivisionIds.Count;
            result.TitlesAnonymized = mappings.TitleIds.Count;
            result.OrganizationsAnonymized = mappings.OrganizationIds.Count;
            result.PersonExternalIdsAnonymized = mappings.PersonExternalIds.Count;

            progress?.Report(new AnonymizationProgress
            {
                CurrentPhase = "Complete!",
                ProcessedItems = 100,
                TotalItems = 100
            });

            namePool.LogSummary();

            return result;
        }
        catch (OperationCanceledException)
        {
            result.Success = false;
            result.ErrorMessage = "Anonymization cancelled by user";
            return result;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            return result;
        }
    }

    public async Task<bool> CanAnonymizeAsync(string filePath)
    {
        if (!File.Exists(filePath))
            return false;

        try
        {
            var json = await File.ReadAllTextAsync(filePath);
            var vault = JsonSerializer.Deserialize<VaultRoot>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return vault?.Persons != null && vault.Persons.Count > 0;
        }
        catch
        {
            return false;
        }
    }

    private async Task<VaultRoot> LoadVaultJsonAsync(string filePath)
    {
        var json = await File.ReadAllTextAsync(filePath);
        return JsonSerializer.Deserialize<VaultRoot>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidOperationException("Failed to parse vault.json");
    }

    private async Task SaveVaultJsonAsync(VaultRoot vault, string filePath)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        var json = JsonSerializer.Serialize(vault, options);
        await File.WriteAllTextAsync(filePath, json);
    }

    private void BuildReferenceMappings(VaultRoot vault, ReferenceMappingTable mappings, Faker faker, AnonymizationOptions options)
    {
        // Build mappings for all reference data to ensure consistency
        // This is called before anonymization to pre-populate mapping tables

        // Build person ExternalId mappings
        if (options.AnonymizePersonExternalIds)
        {
            foreach (var person in vault.Persons)
            {
                if (!string.IsNullOrEmpty(person.ExternalId) &&
                    !mappings.PersonExternalIds.ContainsKey(person.ExternalId))
                {
                    mappings.PersonExternalIds[person.ExternalId] =
                        $"emp-{Guid.NewGuid().ToString("N").Substring(0, 8)}";
                }
            }
        }

        // Build employer mappings and email domains
        if (options.AnonymizeEmployers && options.UseMultiEmployerDomains)
        {
            foreach (var person in vault.Persons)
            {
                foreach (var contract in person.Contracts)
                {
                    if (!string.IsNullOrEmpty(contract.Employer?.ExternalId) &&
                        !mappings.EmployerEmailDomains.ContainsKey(contract.Employer.ExternalId))
                    {
                        var domainGenerator = new EmailDomainGenerator(faker, options);
                        domainGenerator.GetBusinessEmailDomain(contract.Employer.ExternalId, mappings);
                    }
                }
            }
        }
    }

    private string? GetPrimaryEmployerId(VaultPerson person)
    {
        // Get employer from primary contract or first contract
        var contract = person.PrimaryContract ?? person.Contracts.FirstOrDefault();
        return contract?.Employer?.ExternalId;
    }

    private void AnonymizeContract(
        VaultContract contract,
        AnonymizationOptions options,
        DepartmentAnonymizer departmentAnonymizer,
        LocationAnonymizer locationAnonymizer,
        EmployerAnonymizer employerAnonymizer,
        CostCenterAnonymizer costCenterAnonymizer,
        CostBearerAnonymizer costBearerAnonymizer,
        TeamAnonymizer teamAnonymizer,
        DivisionAnonymizer divisionAnonymizer,
        TitleAnonymizer titleAnonymizer,
        OrganizationAnonymizer organizationAnonymizer,
        ManagerAnonymizer managerAnonymizer)
    {
        if (options.VerboseLogging)
        {
            Debug.WriteLine($"[VaultAnonymizerService] AnonymizeContract START - Department: '{contract.Department?.ExternalId}', Location: '{contract.Location?.ExternalId}'");
        }

        // Anonymize with error handling - abort on first error
        try
        {
            if (options.VerboseLogging)
                Debug.WriteLine($"[VaultAnonymizerService] → Department");
            departmentAnonymizer.Anonymize(contract.Department, options);
        }
        catch (Exception ex)
        {
            var errorMsg = $"Department anonymization failed: {ex.Message}";
            Debug.WriteLine($"[VaultAnonymizerService] ✗ {errorMsg}");
            throw new InvalidOperationException(errorMsg, ex);
        }

        try
        {
            if (options.VerboseLogging)
                Debug.WriteLine($"[VaultAnonymizerService] → Location");
            locationAnonymizer.Anonymize(contract.Location, options);
        }
        catch (Exception ex)
        {
            var errorMsg = $"Location anonymization failed: {ex.Message}";
            Debug.WriteLine($"[VaultAnonymizerService] ✗ {errorMsg}");
            throw new InvalidOperationException(errorMsg, ex);
        }

        try
        {
            if (options.VerboseLogging)
                Debug.WriteLine($"[VaultAnonymizerService] → Employer");
            employerAnonymizer.Anonymize(contract.Employer, options);
        }
        catch (Exception ex)
        {
            var errorMsg = $"Employer anonymization failed: {ex.Message}";
            Debug.WriteLine($"[VaultAnonymizerService] ✗ {errorMsg}");
            throw new InvalidOperationException(errorMsg, ex);
        }

        try
        {
            if (options.VerboseLogging)
                Debug.WriteLine($"[VaultAnonymizerService] → CostCenter");
            costCenterAnonymizer.Anonymize(contract.CostCenter, options);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"CostCenter anonymization failed: {ex.Message}", ex);
        }

        try
        {
            if (options.VerboseLogging)
                Debug.WriteLine($"[VaultAnonymizerService] → CostBearer");
            costBearerAnonymizer.Anonymize(contract.CostBearer, options);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"CostBearer anonymization failed: {ex.Message}", ex);
        }

        try
        {
            if (options.VerboseLogging)
                Debug.WriteLine($"[VaultAnonymizerService] → Team");
            teamAnonymizer.Anonymize(contract.Team, options);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Team anonymization failed: {ex.Message}", ex);
        }

        try
        {
            if (options.VerboseLogging)
                Debug.WriteLine($"[VaultAnonymizerService] → Division");
            divisionAnonymizer.Anonymize(contract.Division, options);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Division anonymization failed: {ex.Message}", ex);
        }

        try
        {
            if (options.VerboseLogging)
                Debug.WriteLine($"[VaultAnonymizerService] → Title");
            titleAnonymizer.Anonymize(contract.Title, options);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Title anonymization failed: {ex.Message}", ex);
        }

        try
        {
            if (options.VerboseLogging)
                Debug.WriteLine($"[VaultAnonymizerService] → Organization");
            organizationAnonymizer.Anonymize(contract.Organization, options);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Organization anonymization failed: {ex.Message}", ex);
        }

        try
        {
            if (options.VerboseLogging)
                Debug.WriteLine($"[VaultAnonymizerService] → Manager");
            managerAnonymizer.Anonymize(contract.Manager, options);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Manager anonymization failed: {ex.Message}", ex);
        }

        if (options.VerboseLogging)
        {
            Debug.WriteLine($"[VaultAnonymizerService] AnonymizeContract COMPLETE");
        }
    }

    private void AnonymizeReferenceData(
        VaultRoot vault,
        AnonymizationOptions options,
        DepartmentAnonymizer departmentAnonymizer,
        ReferenceMappingTable mappings,
        Faker faker)
    {
        Debug.WriteLine($"[VaultAnonymizerService] AnonymizeReferenceData - AnonymizeDepartments={options.AnonymizeDepartments}");
        
        if (!options.AnonymizeDepartments) return;

        if (vault.Departments == null || vault.Departments.Count == 0)
        {
            Debug.WriteLine("[VaultAnonymizerService] vault.Departments is null or empty");
            return;
        }

        Debug.WriteLine($"[VaultAnonymizerService] Processing {vault.Departments.Count} root-level departments");

        foreach (var department in vault.Departments)
        {
            if (department == null)
            {
                Debug.WriteLine("[VaultAnonymizerService] Skipping null department");
                continue;
            }
            
            if (string.IsNullOrEmpty(department.ExternalId))
            {
                Debug.WriteLine($"[VaultAnonymizerService] Skipping department with null/empty ExternalId, DisplayName='{department.DisplayName}'");
                continue;
            }

            var originalExternalId = department.ExternalId;
            Debug.WriteLine($"[VaultAnonymizerService] Root department: ExternalId='{originalExternalId}', DisplayName='{department.DisplayName}'");

            // Use the DepartmentAnonymizer for consistent handling including unique name checks
            departmentAnonymizer.Anonymize(department, options);

            // Handle ParentExternalId separately (not in DepartmentAnonymizer)
            var originalParentId = department.ParentExternalId;
            if (!string.IsNullOrEmpty(originalParentId))
            {
                if (!mappings.DepartmentIds.ContainsKey(originalParentId))
                {
                    mappings.DepartmentIds[originalParentId] =
                        $"dept-{Guid.NewGuid().ToString("N").Substring(0, 8)}";
                    Debug.WriteLine($"[VaultAnonymizerService] Created parent ID mapping: '{originalParentId}' -> '{mappings.DepartmentIds[originalParentId]}'");
                }
                department.ParentExternalId = mappings.DepartmentIds[originalParentId];
            }

            if (department.Manager != null && (options.AnonymizeNames || options.AnonymizePersonExternalIds))
            {
                var managerAnonymizer = new ManagerAnonymizer(faker, mappings);
                managerAnonymizer.Anonymize(department.Manager, options);
            }
        }
    }

    private int CountBusinessEmails(VaultRoot vault)
    {
        int count = 0;
        foreach (var person in vault.Persons)
        {
            if (!string.IsNullOrEmpty(person.Contact?.Business?.Email))
                count++;
        }
        return count;
    }

    private int CountPersonalEmails(VaultRoot vault)
    {
        int count = 0;
        foreach (var person in vault.Persons)
        {
            if (!string.IsNullOrEmpty(person.Contact?.Personal?.Email))
                count++;
        }
        return count;
    }

    private (List<VaultPerson> Subset, int Selected, int Managers) SelectPersonSubset(
        List<VaultPerson> allPersons,
        int maxPersons,
        string seed)
    {
        var random = new Random(seed.GetHashCode());

        var shuffled = allPersons
            .OrderBy(p => random.Next())
            .ToList();

        int minPersons = Math.Min(5, maxPersons);
        int targetCount = Math.Max(minPersons, maxPersons);

        var selected = shuffled.Take(targetCount).ToList();
        var selectedPersonIds = new HashSet<string>(selected.Select(p => p.PersonId));

        var managerExternalIds = new HashSet<string>();
        foreach (var person in selected)
        {
            if (person.PrimaryManager?.ExternalId != null)
                managerExternalIds.Add(person.PrimaryManager.ExternalId);

            if (person.PrimaryContract?.Manager?.ExternalId != null)
                managerExternalIds.Add(person.PrimaryContract.Manager.ExternalId);

            foreach (var contract in person.Contracts)
            {
                if (contract.Manager?.ExternalId != null)
                    managerExternalIds.Add(contract.Manager.ExternalId);
            }
        }

        var managerPersons = allPersons
            .Where(p => !selectedPersonIds.Contains(p.PersonId) && 
                        !string.IsNullOrEmpty(p.ExternalId) && 
                        managerExternalIds.Contains(p.ExternalId))
            .ToList();

        var result = new List<VaultPerson>(selected);
        result.AddRange(managerPersons);

        return (result, selected.Count, managerPersons.Count);
    }
}
