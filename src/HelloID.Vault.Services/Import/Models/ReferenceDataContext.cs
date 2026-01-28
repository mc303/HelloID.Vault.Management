using HelloID.Vault.Core.Models.Entities;
using HelloID.Vault.Core.Models.Json;
using HelloID.Vault.Services.Import.Comparers;

namespace HelloID.Vault.Services.Import.Models;

/// <summary>
/// Context object containing all collected reference data from vault import.
/// </summary>
public class ReferenceDataContext
{
    // Collections of unique reference entities
    public HashSet<VaultReference> Organizations { get; set; } = new(new ReferenceComparer());
    public HashSet<VaultReference> Locations { get; set; } = new(new ReferenceComparer());
    public HashSet<VaultReference> Employers { get; set; } = new(new ReferenceComparer());
    public HashSet<VaultReference> CostCenters { get; set; } = new(new ReferenceComparer());
    public HashSet<VaultReference> CostBearers { get; set; } = new(new ReferenceComparer());
    public HashSet<VaultReference> Teams { get; set; } = new(new ReferenceComparer());
    public HashSet<VaultReference> Divisions { get; set; } = new(new ReferenceComparer());
    public HashSet<VaultReference> Titles { get; set; } = new(new ReferenceComparer());
    public HashSet<Department> Departments { get; set; } = new(new DepartmentComparer());

    // Source tracking for each reference entity (external_id -> source)
    public Dictionary<string, string?> OrganizationSources { get; set; } = new();
    public Dictionary<string, string?> LocationSources { get; set; } = new();
    public Dictionary<string, string?> EmployerSources { get; set; } = new();
    public Dictionary<string, string?> EmployerNameToSourceMap { get; set; } = new(); // ExternalId|Name -> Source
    public Dictionary<string, string?> CostCenterSources { get; set; } = new();
    public Dictionary<string, string?> CostBearerSources { get; set; } = new();
    public Dictionary<string, string?> TeamSources { get; set; } = new();
    public Dictionary<string, string?> DivisionSources { get; set; } = new();
    public Dictionary<string, string?> TitleSources { get; set; } = new();

    // Seen name+source combinations for deduplication
    public Dictionary<string, VaultReference> SeenOrganizations { get; set; } = new();
    public Dictionary<string, VaultReference> SeenLocations { get; set; } = new();
    public Dictionary<string, VaultReference> SeenEmployers { get; set; } = new();
    public Dictionary<string, VaultReference> SeenCostCenters { get; set; } = new();
    public Dictionary<string, VaultReference> SeenCostBearers { get; set; } = new();
    public Dictionary<string, VaultReference> SeenTeams { get; set; } = new();
    public Dictionary<string, VaultReference> SeenDivisions { get; set; } = new();
    public Dictionary<string, VaultReference> SeenTitles { get; set; } = new();
}
