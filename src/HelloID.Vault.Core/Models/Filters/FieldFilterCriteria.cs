namespace HelloID.Vault.Core.Models.Filters;

/// <summary>
/// A single advanced search filter criterion for custom field data.
/// </summary>
public class FieldFilterCriteria
{
    public string FieldName { get; set; } = string.Empty;
    public string FieldDisplayName { get; set; } = string.Empty;
    public string Operator { get; set; } = "Contains";
    public string? Value { get; set; }
}

/// <summary>
/// Operators supported by advanced search.
/// </summary>
public static class FieldFilterOperators
{
    public const string Contains = "Contains";
    public const string Equals = "Equals";
    public const string NotEquals = "Not Equals";
    public const string IsEmpty = "Is Empty";
    public const string IsNotEmpty = "Is Not Empty";

    public static readonly string[] All = { Contains, Equals, NotEquals, IsEmpty, IsNotEmpty };
}
