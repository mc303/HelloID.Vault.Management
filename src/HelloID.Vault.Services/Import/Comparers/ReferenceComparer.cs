using HelloID.Vault.Core.Models.Json;

namespace HelloID.Vault.Services.Import.Comparers;

/// <summary>
/// Equality comparer for VaultReference based on ExternalId and Name.
/// </summary>
public class ReferenceComparer : IEqualityComparer<VaultReference>
{
    public bool Equals(VaultReference? x, VaultReference? y)
    {
        if (x == null || y == null) return false;
        return x.ExternalId == y.ExternalId && x.Name == y.Name;
    }

    public int GetHashCode(VaultReference obj)
    {
        var hash = obj.ExternalId?.GetHashCode() ?? 0;
        if (obj.Name != null)
            hash = HashCode.Combine(hash, obj.Name.GetHashCode());
        return hash;
    }
}
