using HelloID.Vault.Core.Models.Json;
using HelloID.Vault.Services.Interfaces;

namespace HelloID.Vault.Services.Import.Models;

/// <summary>
/// Context object containing all data needed for contract mapping operations.
/// </summary>
public class ContractMappingContext
{
    /// <summary>
    /// Lookup dictionary mapping source system IDs to database source values.
    /// </summary>
    public Dictionary<string, string> SourceLookup { get; set; } = new();

    /// <summary>
    /// Import result tracking object for statistics.
    /// </summary>
    public ImportResult Result { get; init; } = null!;

    /// <summary>
    /// Dictionary of seen locations indexed by name|source.
    /// </summary>
    public Dictionary<string, VaultReference> SeenLocations { get; set; } = new();

    /// <summary>
    /// Dictionary of seen employers indexed by name|source.
    /// </summary>
    public Dictionary<string, VaultReference> SeenEmployers { get; set; } = new();

    /// <summary>
    /// Dictionary of seen cost centers indexed by name|source.
    /// </summary>
    public Dictionary<string, VaultReference> SeenCostCenters { get; set; } = new();

    /// <summary>
    /// Dictionary of seen cost bearers indexed by name|source.
    /// </summary>
    public Dictionary<string, VaultReference> SeenCostBearers { get; set; } = new();

    /// <summary>
    /// Dictionary of seen teams indexed by name|source.
    /// </summary>
    public Dictionary<string, VaultReference> SeenTeams { get; set; } = new();

    /// <summary>
    /// Dictionary of seen divisions indexed by name|source.
    /// </summary>
    public Dictionary<string, VaultReference> SeenDivisions { get; set; } = new();

    /// <summary>
    /// Dictionary of seen titles indexed by name|source.
    /// </summary>
    public Dictionary<string, VaultReference> SeenTitles { get; set; } = new();

    /// <summary>
    /// Dictionary of seen organizations indexed by name|source.
    /// </summary>
    public Dictionary<string, VaultReference> SeenOrganizations { get; set; } = new();
}
