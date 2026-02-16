using Dapper;
using HelloID.Vault.Data.Connection;
using HelloID.Vault.Services.Interfaces;

namespace HelloID.Vault.Services.Import.Detection;

/// <summary>
/// Detects which Primary Manager logic was used based on imported data.
/// </summary>
public class PrimaryManagerDetector
{
    private readonly IDatabaseConnectionFactory _connectionFactory;
    private readonly IPrimaryManagerService _primaryManagerService;
    private readonly IUserPreferencesService _userPreferencesService;

    public PrimaryManagerDetector(
        IDatabaseConnectionFactory connectionFactory,
        IPrimaryManagerService primaryManagerService,
        IUserPreferencesService userPreferencesService)
    {
        _connectionFactory = connectionFactory;
        _primaryManagerService = primaryManagerService;
        _userPreferencesService = userPreferencesService;
    }

    /// <summary>
    /// Auto-detects which Primary Manager logic was used based on imported data.
    /// Samples persons and compares their imported primary manager with calculated values.
    /// </summary>
    public async Task<PrimaryManagerLogic?> DetectAsync()
    {
        using var connection = _connectionFactory.CreateConnection();

        // Get persons who have a primary manager set from import
        var personsWithManager = await connection.QueryAsync<PersonWithManagerDto>(@"
            SELECT person_id AS PersonId, primary_manager_person_id AS ImportedManagerId
            FROM persons
            WHERE primary_manager_person_id IS NOT NULL
            LIMIT 100");  // Sample up to 100 persons for performance

        var personsList = personsWithManager.ToList();
        if (!personsList.Any())
        {
            Console.WriteLine("No persons with primary managers found to detect logic.");
            return null;
        }

        int contractBasedMatches = 0;
        int departmentBasedMatches = 0;

        foreach (var person in personsList)
        {
            // Calculate what the primary manager would be using each logic
            var contractBasedManager = await _primaryManagerService.CalculatePrimaryManagerAsync(
                person.PersonId, PrimaryManagerLogic.ContractBased);
            var departmentBasedManager = await _primaryManagerService.CalculatePrimaryManagerAsync(
                person.PersonId, PrimaryManagerLogic.DepartmentBased);

            // Count matches
            if (contractBasedManager == person.ImportedManagerId)
                contractBasedMatches++;
            if (departmentBasedManager == person.ImportedManagerId)
                departmentBasedMatches++;
        }

        Console.WriteLine($"Logic detection results: Contract-Based={contractBasedMatches} matches, Department-Based={departmentBasedMatches} matches (out of {personsList.Count} sampled)");

        // Determine which logic matches better
        PrimaryManagerLogic? detectedLogic;
        if (contractBasedMatches > departmentBasedMatches)
        {
            detectedLogic = PrimaryManagerLogic.ContractBased;
        }
        else if (departmentBasedMatches > contractBasedMatches)
        {
            detectedLogic = PrimaryManagerLogic.DepartmentBased;
        }
        else if (contractBasedMatches == 0 && departmentBasedMatches == 0)
        {
            // No matches - couldn't detect
            return null;
        }
        else
        {
            // Equal matches - default to Department-Based
            detectedLogic = PrimaryManagerLogic.DepartmentBased;
        }

        // Save the detected logic to user preferences
        _userPreferencesService.LastPrimaryManagerLogic = detectedLogic.Value;

        return detectedLogic;
    }

    private class PersonWithManagerDto
    {
        public string PersonId { get; set; } = string.Empty;
        public string? ImportedManagerId { get; set; }
    }
}
