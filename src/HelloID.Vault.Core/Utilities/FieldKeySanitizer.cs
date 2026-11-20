using HelloID.Vault.Core.Models.Json;

namespace HelloID.Vault.Core.Utilities;

/// <summary>
/// Sanitizes custom field keys into the storage-safe form required by
/// SQLite/Turso JSON paths (<c>^[A-Za-z0-9_]+$</c>). Turso Cloud does not
/// resolve quoted path labels, so keys containing other characters cannot be
/// queried there. Display names keep the original text; only the storage key
/// changes.
/// </summary>
public static class FieldKeySanitizer
{
    private static readonly System.Text.RegularExpressions.Regex SafePattern =
        new("^[A-Za-z0-9_]+$", System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>
    /// True when the key is already in storage-safe form.
    /// </summary>
    public static bool IsSafe(string fieldKey) => SafePattern.IsMatch(fieldKey);

    /// <summary>
    /// Converts a field key into storage-safe form: invalid characters become
    /// underscores, repeats collapse, leading/trailing underscores are trimmed,
    /// an empty result becomes "field", and a leading digit gets an underscore
    /// prefix. Keys that are already safe are returned unchanged.
    /// </summary>
    public static string Sanitize(string fieldKey)
    {
        if (string.IsNullOrWhiteSpace(fieldKey))
        {
            return "field";
        }

        if (IsSafe(fieldKey))
        {
            return fieldKey[..Math.Min(100, fieldKey.Length)];
        }

        var chars = fieldKey.Select(c => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_' ? c : '_');
        var key = new string(chars.ToArray());
        key = System.Text.RegularExpressions.Regex.Replace(key, "_{2,}", "_").Trim('_');

        if (key.Length == 0)
        {
            key = "field";
        }

        if (char.IsDigit(key[0]))
        {
            key = "_" + key;
        }

        return key[..Math.Min(100, key.Length)];
    }

    /// <summary>
    /// Re-keys every persons/contracts custom field dictionary in the vault
    /// data with sanitized keys, appending _2/_3/... when two source keys
    /// sanitize to the same name within one dictionary. Run this right after
    /// deserializing vault.json so schema creation and value storage always
    /// agree on the key.
    /// </summary>
    public static void SanitizeVaultData(VaultRoot vaultData)
    {
        // Null-tolerant: import paths null-check vaultData after this call
        if (vaultData?.Persons == null)
        {
            return;
        }

        foreach (var person in vaultData.Persons)
        {
            person.Custom = Rekey(person.Custom);

            if (person.Contracts == null)
            {
                continue;
            }

            foreach (var contract in person.Contracts)
            {
                contract.Custom = Rekey(contract.Custom);
            }
        }
    }

    private static Dictionary<string, object?>? Rekey(Dictionary<string, object?>? custom)
    {
        if (custom == null || custom.Count == 0)
        {
            return custom;
        }

        var result = new Dictionary<string, object?>(custom.Count, StringComparer.Ordinal);

        foreach (var (key, value) in custom)
        {
            var safe = Sanitize(key);
            var candidate = safe;
            var n = 2;

            while (result.ContainsKey(candidate))
            {
                candidate = $"{safe}_{n++}";
            }

            result[candidate] = value;
        }

        return result;
    }
}
