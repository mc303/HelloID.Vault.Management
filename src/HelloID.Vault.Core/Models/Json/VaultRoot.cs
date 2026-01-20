using System.Text.Json.Serialization;

namespace HelloID.Vault.Core.Models.Json;

/// <summary>
/// Root object for vault.json file.
/// </summary>
public class VaultRoot
{
    [JsonPropertyName("Persons")]
    public List<VaultPerson> Persons { get; set; } = new();

    [JsonPropertyName("Departments")]
    public List<VaultDepartmentReference> Departments { get; set; } = new();
}
