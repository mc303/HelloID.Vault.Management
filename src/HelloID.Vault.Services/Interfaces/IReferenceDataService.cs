using HelloID.Vault.Core.Models.Entities;

namespace HelloID.Vault.Services.Interfaces;

public interface IReferenceDataService
{
    // Locations
    Task<IEnumerable<Location>> GetLocationsAsync();
    Task<Location?> GetLocationByIdAsync(string externalId, string source);
    Task CreateLocationAsync(Location location);
    Task UpdateLocationAsync(Location location);
    Task DeleteLocationAsync(string externalId, string source);

    // Titles
    Task<IEnumerable<Title>> GetTitlesAsync();
    Task<Title?> GetTitleByIdAsync(string externalId, string source);
    Task CreateTitleAsync(Title title);
    Task UpdateTitleAsync(Title title);
    Task DeleteTitleAsync(string externalId, string source);

    // Departments
    Task<IEnumerable<Department>> GetDepartmentsAsync();
    Task<Department?> GetDepartmentByIdAsync(string externalId, string source);
    Task CreateDepartmentAsync(Department department);
    Task UpdateDepartmentAsync(Department department);
    Task DeleteDepartmentAsync(string externalId, string source);

    // Divisions
    Task<IEnumerable<Division>> GetDivisionsAsync();
    Task<Division?> GetDivisionByIdAsync(string externalId, string source);
    Task CreateDivisionAsync(Division division);
    Task UpdateDivisionAsync(Division division);
    Task DeleteDivisionAsync(string externalId, string source);

    // Teams
    Task<IEnumerable<Team>> GetTeamsAsync();
    Task<Team?> GetTeamByIdAsync(string externalId, string source);
    Task CreateTeamAsync(Team team);
    Task UpdateTeamAsync(Team team);
    Task DeleteTeamAsync(string externalId, string source);

    // Organizations
    Task<IEnumerable<Organization>> GetOrganizationsAsync();
    Task<Organization?> GetOrganizationByIdAsync(string externalId, string source);
    Task CreateOrganizationAsync(Organization organization);
    Task UpdateOrganizationAsync(Organization organization);
    Task DeleteOrganizationAsync(string externalId, string source);

    // Employers
    Task<IEnumerable<Employer>> GetEmployersAsync();
    Task<Employer?> GetEmployerByIdAsync(string externalId, string source);
    Task CreateEmployerAsync(Employer employer);
    Task UpdateEmployerAsync(Employer employer);
    Task DeleteEmployerAsync(string externalId, string source);

    // Cost Centers
    Task<IEnumerable<CostCenter>> GetCostCentersAsync();
    Task<CostCenter?> GetCostCenterByIdAsync(string externalId, string source);
    Task CreateCostCenterAsync(CostCenter costCenter);
    Task UpdateCostCenterAsync(CostCenter costCenter);
    Task DeleteCostCenterAsync(string externalId, string source);

    // Cost Bearers
    Task<IEnumerable<CostBearer>> GetCostBearersAsync();
    Task<CostBearer?> GetCostBearerByIdAsync(string externalId, string source);
    Task CreateCostBearerAsync(CostBearer costBearer);
    Task UpdateCostBearerAsync(CostBearer costBearer);
    Task DeleteCostBearerAsync(string externalId, string source);

    // Persons
    Task<IEnumerable<Person>> GetPersonsAsync();

    // Cache Management
    /// <summary>
    /// Warms up the cache by loading all lookup data in the background.
    /// Call this on application startup to ensure first dropdown use is fast.
    /// </summary>
    Task WarmupCacheAsync();

    /// <summary>
    /// Clears all lookup data from cache.
    /// Call this after bulk imports or when you need to force a refresh.
    /// </summary>
    void ClearCache();

    /// <summary>
    /// Refreshes the Persons cache specifically.
    /// Call this after adding/editing persons to immediately see changes in dropdowns.
    /// </summary>
    void RefreshPersonsCache();
}
