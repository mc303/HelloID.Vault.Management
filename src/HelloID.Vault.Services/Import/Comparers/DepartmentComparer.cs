using HelloID.Vault.Core.Models.Entities;

namespace HelloID.Vault.Services.Import.Comparers;

/// <summary>
/// Equality comparer for Department based on ExternalId.
/// </summary>
public class DepartmentComparer : IEqualityComparer<Department>
{
    public bool Equals(Department? x, Department? y)
    {
        if (x == null || y == null) return false;
        return x.ExternalId == y.ExternalId;
    }

    public int GetHashCode(Department obj)
    {
        return obj.ExternalId?.GetHashCode() ?? 0;
    }
}
