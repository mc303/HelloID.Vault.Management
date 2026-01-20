namespace HelloID.Vault.Core.Models.DTOs;

/// <summary>
/// Metadata for cache tables to track refresh status and performance.
/// </summary>
public class CacheMetadata
{
    public string CacheName { get; set; } = string.Empty;
    public DateTime LastRefreshed { get; set; }
    public int RowCount { get; set; }
    public int RefreshDurationMs { get; set; }
}
