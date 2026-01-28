using HelloID.Vault.Core.Models.Json;

namespace HelloID.Vault.Services.Import.Utilities;

/// <summary>
/// Provides utility methods for resolving reference entities.
/// </summary>
public static class ReferenceResolver
{
    /// <summary>
    /// Resolves the external_id for a reference entity by looking up the transformed GUID from seenDictionary.
    /// Handles cases where entities without external_id get a generated GUID during collection.
    /// </summary>
    public static string? ResolveReferenceExternalId(VaultReference? reference, string? contractSource,
        Dictionary<string, VaultReference> seenDictionary)
    {
        if (reference == null || string.IsNullOrWhiteSpace(reference.Name))
            return null;

        var key = $"{reference.Name}|{contractSource ?? "default"}";
        if (seenDictionary.TryGetValue(key, out var transformed))
            return transformed.ExternalId;

        return null;
    }
}
