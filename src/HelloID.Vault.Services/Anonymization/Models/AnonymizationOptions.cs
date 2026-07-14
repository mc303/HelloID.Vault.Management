namespace HelloID.Vault.Services.Anonymization.Models;

public class AnonymizationOptions
{
    public AnonymizationLocale Locale { get; set; } = AnonymizationLocale.Dutch;

    public int ForeignNamePercentage { get; set; } = 0;

    public ForeignNameMix ForeignNameMix { get; set; } = ForeignNameMix.EasternEuropean;

    public NameSharingMode NameSharingMode { get; set; } = NameSharingMode.Unique;

    public int MaxPersonsToImport { get; set; } = 0;

    public string Seed { get; set; } = "default";

    public bool AnonymizeNames { get; set; } = true;
    public bool AnonymizeEmails { get; set; } = true;
    public bool AnonymizePhones { get; set; } = true;
    public bool AnonymizeAddresses { get; set; } = true;
    public bool AnonymizeBirthDates { get; set; } = true;
    public bool AnonymizeUserNames { get; set; } = true;

    public bool AnonymizePersonExternalIds { get; set; } = true;
    public bool AnonymizeReferenceExternalIds { get; set; } = true;

    public bool UseCustomExternalIdRange { get; set; } = false;
    public int ExternalIdMin { get; set; } = 100000;
    public int ExternalIdMax { get; set; } = 200000;
    public bool PadExternalId { get; set; } = false;
    public bool UseRandomExternalIds { get; set; } = false;

    public bool AnonymizeDepartments { get; set; } = true;
    public bool AnonymizeLocations { get; set; } = true;
    public bool AnonymizeEmployers { get; set; } = true;
    public bool AnonymizeCostCenters { get; set; } = true;
    public bool AnonymizeCostBearers { get; set; } = true;
    public bool AnonymizeTeams { get; set; } = true;
    public bool AnonymizeDivisions { get; set; } = true;
    public bool AnonymizeTitles { get; set; } = true;
    public bool AnonymizeOrganizations { get; set; } = true;

    public bool UseConsistentBusinessDomain { get; set; } = true;
    public bool UseMultiEmployerDomains { get; set; } = true;
    public string? CustomBusinessEmailDomain { get; set; } = null;

    public bool KeepAnonymizedFile { get; set; } = true;

    public bool VerboseLogging { get; set; } = false;
}

public enum AnonymizationLocale
{
    Dutch,
    English
}

[Flags]
public enum ForeignNameMix
{
    None = 0,
    EasternEuropean = 1,
    WesternEuropean = 2,
    All = EasternEuropean | WesternEuropean
}

public enum NameSharingMode
{
    Unlimited = 0,
    Unique = 1,
    Max2 = 2,
    Max3 = 3,
    Max4 = 4,
    Pairs = 5,
    Triples = 6,
    Quadruples = 7
}
