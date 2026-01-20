using System.Text.Json.Serialization;

namespace HelloID.Vault.Core.Models.Json;

/// <summary>
/// Contract object from vault.json.
/// </summary>
public class VaultContract
{
    [JsonPropertyName("Context")]
    public VaultContext? Context { get; set; }

    [JsonPropertyName("ExternalId")]
    public string? ExternalId { get; set; }

    [JsonPropertyName("StartDate")]
    public DateTime? StartDate { get; set; }

    [JsonPropertyName("EndDate")]
    public DateTime? EndDate { get; set; }

    [JsonPropertyName("Type")]
    public VaultType? Type { get; set; }

    [JsonPropertyName("Details")]
    public VaultContractDetails? Details { get; set; }

    [JsonPropertyName("Location")]
    public VaultReference? Location { get; set; }

    [JsonPropertyName("CostCenter")]
    public VaultReference? CostCenter { get; set; }

    [JsonPropertyName("CostBearer")]
    public VaultReference? CostBearer { get; set; }

    [JsonPropertyName("Employer")]
    public VaultReference? Employer { get; set; }

    [JsonPropertyName("Manager")]
    public VaultManagerReference? Manager { get; set; }

    [JsonPropertyName("Team")]
    public VaultReference? Team { get; set; }

    [JsonPropertyName("Department")]
    public VaultDepartmentReference? Department { get; set; }

    [JsonPropertyName("Division")]
    public VaultReference? Division { get; set; }

    [JsonPropertyName("Title")]
    public VaultReference? Title { get; set; }

    [JsonPropertyName("Organization")]
    public VaultReference? Organization { get; set; }

    [JsonPropertyName("Source")]
    public VaultSource? Source { get; set; }

    [JsonPropertyName("Custom")]
    public Dictionary<string, object?>? Custom { get; set; }
}

public class VaultContext
{
    [JsonPropertyName("InConditions")]
    public bool InConditions { get; set; }
}

public class VaultType
{
    [JsonPropertyName("Code")]
    public string? Code { get; set; }

    [JsonPropertyName("Description")]
    public string? Description { get; set; }
}

public class VaultContractDetails
{
    [JsonPropertyName("Fte")]
    public decimal? Fte { get; set; }

    [JsonPropertyName("HoursPerWeek")]
    public decimal? HoursPerWeek { get; set; }

    [JsonPropertyName("Percentage")]
    public decimal? Percentage { get; set; }

    [JsonPropertyName("Sequence")]
    public int? Sequence { get; set; }
}

/// <summary>
/// Common reference structure used throughout vault.json.
/// </summary>
public class VaultReference
{
    [JsonPropertyName("ExternalId")]
    public string? ExternalId { get; set; }

    [JsonPropertyName("Code")]
    public string? Code { get; set; }

    [JsonPropertyName("Name")]
    public string? Name { get; set; }
}

public class VaultManagerReference
{
    [JsonPropertyName("PersonId")]
    public string? PersonId { get; set; }

    [JsonPropertyName("ExternalId")]
    public string? ExternalId { get; set; }

    [JsonPropertyName("DisplayName")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("Email")]
    public string? Email { get; set; }
}

public class VaultDepartmentReference
{
    [JsonPropertyName("DepartmentVersion")]
    public string? DepartmentVersion { get; set; }

    [JsonPropertyName("DisplayName")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("ExternalId")]
    public string? ExternalId { get; set; }

    [JsonPropertyName("Code")]
    public string? Code { get; set; }

    [JsonPropertyName("ParentExternalId")]
    public string? ParentExternalId { get; set; }

    [JsonPropertyName("Manager")]
    public VaultManagerReference? Manager { get; set; }

    [JsonPropertyName("Source")]
    public VaultSource? Source { get; set; }
}
