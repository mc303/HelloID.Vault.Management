namespace HelloID.Vault.Services.Anonymization.Utilities;

public class ReferenceMappingTable
{
    public Dictionary<string, string> DepartmentIds { get; } = new();
    public Dictionary<string, string> LocationIds { get; } = new();
    public Dictionary<string, string> EmployerIds { get; } = new();
    public Dictionary<string, string> CostCenterIds { get; } = new();
    public Dictionary<string, string> CostBearerIds { get; } = new();
    public Dictionary<string, string> TeamIds { get; } = new();
    public Dictionary<string, string> DivisionIds { get; } = new();
    public Dictionary<string, string> TitleIds { get; } = new();
    public Dictionary<string, string> OrganizationIds { get; } = new();

    public Dictionary<string, string> PersonExternalIds { get; } = new();

    public Dictionary<string, string> DepartmentNames { get; } = new();
    public Dictionary<string, string> LocationNames { get; } = new();
    public Dictionary<string, string> EmployerNames { get; } = new();
    public Dictionary<string, string> CostCenterNames { get; } = new();
    public Dictionary<string, string> CostBearerNames { get; } = new();
    public Dictionary<string, string> TeamNames { get; } = new();
    public Dictionary<string, string> DivisionNames { get; } = new();
    public Dictionary<string, string> TitleNames { get; } = new();
    public Dictionary<string, string> OrganizationNames { get; } = new();

    public Dictionary<string, string> EmployerEmailDomains { get; } = new();

    public Dictionary<string, PersonNameMapping> PersonNameMappings { get; } = new();

    public Dictionary<string, string> PersonIdToSourceNameKey { get; } = new();

    public HashSet<string> UsedLastNames { get; } = new(StringComparer.OrdinalIgnoreCase);

    public HashSet<string> UsedDepartmentNames { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> UsedLocationNames { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> UsedEmployerNames { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> UsedCostCenterNames { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> UsedCostBearerNames { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> UsedTeamNames { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> UsedDivisionNames { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> UsedTitleNames { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> UsedOrganizationNames { get; } = new(StringComparer.OrdinalIgnoreCase);

    public static string CreateNameKey(string? givenName, string? familyName)
    {
        return $"{givenName?.ToLowerInvariant()}|{familyName?.ToLowerInvariant()}";
    }
}

public class PersonNameMapping
{
    public string GivenName { get; set; } = string.Empty;
    public string FamilyName { get; set; } = string.Empty;
    public string? FamilyNamePrefix { get; set; }
    public string? FamilyNamePartner { get; set; }
    public string? FamilyNamePartnerPrefix { get; set; }
}
