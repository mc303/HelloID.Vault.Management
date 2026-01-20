namespace HelloID.Vault.Core.Models.Entities;

/// <summary>
/// Configuration entry for determining the primary contract.
/// Represents a single field in the priority ordering.
/// </summary>
public class PrimaryContractConfig
{
    public int Id { get; set; }

    /// <summary>
    /// Internal field name (e.g., 'Fte', 'HoursPerWeek', 'Sequence')
    /// </summary>
    public string FieldName { get; set; } = string.Empty;

    /// <summary>
    /// User-friendly display name for the field
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Sort direction: 'ASC' for ascending, 'DESC' for descending
    /// </summary>
    public string SortOrder { get; set; } = "DESC";

    /// <summary>
    /// Priority order (1 = highest priority, 2 = next, etc.)
    /// </summary>
    public int PriorityOrder { get; set; }

    /// <summary>
    /// Whether this field is active in the calculation
    /// </summary>
    public bool IsActive { get; set; } = true;

    public string CreatedAt { get; set; } = string.Empty;
    public string UpdatedAt { get; set; } = string.Empty;
}
