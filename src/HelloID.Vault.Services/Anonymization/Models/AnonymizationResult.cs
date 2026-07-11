namespace HelloID.Vault.Services.Anonymization.Models;

/// <summary>
/// Result of vault.json anonymization operation.
/// </summary>
public class AnonymizationResult
{
    public bool Success { get; set; }
    public string? OutputFilePath { get; set; }
    public string? ErrorMessage { get; set; }

    public int PersonsSelected { get; set; }
    public int ManagersIncluded { get; set; }
    public string? SeedUsed { get; set; }

    // Personal data stats
    public int PersonsAnonymized { get; set; }
    public int PersonExternalIdsAnonymized { get; set; }
    public int BusinessEmailsAnonymized { get; set; }
    public int PersonalEmailsAnonymized { get; set; }
    public int ContactsAnonymized { get; set; }
    public int ManagersAnonymized { get; set; }

    // Business data stats
    public int DepartmentsAnonymized { get; set; }
    public int LocationsAnonymized { get; set; }
    public int EmployersAnonymized { get; set; }
    public int CostCentersAnonymized { get; set; }
    public int CostBearersAnonymized { get; set; }
    public int TeamsAnonymized { get; set; }
    public int DivisionsAnonymized { get; set; }
    public int TitlesAnonymized { get; set; }
    public int OrganizationsAnonymized { get; set; }

    // Email domain info
    /// <summary>
    /// Mapping of employer ExternalIds to their generated email domains.
    /// Key: Employer ExternalId, Value: Generated domain (e.g., "techcorp.com")
    /// </summary>
    public Dictionary<string, string> EmployerDomains { get; set; } = new();

    /// <summary>
    /// Fallback domain used for persons without an employer.
    /// </summary>
    public string? FallbackDomain { get; set; }

    public TimeSpan Duration { get; set; }
}
