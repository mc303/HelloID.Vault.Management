using HelloID.Vault.Core.Models.Entities;
using HelloID.Vault.Core.Models.DTOs;
using HelloID.Vault.Core.Models.Filters;
using HelloID.Vault.Data.Repositories.Interfaces;
using HelloID.Vault.Services.Interfaces;
using Dapper;
using HelloID.Vault.Data.Connection;

namespace HelloID.Vault.Services;

/// <summary>
/// Service implementation for Person-related business logic.
/// </summary>
public class PersonService : IPersonService
{
    private readonly IPersonRepository _personRepository;
    private readonly ICustomFieldRepository _customFieldRepository;
    private readonly IDatabaseConnectionFactory _connectionFactory;
    private readonly IPrimaryContractConfigRepository _primaryContractConfigRepository;
    private readonly IContactRepository _contactRepository;
    private readonly PrimaryContractFieldResolver _fieldResolver;

    public PersonService(
        IPersonRepository personRepository,
        ICustomFieldRepository customFieldRepository,
        IDatabaseConnectionFactory connectionFactory,
        IPrimaryContractConfigRepository primaryContractConfigRepository,
        IContactRepository contactRepository)
    {
        _personRepository = personRepository ?? throw new ArgumentNullException(nameof(personRepository));
        _customFieldRepository = customFieldRepository ?? throw new ArgumentNullException(nameof(customFieldRepository));
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _primaryContractConfigRepository = primaryContractConfigRepository ?? throw new ArgumentNullException(nameof(primaryContractConfigRepository));
        _contactRepository = contactRepository ?? throw new ArgumentNullException(nameof(contactRepository));
        _fieldResolver = new PrimaryContractFieldResolver(_customFieldRepository);
    }

    public async Task<(IEnumerable<PersonListDto> Items, int TotalCount)> GetPagedAsync(
        PersonFilter filter, int page, int pageSize)
    {
        var items = await _personRepository.GetPagedAsync(filter, page, pageSize);
        var totalCount = await _personRepository.GetCountAsync(filter);

        return (items, totalCount);
    }

    public async Task<Person?> GetByIdAsync(string personId)
    {
        if (string.IsNullOrWhiteSpace(personId))
        {
            throw new ArgumentException("Person ID cannot be null or empty.", nameof(personId));
        }

        return await _personRepository.GetByIdAsync(personId);
    }

    public async Task<PersonDetailDto?> GetPersonDetailAsync(string personId)
    {
        if (string.IsNullOrWhiteSpace(personId))
        {
            throw new ArgumentException("Person ID cannot be null or empty.", nameof(personId));
        }

        return await _personRepository.GetPersonDetailAsync(personId);
    }

    public async Task<Person?> GetByExternalIdAsync(string externalId)
    {
        if (string.IsNullOrWhiteSpace(externalId))
        {
            throw new ArgumentException("External ID cannot be null or empty.", nameof(externalId));
        }

        return await _personRepository.GetByExternalIdAsync(externalId);
    }

    public async Task<string> CreateAsync(Person person)
    {
        if (person == null)
        {
            throw new ArgumentNullException(nameof(person));
        }

        // Generate new person ID if not provided
        if (string.IsNullOrWhiteSpace(person.PersonId))
        {
            person.PersonId = Guid.NewGuid().ToString();
        }

        await _personRepository.InsertAsync(person);

        return person.PersonId;
    }

    public async Task UpdateAsync(Person person)
    {
        if (person == null)
        {
            throw new ArgumentNullException(nameof(person));
        }

        if (string.IsNullOrWhiteSpace(person.PersonId))
        {
            throw new ArgumentException("Person ID cannot be null or empty.", nameof(person.PersonId));
        }

        await _personRepository.UpdateAsync(person);
    }

    public async Task DeleteAsync(string personId)
    {
        if (string.IsNullOrWhiteSpace(personId))
        {
            throw new ArgumentException("Person ID cannot be null or empty.", nameof(personId));
        }

        await _personRepository.DeleteAsync(personId);
    }

    public async Task<IEnumerable<PersonListDto>> SearchAsync(string searchTerm, int maxResults = 50)
    {
        var filter = new PersonFilter
        {
            SearchTerm = searchTerm
        };

        return await _personRepository.GetPagedAsync(filter, 1, maxResults);
    }

    public async Task<IEnumerable<CustomFieldDto>> GetCustomFieldsAsync(string personId)
    {
        if (string.IsNullOrWhiteSpace(personId))
        {
            throw new ArgumentException("Person ID cannot be null or empty.", nameof(personId));
        }

        return await _personRepository.GetCustomFieldsAsync(personId);
    }

    public async Task<IEnumerable<ContractDetailDto>> GetContractsByPersonIdAsync(string personId)
    {
        if (string.IsNullOrWhiteSpace(personId))
        {
            throw new ArgumentException("Person ID cannot be null or empty.", nameof(personId));
        }

        System.Diagnostics.Debug.WriteLine($"[PersonService] GetContractsByPersonIdAsync START - PersonId: '{personId}' (Length: {personId.Length})");

        using var connection = _connectionFactory.CreateConnection();

        // Query contract_details_view for all contracts of this person
        // Note: The view already includes source_display_name from the source_system join
        var sql = @"
            SELECT
                contract_id AS ContractId,
                external_id AS ExternalId,
                person_id AS PersonId,
                start_date AS StartDate,
                end_date AS EndDate,
                type_code AS TypeCode,
                type_description AS TypeDescription,
                fte AS Fte,
                hours_per_week AS HoursPerWeek,
                percentage AS Percentage,
                sequence AS Sequence,
                person_name AS PersonName,
                person_external_id AS PersonExternalId,
                manager_person_external_id AS ManagerPersonId,
                manager_person_name AS ManagerPersonName,
                location_external_id AS LocationExternalId,
                location_code AS LocationCode,
                location_name AS LocationName,
                cost_center_external_id AS CostCenterExternalId,
                cost_center_code AS CostCenterCode,
                cost_center_name AS CostCenterName,
                cost_bearer_external_id AS CostBearerExternalId,
                cost_bearer_code AS CostBearerCode,
                cost_bearer_name AS CostBearerName,
                employer_external_id AS EmployerExternalId,
                employer_code AS EmployerCode,
                employer_name AS EmployerName,
                team_external_id AS TeamExternalId,
                team_code AS TeamCode,
                team_name AS TeamName,
                department_external_id AS DepartmentExternalId,
                department_name AS DepartmentName,
                department_code AS DepartmentCode,
                department_parent_external_id AS DepartmentParentExternalId,
                department_manager_person_id AS DepartmentManagerPersonId,
                department_manager_name AS DepartmentManagerName,
                department_parent_department_name AS DepartmentParentDepartmentName,
                division_external_id AS DivisionExternalId,
                division_code AS DivisionCode,
                division_name AS DivisionName,
                title_external_id AS TitleExternalId,
                title_code AS TitleCode,
                title_name AS TitleName,
                organization_external_id AS OrganizationExternalId,
                organization_code AS OrganizationCode,
                organization_name AS OrganizationName,
                contract_status AS ContractStatus,
                contract_date_range AS ContractDateRange,
                source AS Source,
                source_display_name AS SourceDisplayName
            FROM contract_details_view
            WHERE person_id = @PersonId
            ORDER BY
                CASE contract_status
                    WHEN 'Active' THEN 1
                    WHEN 'Future' THEN 2
                    WHEN 'Past' THEN 3
                    ELSE 4
                END,
                start_date DESC";

        System.Diagnostics.Debug.WriteLine($"[PersonService] Executing SQL Query for PersonId: '{personId}'");

        List<ContractDetailDto> contractsList;

        try
        {
            var contracts = await connection.QueryAsync<ContractDetailDto>(sql, new { PersonId = personId }).ConfigureAwait(false);
            contractsList = contracts.ToList();

            System.Diagnostics.Debug.WriteLine($"[PersonService] Query SUCCESS - Loaded {contractsList.Count} contracts");
            foreach (var contract in contractsList)
            {
                System.Diagnostics.Debug.WriteLine($"[PersonService]   Contract {contract.ContractId}: ExternalId='{contract.ExternalId}', Status='{contract.ContractStatus}', Location='{contract.LocationName}'");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PersonService] Query FAILED: {ex.GetType().Name} - {ex.Message}");
            if (ex.InnerException != null)
            {
                System.Diagnostics.Debug.WriteLine($"[PersonService]   Inner Exception: {ex.InnerException.Message}");
            }
            System.Diagnostics.Debug.WriteLine($"[PersonService]   Parameter: PersonId='{personId}' (Length: {personId.Length})");
            throw;
        }

        // Calculate primary contract based on configuration
        if (contractsList.Any())
        {
            var primaryContract = await DeterminePrimaryContractAsync(contractsList);
            if (primaryContract != null)
            {
                primaryContract.IsPrimary = true;
            }
        }

        // Load custom fields for each contract
        foreach (var contract in contractsList)
        {
            if (!string.IsNullOrWhiteSpace(contract.ExternalId))
            {
                var customFields = await _customFieldRepository.GetValuesAsync(contract.ExternalId, "contracts");
                var schemas = await _customFieldRepository.GetSchemasAsync("contracts");
                var schemasDict = schemas.ToDictionary(s => s.FieldKey);

                contract.CustomFields = customFields.Select(cf =>
                {
                    var schema = schemasDict.ContainsKey(cf.FieldKey) ? schemasDict[cf.FieldKey] : null;
                    return new CustomFieldDto
                    {
                        FieldKey = cf.FieldKey,
                        DisplayName = schema?.DisplayName ?? cf.FieldKey,
                        Value = cf.TextValue
                    };
                }).ToList();
            }
        }

        return contractsList;
    }

    /// <summary>
    /// Determines the primary contract based on database configuration.
    /// Applies priority ordering from primary_contract_config table.
    /// </summary>
    private async Task<ContractDetailDto?> DeterminePrimaryContractAsync(List<ContractDetailDto> contracts)
    {
        if (!contracts.Any())
            return null;

        // Load active configuration
        var config = await _primaryContractConfigRepository.GetActiveConfigAsync();
        var configList = config.ToList();

        if (!configList.Any())
        {
            // Fallback: if no config, return first contract ordered by status
            return contracts.First();
        }

        // Start with contract status priority (always first, not configurable)
        IOrderedEnumerable<ContractDetailDto> orderedContracts = contracts
            .OrderBy(c => c.ContractStatus switch
            {
                "Active" => 1,
                "Future" => 2,
                "Past" => 3,
                _ => 4
            });

        // Apply each configured field in priority order
        foreach (var configItem in configList.OrderBy(c => c.PriorityOrder))
        {
            orderedContracts = ApplyOrdering(orderedContracts, configItem);
        }

        return orderedContracts.FirstOrDefault();
    }

    /// <summary>
    /// Applies ordering for a specific field based on configuration.
    /// Uses the field resolver to handle both core fields and custom fields dynamically.
    /// </summary>
    private IOrderedEnumerable<ContractDetailDto> ApplyOrdering(
        IOrderedEnumerable<ContractDetailDto> query,
        PrimaryContractConfig config)
    {
        var isDescending = config.SortOrder == "DESC";

        // Use the field resolver to get the value for each contract
        if (isDescending)
        {
            return query.ThenByDescending(c => _fieldResolver.GetFieldValue(c, config.FieldName));
        }
        else
        {
            return query.ThenBy(c => _fieldResolver.GetFieldValue(c, config.FieldName));
        }
    }

    public async Task<IEnumerable<ContactDto>> GetContactsByPersonIdAsync(string personId)
    {
        if (string.IsNullOrWhiteSpace(personId))
        {
            throw new ArgumentException("Person ID cannot be null or empty.", nameof(personId));
        }

        System.Diagnostics.Debug.WriteLine($"[PersonService] GetContactsByPersonIdAsync START - PersonId: '{personId}' (Length: {personId.Length})");

        try
        {
            var contacts = await _contactRepository.GetByPersonIdAsync(personId);
            var contactsList = contacts.ToList();

            System.Diagnostics.Debug.WriteLine($"[PersonService] GetContactsByPersonIdAsync SUCCESS - Loaded {contactsList.Count} contacts");
            foreach (var contact in contactsList)
            {
                System.Diagnostics.Debug.WriteLine($"[PersonService]   Contact {contact.ContactId}: Type='{contact.Type}', Email='{contact.Email}'");
            }

            return contactsList;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PersonService] GetContactsByPersonIdAsync FAILED: {ex.GetType().Name} - {ex.Message}");
            if (ex.InnerException != null)
            {
                System.Diagnostics.Debug.WriteLine($"[PersonService]   Inner Exception: {ex.InnerException.Message}");
            }
            System.Diagnostics.Debug.WriteLine($"[PersonService]   Parameter: PersonId='{personId}' (Length: {personId.Length})");
            throw;
        }
    }

    public async Task<PersonDetailDto?> GetPersonWithMostContractsAsync(int skip = 0)
    {
        return await _personRepository.GetPersonWithMostContractsAsync(skip);
    }

    public async Task<PrimaryContractPreviewResult?> PreviewPrimaryContractAsync(string personId)
    {
        var person = await GetPersonDetailAsync(personId);
        if (person == null)
            return null;

        var contracts = (await GetContractsByPersonIdAsync(personId)).ToList();
        if (!contracts.Any())
            return null;

        var result = await DeterminePrimaryContractWithStepsAsync(contracts);

        return new PrimaryContractPreviewResult
        {
            Person = person,
            WinningContract = result.WinningContract,
            AllContracts = contracts,
            SelectionSteps = result.Steps
        };
    }

    /// <summary>
    /// Determines the primary contract with step-by-step breakdown.
    /// Tracks each ordering step for preview/debugging purposes.
    /// </summary>
    private async Task<(ContractDetailDto? WinningContract, List<PrimaryContractSelectionStep> Steps)> DeterminePrimaryContractWithStepsAsync(
        List<ContractDetailDto> contracts)
    {
        var steps = new List<PrimaryContractSelectionStep>();

        if (!contracts.Any())
        {
            return (null, steps);
        }

        // Load active configuration
        var config = await _primaryContractConfigRepository.GetActiveConfigAsync();
        var configList = config.ToList();

        IOrderedEnumerable<ContractDetailDto> orderedContracts;

        if (!configList.Any())
        {
            // Fallback: if no config, return first contract ordered by status
            orderedContracts = contracts
                .OrderBy(c => c.ContractStatus switch
                {
                    "Active" => 1,
                    "Future" => 2,
                    "Past" => 3,
                    _ => 4
                });

            var winner = orderedContracts.FirstOrDefault();
            steps.Add(new PrimaryContractSelectionStep
            {
                StepNumber = 1,
                FieldName = "Contract Status",
                SortDirection = "Ascending",
                Description = "No configuration - using fallback ordering",
                ContractOrder = orderedContracts.Take(3).Select(c => new ContractSummary
                {
                    ContractId = c.ContractId,
                    ContractStatus = c.ContractStatus,
                    DisplayValue = c.ContractStatus,
                    IsWinner = c.ContractId == winner?.ContractId
                }).ToList()
            });

            return (winner, steps);
        }

        // Step 1: Contract Status Priority (always first, not configurable)
        orderedContracts = contracts
            .OrderBy(c => c.ContractStatus switch
            {
                "Active" => 1,
                "Future" => 2,
                "Past" => 3,
                _ => 4
            });

        var currentWinner = orderedContracts.FirstOrDefault();
        steps.Add(new PrimaryContractSelectionStep
        {
            StepNumber = 1,
            FieldName = "Contract Status",
            SortDirection = "Active > Future > Past",
            Description = "Active contracts prioritized",
            ContractOrder = orderedContracts.Take(3).Select(c => new ContractSummary
            {
                ContractId = c.ContractId,
                ContractStatus = c.ContractStatus,
                DisplayValue = c.ContractStatus,
                IsWinner = c.ContractId == currentWinner?.ContractId
            }).ToList()
        });

        // Apply each configured field in priority order
        int stepNum = 2;
        foreach (var configItem in configList.OrderBy(c => c.PriorityOrder).Where(c => c.IsActive))
        {
            orderedContracts = ApplyOrdering(orderedContracts, configItem);
            var newWinner = orderedContracts.FirstOrDefault();

            var displayName = _fieldResolver.GetDisplayName(configItem.FieldName);
            var sortDirection = configItem.SortOrder == "DESC" ? "Descending" : "Ascending";

            steps.Add(new PrimaryContractSelectionStep
            {
                StepNumber = stepNum++,
                FieldName = displayName,
                SortDirection = sortDirection,
                Description = $"{displayName} ({sortDirection})",
                ContractOrder = orderedContracts.Take(3).Select(c => new ContractSummary
                {
                    ContractId = c.ContractId,
                    ContractStatus = c.ContractStatus,
                    DisplayValue = GetFieldValueDisplay(c, configItem.FieldName),
                    IsWinner = c.ContractId == newWinner?.ContractId
                }).ToList()
            });

            currentWinner = newWinner;
        }

        return (currentWinner, steps);
    }

    /// <summary>
    /// Gets a display value for a contract field for preview purposes.
    /// </summary>
    private string GetFieldValueDisplay(ContractDetailDto contract, string fieldName)
    {
        var value = _fieldResolver.GetFieldValue(contract, fieldName);
        var displayValue = value?.ToString() ?? "-";
        return string.IsNullOrWhiteSpace(displayValue) ? "-" : displayValue;
    }
}
