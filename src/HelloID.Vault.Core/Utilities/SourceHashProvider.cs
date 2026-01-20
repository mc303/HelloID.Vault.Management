using System.Security.Cryptography;
using System.Text;

namespace HelloID.Vault.Core.Utilities;

/// <summary>
/// Provides deterministic hash generation for source system namespace isolation.
/// Generates 5-character hashes to prevent ExternalId collisions across multi-source systems.
/// </summary>
public static class SourceHashProvider
{
    private static readonly Dictionary<string, string> SourceHashes = new();

    /// <summary>
    /// Gets a deterministic 5-character hash for a source system ID.
    /// Uses SHA256 hash of the source system ID, then sanitizes for database compatibility.
    /// </summary>
    /// <param name="sourceSystemId">The source system identifier</param>
    /// <returns>A 5-character hash string (e.g., "A7X9K")</returns>
    public static string GetSourceHash(string sourceSystemId)
    {
        if (string.IsNullOrEmpty(sourceSystemId))
            throw new ArgumentException("Source system ID cannot be null or empty");

        if (SourceHashes.TryGetValue(sourceSystemId, out var cachedHash))
            return cachedHash;

        // Generate deterministic SHA256 hash
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(sourceSystemId));
        var base64 = Convert.ToBase64String(hashBytes)[..5];

        // Sanitize for database compatibility
        var cleanHash = base64
            .Replace("/", "0")  // Replace forward slash
            .Replace("+", "1")  // Replace plus sign
            .Replace("=", "A"); // Replace equals sign

        SourceHashes[sourceSystemId] = cleanHash;
        return cleanHash;
    }

    /// <summary>
    /// Transforms an ExternalId with source hash for namespace isolation.
    /// Format: "{hash}-{originalExternalId}"
    /// </summary>
    /// <param name="originalExternalId">The original ExternalId from source system</param>
    /// <param name="sourceSystemId">The source system identifier</param>
    /// <returns>Transformed ExternalId with hash prefix</returns>
    public static string TransformExternalId(string originalExternalId, string sourceSystemId)
    {
        if (string.IsNullOrEmpty(originalExternalId))
            throw new ArgumentException("Original ExternalId cannot be null or empty");

        var hash = GetSourceHash(sourceSystemId);
        return $"{hash}-{originalExternalId}";
    }
}