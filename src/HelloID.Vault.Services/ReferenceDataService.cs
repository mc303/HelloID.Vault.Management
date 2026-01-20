using HelloID.Vault.Core.Models.Entities;
using HelloID.Vault.Data.Repositories.Interfaces;
using HelloID.Vault.Services.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace HelloID.Vault.Services;

public class ReferenceDataService : IReferenceDataService
{
    private readonly ILocationRepository _locationRepository;
    private readonly ITitleRepository _titleRepository;
    private readonly IDepartmentRepository _departmentRepository;
    private readonly IDivisionRepository _divisionRepository;
    private readonly ITeamRepository _teamRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IEmployerRepository _employerRepository;
    private readonly ICostCenterRepository _costCenterRepository;
    private readonly ICostBearerRepository _costBearerRepository;
    private readonly IPersonRepository _personRepository;
    private readonly IMemoryCache _cache;
    private readonly MemoryCacheEntryOptions _cacheOptions;

    private const string CacheKeyLocations = "Locations";
    private const string CacheKeyTitles = "Titles";
    private const string CacheKeyDepartments = "Departments";
    private const string CacheKeyDivisions = "Divisions";
    private const string CacheKeyTeams = "Teams";
    private const string CacheKeyOrganizations = "Organizations";
    private const string CacheKeyEmployers = "Employers";
    private const string CacheKeyCostCenters = "CostCenters";
    private const string CacheKeyCostBearers = "CostBearers";
    private const string CacheKeyPersons = "Persons";

    public ReferenceDataService(
        ILocationRepository locationRepository,
        ITitleRepository titleRepository,
        IDepartmentRepository departmentRepository,
        IDivisionRepository divisionRepository,
        ITeamRepository teamRepository,
        IOrganizationRepository organizationRepository,
        IEmployerRepository employerRepository,
        ICostCenterRepository costCenterRepository,
        ICostBearerRepository costBearerRepository,
        IPersonRepository personRepository,
        IMemoryCache cache)
    {
        _locationRepository = locationRepository;
        _titleRepository = titleRepository;
        _departmentRepository = departmentRepository;
        _divisionRepository = divisionRepository;
        _teamRepository = teamRepository;
        _organizationRepository = organizationRepository;
        _employerRepository = employerRepository;
        _costCenterRepository = costCenterRepository;
        _costBearerRepository = costBearerRepository;
        _personRepository = personRepository;
        _cache = cache;

        // 5-minute sliding expiration - cache expires if not accessed for 5 minutes
        _cacheOptions = new MemoryCacheEntryOptions()
            .SetSlidingExpiration(TimeSpan.FromMinutes(5));
    }

    // Locations
    public async Task<IEnumerable<Location>> GetLocationsAsync()
    {
        return await _cache.GetOrCreateAsync(CacheKeyLocations, async entry =>
        {
            entry.SetOptions(_cacheOptions);
            return await _locationRepository.GetAllAsync();
        });
    }
    public async Task<Location?> GetLocationByIdAsync(string externalId, string source) => await _locationRepository.GetByIdAsync(externalId, source);
    public async Task CreateLocationAsync(Location location)
    {
        await _locationRepository.InsertAsync(location);
        _cache.Remove(CacheKeyLocations);
    }
    public async Task UpdateLocationAsync(Location location)
    {
        await _locationRepository.UpdateAsync(location);
        _cache.Remove(CacheKeyLocations);
    }
    public async Task DeleteLocationAsync(string externalId, string source)
    {
        await _locationRepository.DeleteAsync(externalId, source);
        _cache.Remove(CacheKeyLocations);
    }

    // Titles
    public async Task<IEnumerable<Title>> GetTitlesAsync()
    {
        return await _cache.GetOrCreateAsync(CacheKeyTitles, async entry =>
        {
            entry.SetOptions(_cacheOptions);
            return await _titleRepository.GetAllAsync();
        });
    }
    public async Task<Title?> GetTitleByIdAsync(string externalId, string source) => await _titleRepository.GetByIdAsync(externalId, source);
    public async Task CreateTitleAsync(Title title)
    {
        await _titleRepository.InsertAsync(title);
        _cache.Remove(CacheKeyTitles);
    }
    public async Task UpdateTitleAsync(Title title)
    {
        await _titleRepository.UpdateAsync(title);
        _cache.Remove(CacheKeyTitles);
    }
    public async Task DeleteTitleAsync(string externalId, string source)
    {
        await _titleRepository.DeleteAsync(externalId, source);
        _cache.Remove(CacheKeyTitles);
    }
    public async Task<IEnumerable<Department>> GetDepartmentsAsync()
    {
        return await _cache.GetOrCreateAsync(CacheKeyDepartments, async entry =>
        {
            entry.SetOptions(_cacheOptions);
            var dtos = await _departmentRepository.GetAllAsync();
            return dtos.Select(d => new Department
            {
                ExternalId = d.ExternalId,
                DisplayName = d.DisplayName,
                Code = d.Code,
                ParentExternalId = d.ParentExternalId,
                ManagerPersonId = d.ManagerPersonId,
                Source = d.Source
            });
        });
    }
    public async Task<Department?> GetDepartmentByIdAsync(string externalId, string source) => await _departmentRepository.GetByIdAsync(externalId, source);
    public async Task CreateDepartmentAsync(Department department)
    {
        await _departmentRepository.InsertAsync(department);
        _cache.Remove(CacheKeyDepartments);
    }
    public async Task UpdateDepartmentAsync(Department department)
    {
        await _departmentRepository.UpdateAsync(department);
        _cache.Remove(CacheKeyDepartments);
    }
    public async Task DeleteDepartmentAsync(string externalId, string source)
    {
        await _departmentRepository.DeleteAsync(externalId, source);
        _cache.Remove(CacheKeyDepartments);
    }

    // Divisions
    public async Task<IEnumerable<Division>> GetDivisionsAsync()
    {
        return await _cache.GetOrCreateAsync(CacheKeyDivisions, async entry =>
        {
            entry.SetOptions(_cacheOptions);
            return await _divisionRepository.GetAllAsync();
        });
    }
    public async Task<Division?> GetDivisionByIdAsync(string externalId, string source) => await _divisionRepository.GetByIdAsync(externalId, source);
    public async Task CreateDivisionAsync(Division division)
    {
        await _divisionRepository.InsertAsync(division);
        _cache.Remove(CacheKeyDivisions);
    }
    public async Task UpdateDivisionAsync(Division division)
    {
        await _divisionRepository.UpdateAsync(division);
        _cache.Remove(CacheKeyDivisions);
    }
    public async Task DeleteDivisionAsync(string externalId, string source)
    {
        await _divisionRepository.DeleteAsync(externalId, source);
        _cache.Remove(CacheKeyDivisions);
    }

    // Teams
    public async Task<IEnumerable<Team>> GetTeamsAsync()
    {
        return await _cache.GetOrCreateAsync(CacheKeyTeams, async entry =>
        {
            entry.SetOptions(_cacheOptions);
            return await _teamRepository.GetAllAsync();
        });
    }
    public async Task<Team?> GetTeamByIdAsync(string externalId, string source) => await _teamRepository.GetByIdAsync(externalId, source);
    public async Task CreateTeamAsync(Team team)
    {
        await _teamRepository.InsertAsync(team);
        _cache.Remove(CacheKeyTeams);
    }
    public async Task UpdateTeamAsync(Team team)
    {
        await _teamRepository.UpdateAsync(team);
        _cache.Remove(CacheKeyTeams);
    }
    public async Task DeleteTeamAsync(string externalId, string source)
    {
        await _teamRepository.DeleteAsync(externalId, source);
        _cache.Remove(CacheKeyTeams);
    }

    // Organizations
    public async Task<IEnumerable<Organization>> GetOrganizationsAsync()
    {
        return await _cache.GetOrCreateAsync(CacheKeyOrganizations, async entry =>
        {
            entry.SetOptions(_cacheOptions);
            return await _organizationRepository.GetAllAsync();
        });
    }
    public async Task<Organization?> GetOrganizationByIdAsync(string externalId, string source) => await _organizationRepository.GetByIdAsync(externalId, source);
    public async Task CreateOrganizationAsync(Organization organization)
    {
        await _organizationRepository.InsertAsync(organization);
        _cache.Remove(CacheKeyOrganizations);
    }
    public async Task UpdateOrganizationAsync(Organization organization)
    {
        await _organizationRepository.UpdateAsync(organization);
        _cache.Remove(CacheKeyOrganizations);
    }
    public async Task DeleteOrganizationAsync(string externalId, string source)
    {
        await _organizationRepository.DeleteAsync(externalId, source);
        _cache.Remove(CacheKeyOrganizations);
    }

    // Employers
    public async Task<IEnumerable<Employer>> GetEmployersAsync()
    {
        return await _cache.GetOrCreateAsync(CacheKeyEmployers, async entry =>
        {
            entry.SetOptions(_cacheOptions);
            return await _employerRepository.GetAllAsync();
        });
    }
    public async Task<Employer?> GetEmployerByIdAsync(string externalId, string source) => await _employerRepository.GetByIdAsync(externalId, source);
    public async Task CreateEmployerAsync(Employer employer)
    {
        await _employerRepository.InsertAsync(employer);
        _cache.Remove(CacheKeyEmployers);
    }
    public async Task UpdateEmployerAsync(Employer employer)
    {
        await _employerRepository.UpdateAsync(employer);
        _cache.Remove(CacheKeyEmployers);
    }
    public async Task DeleteEmployerAsync(string externalId, string source)
    {
        await _employerRepository.DeleteAsync(externalId, source);
        _cache.Remove(CacheKeyEmployers);
    }

    // Cost Centers
    public async Task<IEnumerable<CostCenter>> GetCostCentersAsync()
    {
        return await _cache.GetOrCreateAsync(CacheKeyCostCenters, async entry =>
        {
            entry.SetOptions(_cacheOptions);
            return await _costCenterRepository.GetAllAsync();
        });
    }
    public async Task<CostCenter?> GetCostCenterByIdAsync(string externalId, string source) => await _costCenterRepository.GetByIdAsync(externalId, source);
    public async Task CreateCostCenterAsync(CostCenter costCenter)
    {
        await _costCenterRepository.InsertAsync(costCenter);
        _cache.Remove(CacheKeyCostCenters);
    }
    public async Task UpdateCostCenterAsync(CostCenter costCenter)
    {
        await _costCenterRepository.UpdateAsync(costCenter);
        _cache.Remove(CacheKeyCostCenters);
    }
    public async Task DeleteCostCenterAsync(string externalId, string source)
    {
        await _costCenterRepository.DeleteAsync(externalId, source);
        _cache.Remove(CacheKeyCostCenters);
    }

    // Cost Bearers
    public async Task<IEnumerable<CostBearer>> GetCostBearersAsync()
    {
        return await _cache.GetOrCreateAsync(CacheKeyCostBearers, async entry =>
        {
            entry.SetOptions(_cacheOptions);
            return await _costBearerRepository.GetAllAsync();
        });
    }
    public async Task<CostBearer?> GetCostBearerByIdAsync(string externalId, string source) => await _costBearerRepository.GetByIdAsync(externalId, source);
    public async Task CreateCostBearerAsync(CostBearer costBearer)
    {
        await _costBearerRepository.InsertAsync(costBearer);
        _cache.Remove(CacheKeyCostBearers);
    }
    public async Task UpdateCostBearerAsync(CostBearer costBearer)
    {
        await _costBearerRepository.UpdateAsync(costBearer);
        _cache.Remove(CacheKeyCostBearers);
    }
    public async Task DeleteCostBearerAsync(string externalId, string source)
    {
        await _costBearerRepository.DeleteAsync(externalId, source);
        _cache.Remove(CacheKeyCostBearers);
    }

    // Persons
    public async Task<IEnumerable<Person>> GetPersonsAsync()
    {
        return await _cache.GetOrCreateAsync(CacheKeyPersons, async entry =>
        {
            entry.SetOptions(_cacheOptions);
            return await _personRepository.GetAllAsync();
        });
    }

    /// <summary>
    /// Warms up the cache by loading all lookup data in the background.
    /// Call this on application startup to ensure first dropdown use is fast.
    /// </summary>
    public async Task WarmupCacheAsync()
    {
        // Load all lookup tables in parallel
        var tasks = new List<Task>
        {
            Task.Run(() => GetLocationsAsync()),
            Task.Run(() => GetTitlesAsync()),
            Task.Run(() => GetDepartmentsAsync()),
            Task.Run(() => GetDivisionsAsync()),
            Task.Run(() => GetTeamsAsync()),
            Task.Run(() => GetOrganizationsAsync()),
            Task.Run(() => GetEmployersAsync()),
            Task.Run(() => GetCostCentersAsync()),
            Task.Run(() => GetCostBearersAsync()),
            Task.Run(() => GetPersonsAsync())
        };

        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Clears all lookup data from cache.
    /// Call this after bulk imports or when you need to force a refresh.
    /// </summary>
    public void ClearCache()
    {
        _cache.Remove(CacheKeyLocations);
        _cache.Remove(CacheKeyTitles);
        _cache.Remove(CacheKeyDepartments);
        _cache.Remove(CacheKeyDivisions);
        _cache.Remove(CacheKeyTeams);
        _cache.Remove(CacheKeyOrganizations);
        _cache.Remove(CacheKeyEmployers);
        _cache.Remove(CacheKeyCostCenters);
        _cache.Remove(CacheKeyCostBearers);
        _cache.Remove(CacheKeyPersons);
    }

    /// <summary>
    /// Refreshes the Persons cache specifically.
    /// Call this after adding/editing persons to immediately see changes in dropdowns.
    /// </summary>
    public void RefreshPersonsCache()
    {
        _cache.Remove(CacheKeyPersons);
    }
}
