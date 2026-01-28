using HelloID.Vault.Core.Models.Entities;
using HelloID.Vault.Core.Models.Json;
using HelloID.Vault.Services.Interfaces;

namespace HelloID.Vault.Services.Import.Mappers;

/// <summary>
/// Maps VaultPerson objects to Person entities during import.
/// </summary>
public static class PersonMapper
{
    /// <summary>
    /// Maps a VaultPerson to a Person entity, including source lookup and primary manager logic.
    /// </summary>
    public static Person Map(VaultPerson vaultPerson, Dictionary<string, string> sourceLookup, PrimaryManagerLogic primaryManagerLogic)
    {
        // Get source from lookup, or null if not found
        string? sourceId = null;
        if (vaultPerson.Source?.SystemId != null && sourceLookup.TryGetValue(vaultPerson.Source.SystemId, out var mappedSourceId))
        {
            sourceId = mappedSourceId;
        }

        // Handle primary manager for FromJson logic
        string? primaryManagerPersonId = null;
        string? primaryManagerSource = null;
        if (primaryManagerLogic == PrimaryManagerLogic.FromJson)
        {
            primaryManagerPersonId = vaultPerson.PrimaryManager?.PersonId;
            primaryManagerSource = "import";
        }

        return new Person
        {
            PersonId = vaultPerson.PersonId,
            DisplayName = vaultPerson.DisplayName,
            ExternalId = vaultPerson.ExternalId,
            UserName = vaultPerson.UserName,
            Gender = vaultPerson.Details?.Gender,
            BirthDate = vaultPerson.Details?.BirthDate?.ToString("yyyy-MM-dd"),
            BirthLocality = vaultPerson.Details?.BirthLocality,
            Initials = vaultPerson.Name?.Initials,
            GivenName = vaultPerson.Name?.GivenName,
            FamilyName = vaultPerson.Name?.FamilyName,
            FamilyNamePrefix = vaultPerson.Name?.FamilyNamePrefix,
            FamilyNamePartner = vaultPerson.Name?.FamilyNamePartner,
            FamilyNamePartnerPrefix = vaultPerson.Name?.FamilyNamePartnerPrefix,
            Convention = vaultPerson.Name?.Convention,
            HonorificPrefix = vaultPerson.Details?.HonorificPrefix,
            HonorificSuffix = vaultPerson.Details?.HonorificSuffix,
            NickName = vaultPerson.Name?.NickName,
            MaritalStatus = vaultPerson.Details?.MaritalStatus,
            Blocked = vaultPerson.Status?.Blocked ?? false,
            StatusReason = vaultPerson.Status?.Reason,
            Excluded = vaultPerson.Excluded,
            HrExcluded = vaultPerson.ExclusionDetails?.Hr ?? false,
            ManualExcluded = vaultPerson.ExclusionDetails?.Manual ?? false,
            Source = sourceId,
            PrimaryManagerPersonId = primaryManagerPersonId,
            PrimaryManagerSource = primaryManagerSource,
            PrimaryManagerUpdatedAt = primaryManagerPersonId != null ? DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") : null
        };
    }
}
