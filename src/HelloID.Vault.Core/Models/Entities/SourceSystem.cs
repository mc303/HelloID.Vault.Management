namespace HelloID.Vault.Core.Models.Entities;

/// <summary>
/// Represents a source system that provides data to the vault.
/// Source systems are used for namespace isolation and data provenance tracking.
/// </summary>
public class SourceSystem
{
    /// <summary>
    /// Unique identifier for the source system (UUID format)
    /// Examples: "847fb1b8-de82-4a64-997f-86daeba38114", "1c0f4b1b-fcc4-4631-9e0a-8ae4c51731c7"
    /// </summary>
    public string SystemId { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable display name for the source system
    /// Examples: "AFAS Profit - Frion", "AFAS Profit - BBG", "Manual Entry"
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Alternative identifier for the source system
    /// Often defaults to the same value as SystemId
    /// </summary>
    public string IdentificationKey { get; set; } = string.Empty;

    /// <summary>
    /// Computed: Number of records that reference this source system
    /// Used for data usage statistics and cleanup decisions
    /// </summary>
    public int ReferenceCount { get; set; } = 0;

    /// <summary>
    /// Computed: Timestamp when source system record was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}