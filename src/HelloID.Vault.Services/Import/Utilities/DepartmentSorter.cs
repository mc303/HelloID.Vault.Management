using HelloID.Vault.Core.Models.Entities;

namespace HelloID.Vault.Services.Import.Utilities;

/// <summary>
/// Provides utility methods for working with department hierarchies.
/// </summary>
public static class DepartmentSorter
{
    /// <summary>
    /// Performs topological sort on departments to ensure parents are inserted before children.
    /// Uses depth-first traversal with cycle detection.
    /// </summary>
    public static List<Department> TopologicalSort(List<Department> departments)
    {
        var sorted = new List<Department>();
        var visited = new HashSet<string>();
        var visiting = new HashSet<string>(); // For cycle detection
        var deptLookup = departments.ToDictionary(d => d.ExternalId);

        void Visit(Department dept)
        {
            // Skip if already processed
            if (visited.Contains(dept.ExternalId))
                return;

            // Detect cycles
            if (visiting.Contains(dept.ExternalId))
            {
                throw new InvalidOperationException(
                    $"Circular dependency detected in department hierarchy at: {dept.ExternalId} ({dept.DisplayName})");
            }

            visiting.Add(dept.ExternalId);

            // Visit parent first (if exists in our dataset)
            if (!string.IsNullOrEmpty(dept.ParentExternalId) &&
                deptLookup.TryGetValue(dept.ParentExternalId, out var parent))
            {
                Visit(parent);
            }

            visiting.Remove(dept.ExternalId);
            visited.Add(dept.ExternalId);
            sorted.Add(dept);
        }

        // Visit all departments
        foreach (var dept in departments)
        {
            Visit(dept);
        }

        return sorted;
    }
}
