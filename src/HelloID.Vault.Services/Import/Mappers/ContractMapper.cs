using System.Diagnostics;
using HelloID.Vault.Core.Models.Entities;
using HelloID.Vault.Core.Models.Json;
using HelloID.Vault.Services.Import.Models;
using HelloID.Vault.Services.Import.Utilities;

namespace HelloID.Vault.Services.Import.Mappers;

/// <summary>
/// Provides contract mapping functionality for import operations.
/// </summary>
public static class ContractMapper
{
    private static int _contractDebugCounter = 0;

    /// <summary>
    /// Maps a VaultContract to a Contract entity.
    /// </summary>
    public static Contract Map(string personId, VaultContract vaultContract, ContractMappingContext context)
    {
        // Get source from lookup, or null if not found
        string? sourceId = null;
        if (vaultContract.Source?.SystemId != null && context.SourceLookup.TryGetValue(vaultContract.Source.SystemId, out var mappedSourceId))
        {
            sourceId = mappedSourceId;
        }

        // Debug: Log first few contracts to see ExternalId values
        if (_contractDebugCounter++ < 5)
        {
            Debug.WriteLine($"[MapContract] Contract {_contractDebugCounter}: ExternalId={vaultContract.ExternalId}, " +
                         $"Location={vaultContract.Location?.ExternalId ?? "NULL"}, " +
                         $"Department={vaultContract.Department?.ExternalId ?? "NULL"}, " +
                         $"Employer={vaultContract.Employer?.ExternalId ?? "NULL"}, " +
                         $"CostCenter={vaultContract.CostCenter?.ExternalId ?? "NULL"}, " +
                         $"CostBearer={vaultContract.CostBearer?.ExternalId ?? "NULL"}, " +
                         $"Team={vaultContract.Team?.ExternalId ?? "NULL"}, " +
                         $"Division={vaultContract.Division?.ExternalId ?? "NULL"}, " +
                         $"Title={vaultContract.Title?.ExternalId ?? "NULL"}, " +
                         $"Organization={vaultContract.Organization?.ExternalId ?? "NULL"}, " +
                         $"sourceId={sourceId ?? "NULL"}");
        }

        // Resolve manager reference
        var managerPersonId = ResolveManagerReference(
            vaultContract.Manager?.PersonId,
            context,
            vaultContract.ExternalId ?? "",
            personId);

        return new Contract
        {
            PersonId = personId,
            ExternalId = vaultContract.ExternalId,
            StartDate = vaultContract.StartDate?.ToString("yyyy-MM-dd"),
            EndDate = vaultContract.EndDate?.ToString("yyyy-MM-dd"),
            Fte = (double?)vaultContract.Details?.Fte,
            HoursPerWeek = (double?)vaultContract.Details?.HoursPerWeek,
            Percentage = (double?)vaultContract.Details?.Percentage,
            Sequence = vaultContract.Details?.Sequence,
            TypeCode = vaultContract.Type?.Code,
            TypeDescription = vaultContract.Type?.Description,
            LocationExternalId = ReferenceResolver.ResolveReferenceExternalId(vaultContract.Location, sourceId, context.SeenLocations),
            LocationSource = sourceId,
            DepartmentExternalId = !string.IsNullOrWhiteSpace(vaultContract.Department?.ExternalId) && sourceId != null ?
                vaultContract.Department.ExternalId : null,
            DepartmentSource = sourceId,
            CostCenterExternalId = ReferenceResolver.ResolveReferenceExternalId(vaultContract.CostCenter, sourceId, context.SeenCostCenters),
            CostCenterSource = sourceId,
            CostBearerExternalId = ReferenceResolver.ResolveReferenceExternalId(vaultContract.CostBearer, sourceId, context.SeenCostBearers),
            CostBearerSource = sourceId,
            EmployerExternalId = ReferenceResolver.ResolveReferenceExternalId(vaultContract.Employer, sourceId, context.SeenEmployers),
            EmployerSource = sourceId,
            TitleExternalId = ReferenceResolver.ResolveReferenceExternalId(vaultContract.Title, sourceId, context.SeenTitles),
            TitleSource = sourceId,
            TeamExternalId = ReferenceResolver.ResolveReferenceExternalId(vaultContract.Team, sourceId, context.SeenTeams),
            TeamSource = sourceId,
            DivisionExternalId = ReferenceResolver.ResolveReferenceExternalId(vaultContract.Division, sourceId, context.SeenDivisions),
            DivisionSource = sourceId,
            OrganizationExternalId = ReferenceResolver.ResolveReferenceExternalId(vaultContract.Organization, sourceId, context.SeenOrganizations),
            OrganizationSource = sourceId,
            ManagerPersonExternalId = managerPersonId,
            Source = sourceId
        };
    }

    /// <summary>
    /// Resolves the manager reference for a contract, handling empty GUIDs.
    /// </summary>
    private static string? ResolveManagerReference(
        string? managerPersonId,
        ContractMappingContext context,
        string contractExternalId,
        string personId)
    {
        // Check if manager GUID is empty/blank - count and replace with null
        if (managerPersonId == "00000000-0000-0000-0000-000000000000" || string.IsNullOrWhiteSpace(managerPersonId))
        {
            context.Result.EmptyManagerGuidsReplaced++;
            Debug.WriteLine($"[MapContract] Empty manager GUID replaced for contract {contractExternalId} (person {personId})");
            return null;
        }

        return managerPersonId;
    }
}
