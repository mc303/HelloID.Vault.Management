using HelloID.Vault.Core.Models.Entities;
using HelloID.Vault.Core.Models.Json;

namespace HelloID.Vault.Services.Import.Mappers;

/// <summary>
/// Maps VaultDepartmentReference objects to Department entities during import.
/// </summary>
public static class DepartmentMapper
{
    /// <summary>
    /// Maps a VaultDepartmentReference to a Department entity, including source lookup and manager reference resolution.
    /// </summary>
    public static Department Map(VaultDepartmentReference deptRef, Dictionary<string, string> sourceLookup)
    {
        // Convert null UUID to actual NULL
        // Check for: "00000000-0000-0000-0000-000000000000", empty string, or null
        string? managerPersonId = null;
        if (!string.IsNullOrWhiteSpace(deptRef.Manager?.PersonId) &&
            deptRef.Manager.PersonId != "00000000-0000-0000-0000-000000000000")
        {
            managerPersonId = deptRef.Manager.PersonId;
        }

        // Get source from lookup, or null if not found
        string? sourceId = null;
        if (deptRef.Source?.SystemId != null && sourceLookup.TryGetValue(deptRef.Source.SystemId, out var mappedSourceId))
        {
            sourceId = mappedSourceId;
        }

        // Apply hash transformation to ExternalId for namespace isolation
        string transformedExternalId = string.Empty;
        if (!string.IsNullOrWhiteSpace(deptRef.ExternalId) && sourceId != null)
        {
            transformedExternalId = deptRef.ExternalId;
        }

        // Also transform ParentExternalId if it exists
        string transformedParentExternalId = string.Empty;
        if (!string.IsNullOrWhiteSpace(deptRef.ParentExternalId) && sourceId != null)
        {
            transformedParentExternalId = deptRef.ParentExternalId;
        }

        return new Department
        {
            ExternalId = transformedExternalId,
            DisplayName = deptRef.DisplayName ?? string.Empty,
            Code = deptRef.Code,
            ParentExternalId = transformedParentExternalId,
            ManagerPersonId = managerPersonId,
            Source = sourceId
        };
    }
}
