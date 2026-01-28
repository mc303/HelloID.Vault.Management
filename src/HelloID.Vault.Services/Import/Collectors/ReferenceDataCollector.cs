using System.Diagnostics;
using HelloID.Vault.Core.Models.Entities;
using HelloID.Vault.Core.Models.Json;
using HelloID.Vault.Services.Import.Mappers;
using HelloID.Vault.Services.Import.Models;

namespace HelloID.Vault.Services.Import.Collectors;

/// <summary>
/// Collects reference data (organizations, locations, employers, etc.) from vault contracts.
/// Handles deduplication by name+source combination and tracks sources for each entity.
/// </summary>
public static class ReferenceDataCollector
{
    /// <summary>
    /// Collects all reference data from vault contracts including departments and lookup tables.
    /// </summary>
    public static ReferenceDataContext Collect(VaultRoot vaultData, Dictionary<string, string> sourceLookup)
    {
        var context = new ReferenceDataContext();

        // Extract departments from root-level Departments array (full data with hierarchy)
        if (vaultData.Departments != null && vaultData.Departments.Any())
        {
            foreach (var deptRef in vaultData.Departments)
            {
                if (!string.IsNullOrWhiteSpace(deptRef.ExternalId))
                {
                    var mappedDept = DepartmentMapper.Map(deptRef, sourceLookup);
                    // Only add departments with a valid source (NOT NULL constraint)
                    if (!string.IsNullOrWhiteSpace(mappedDept.Source))
                    {
                        context.Departments.Add(mappedDept);
                    }
                    else
                    {
                        Console.WriteLine($"WARNING: Skipping department '{deptRef.DisplayName}' ({deptRef.ExternalId}) - no valid source found");
                    }
                }
            }
        }
        else
        {
            // Fallback: If no root-level Departments array, extract from contracts (references only)
            Console.WriteLine("WARNING: No root-level Departments array found in vault.json");
            Console.WriteLine("         Extracting departments from contract references (Code, ParentExternalId, Manager will be NULL)");

            foreach (var vaultPerson in vaultData.Persons)
            {
                foreach (var contract in vaultPerson.Contracts)
                {
                    if (contract.Department?.ExternalId != null)
                    {
                        var mappedDept = DepartmentMapper.Map(contract.Department, sourceLookup);
                        // Only add departments with a valid source (NOT NULL constraint)
                        if (!string.IsNullOrWhiteSpace(mappedDept.Source))
                        {
                            context.Departments.Add(mappedDept);
                        }
                        else
                        {
                            Console.WriteLine($"WARNING: Skipping department '{contract.Department.DisplayName}' ({contract.Department.ExternalId}) - no valid source found");
                        }
                    }
                }
            }
        }

        // Extract other lookup tables from contracts with source tracking
        foreach (var vaultPerson in vaultData.Persons)
        {
            foreach (var contract in vaultPerson.Contracts)
            {
                // Get contract source for inheritance
                string? contractSource = null;
                if (contract.Source?.SystemId != null && sourceLookup.TryGetValue(contract.Source.SystemId, out var sourceId))
                {
                    contractSource = sourceId;
                }

                CollectOrganizations(contract, contractSource, context);
                CollectLocations(contract, contractSource, context);
                CollectEmployers(contract, contractSource, context);
                CollectCostCenters(contract, contractSource, context);
                CollectCostBearers(contract, contractSource, context);
                CollectTeams(contract, contractSource, context);
                CollectDivisions(contract, contractSource, context);
                CollectTitles(contract, contractSource, context);
            }
        }

        return context;
    }

    private static void CollectOrganizations(VaultContract contract, string? contractSource, ReferenceDataContext context)
    {
        if (contract.Organization == null || string.IsNullOrWhiteSpace(contract.Organization.Name))
            return;

        var nameKey = $"{contract.Organization.Name}|{contractSource ?? "default"}";

        // Skip if we've already seen this name+source combination
        if (context.SeenOrganizations.ContainsKey(nameKey))
            return;

        string orgExternalId;
        if (!string.IsNullOrWhiteSpace(contract.Organization.ExternalId))
        {
            orgExternalId = contract.Organization.ExternalId;
        }
        else
        {
            orgExternalId = Guid.NewGuid().ToString();
        }

        var transformedOrganization = new VaultReference
        {
            ExternalId = orgExternalId,
            Code = contract.Organization.Code,
            Name = contract.Organization.Name
        };
        context.SeenOrganizations[nameKey] = transformedOrganization;
        context.Organizations.Add(transformedOrganization);
        if (!context.OrganizationSources.ContainsKey(orgExternalId))
        {
            context.OrganizationSources[orgExternalId] = contractSource;
        }
    }

    private static void CollectLocations(VaultContract contract, string? contractSource, ReferenceDataContext context)
    {
        if (contract.Location == null || string.IsNullOrWhiteSpace(contract.Location.Name))
            return;

        var nameKey = $"{contract.Location.Name}|{contractSource ?? "default"}";

        if (context.SeenLocations.ContainsKey(nameKey))
            return;

        string locationExternalId;
        if (!string.IsNullOrWhiteSpace(contract.Location.ExternalId))
        {
            locationExternalId = contract.Location.ExternalId;
        }
        else
        {
            locationExternalId = Guid.NewGuid().ToString();
        }

        var transformedLocation = new VaultReference
        {
            ExternalId = locationExternalId,
            Code = contract.Location.Code,
            Name = contract.Location.Name
        };
        context.SeenLocations[nameKey] = transformedLocation;
        context.Locations.Add(transformedLocation);
        if (!context.LocationSources.ContainsKey(locationExternalId))
        {
            context.LocationSources[locationExternalId] = contractSource;
        }
    }

    private static void CollectEmployers(VaultContract contract, string? contractSource, ReferenceDataContext context)
    {
        if (contract.Employer == null || string.IsNullOrWhiteSpace(contract.Employer.Name))
            return;

        var nameKey = $"{contract.Employer.Name}|{contractSource ?? "default"}";

        if (context.SeenEmployers.ContainsKey(nameKey))
            return;

        string employerExternalId;
        if (!string.IsNullOrWhiteSpace(contract.Employer.ExternalId))
        {
            employerExternalId = contract.Employer.ExternalId;
        }
        else
        {
            employerExternalId = Guid.NewGuid().ToString();
        }

        var transformedEmployer = new VaultReference
        {
            ExternalId = employerExternalId,
            Code = contract.Employer.Code,
            Name = contract.Employer.Name
        };

        context.SeenEmployers[nameKey] = transformedEmployer;
        context.Employers.Add(transformedEmployer);

        // Track source mapping for the transformed ExternalId
        var employerKey = $"{employerExternalId}|{contractSource}";
        if (!context.EmployerSources.ContainsKey(employerKey))
        {
            context.EmployerSources[employerKey] = contractSource;
        }

        // Also track employer name to source mapping for proper insertion later
        var employerNameKey = $"{employerExternalId}|{contract.Employer.Name}";
        if (!context.EmployerNameToSourceMap.ContainsKey(employerNameKey))
        {
            context.EmployerNameToSourceMap[employerNameKey] = contractSource;
        }
    }

    private static void CollectCostCenters(VaultContract contract, string? contractSource, ReferenceDataContext context)
    {
        if (contract.CostCenter == null || string.IsNullOrWhiteSpace(contract.CostCenter.Name))
            return;

        var nameKey = $"{contract.CostCenter.Name}|{contractSource ?? "default"}";

        if (context.SeenCostCenters.ContainsKey(nameKey))
            return;

        string costCenterExternalId;
        if (!string.IsNullOrWhiteSpace(contract.CostCenter.ExternalId))
        {
            costCenterExternalId = contract.CostCenter.ExternalId;
        }
        else
        {
            costCenterExternalId = Guid.NewGuid().ToString();
        }

        var transformedCostCenter = new VaultReference
        {
            ExternalId = costCenterExternalId,
            Code = contract.CostCenter.Code,
            Name = contract.CostCenter.Name
        };
        context.SeenCostCenters[nameKey] = transformedCostCenter;
        context.CostCenters.Add(transformedCostCenter);
        if (!context.CostCenterSources.ContainsKey(costCenterExternalId))
        {
            context.CostCenterSources[costCenterExternalId] = contractSource;
        }
    }

    private static void CollectCostBearers(VaultContract contract, string? contractSource, ReferenceDataContext context)
    {
        if (contract.CostBearer == null || string.IsNullOrWhiteSpace(contract.CostBearer.Name))
            return;

        var nameKey = $"{contract.CostBearer.Name}|{contractSource ?? "default"}";

        if (context.SeenCostBearers.ContainsKey(nameKey))
            return;

        string costBearerExternalId;
        if (!string.IsNullOrWhiteSpace(contract.CostBearer.ExternalId))
        {
            costBearerExternalId = contract.CostBearer.ExternalId;
        }
        else
        {
            costBearerExternalId = Guid.NewGuid().ToString();
        }

        var transformedCostBearer = new VaultReference
        {
            ExternalId = costBearerExternalId,
            Code = contract.CostBearer.Code,
            Name = contract.CostBearer.Name
        };
        context.SeenCostBearers[nameKey] = transformedCostBearer;
        context.CostBearers.Add(transformedCostBearer);
        if (!context.CostBearerSources.ContainsKey(costBearerExternalId))
        {
            context.CostBearerSources[costBearerExternalId] = contractSource;
        }
    }

    private static void CollectTeams(VaultContract contract, string? contractSource, ReferenceDataContext context)
    {
        if (contract.Team == null || string.IsNullOrWhiteSpace(contract.Team.Name))
            return;

        var nameKey = $"{contract.Team.Name}|{contractSource ?? "default"}";

        if (context.SeenTeams.ContainsKey(nameKey))
            return;

        string teamExternalId;
        if (!string.IsNullOrWhiteSpace(contract.Team.ExternalId))
        {
            teamExternalId = contract.Team.ExternalId;
        }
        else
        {
            teamExternalId = Guid.NewGuid().ToString();
        }

        var transformedTeam = new VaultReference
        {
            ExternalId = teamExternalId,
            Code = contract.Team.Code,
            Name = contract.Team.Name
        };
        context.SeenTeams[nameKey] = transformedTeam;
        context.Teams.Add(transformedTeam);
        if (!context.TeamSources.ContainsKey(teamExternalId))
        {
            context.TeamSources[teamExternalId] = contractSource;
        }
    }

    private static void CollectDivisions(VaultContract contract, string? contractSource, ReferenceDataContext context)
    {
        if (contract.Division == null || string.IsNullOrWhiteSpace(contract.Division.Name))
            return;

        var nameKey = $"{contract.Division.Name}|{contractSource ?? "default"}";

        if (context.SeenDivisions.ContainsKey(nameKey))
            return;

        string divisionExternalId;
        if (!string.IsNullOrWhiteSpace(contract.Division.ExternalId))
        {
            divisionExternalId = contract.Division.ExternalId;
        }
        else
        {
            divisionExternalId = Guid.NewGuid().ToString();
        }

        var transformedDivision = new VaultReference
        {
            ExternalId = divisionExternalId,
            Code = contract.Division.Code,
            Name = contract.Division.Name
        };
        context.SeenDivisions[nameKey] = transformedDivision;
        context.Divisions.Add(transformedDivision);
        if (!context.DivisionSources.ContainsKey(divisionExternalId))
        {
            context.DivisionSources[divisionExternalId] = contractSource;
        }
    }

    private static void CollectTitles(VaultContract contract, string? contractSource, ReferenceDataContext context)
    {
        if (contract.Title == null || string.IsNullOrWhiteSpace(contract.Title.Name))
            return;

        var nameKey = $"{contract.Title.Name}|{contractSource ?? "default"}";

        if (context.SeenTitles.ContainsKey(nameKey))
            return;

        string titleExternalId;
        if (!string.IsNullOrWhiteSpace(contract.Title.ExternalId))
        {
            titleExternalId = contract.Title.ExternalId;
        }
        else
        {
            titleExternalId = Guid.NewGuid().ToString();
        }

        var transformedTitle = new VaultReference
        {
            ExternalId = titleExternalId,
            Code = contract.Title.Code,
            Name = contract.Title.Name
        };
        context.SeenTitles[nameKey] = transformedTitle;
        context.Titles.Add(transformedTitle);
        if (!context.TitleSources.ContainsKey(titleExternalId))
        {
            context.TitleSources[titleExternalId] = contractSource;
        }
    }
}
