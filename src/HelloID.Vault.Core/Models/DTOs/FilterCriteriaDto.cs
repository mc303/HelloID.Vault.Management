namespace HelloID.Vault.Core.Models.DTOs;

/// <summary>
/// Represents a single filter criterion for advanced search.
/// </summary>
public class FilterCriteriaDto
{
    /// <summary>
    /// The field name to filter on (e.g., "PersonName", "StartDate").
    /// Maps to ContractDetailDto property names.
    /// </summary>
    public string FieldName { get; set; } = string.Empty;

    /// <summary>
    /// Display name for the field (e.g., "Person Name", "Start Date").
    /// </summary>
    public string FieldDisplayName { get; set; } = string.Empty;

    /// <summary>
    /// The filter operator (Contains, Equals, StartsWith, EndsWith, GreaterThan, LessThan, IsEmpty, IsNotEmpty).
    /// </summary>
    public string Operator { get; set; } = "Contains";

    /// <summary>
    /// The value to filter by.
    /// </summary>
    public string Value { get; set; } = string.Empty;
}
