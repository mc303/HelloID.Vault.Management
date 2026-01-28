using Dapper;
using HelloID.Vault.Core.Models.DTOs;
using HelloID.Vault.Core.Models.Entities;
using HelloID.Vault.Data.Connection;
using HelloID.Vault.Data.Repositories.Interfaces;
using HelloID.Vault.Services.Interfaces;
using ContractDto = HelloID.Vault.Core.Models.DTOs.ContractDetailDto;

namespace HelloID.Vault.Services;

/// <summary>
/// Service implementation for Primary Manager business logic.
/// </summary>
public class PrimaryManagerService : IPrimaryManagerService
{
    private readonly ISqliteConnectionFactory _connectionFactory;
    private readonly IPersonRepository _personRepository;
    private readonly IPrimaryContractConfigRepository _primaryContractConfigRepository;

    public PrimaryManagerService(
        ISqliteConnectionFactory connectionFactory,
        IPersonRepository personRepository,
        IPrimaryContractConfigRepository primaryContractConfigRepository)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _personRepository = personRepository ?? throw new ArgumentNullException(nameof(personRepository));
        _primaryContractConfigRepository = primaryContractConfigRepository ?? throw new ArgumentNullException(nameof(primaryContractConfigRepository));
    }

    public async Task<string?> CalculatePrimaryManagerAsync(string personId, PrimaryManagerLogic logic)
    {
        if (string.IsNullOrWhiteSpace(personId))
        {
            throw new ArgumentException("Person ID cannot be null or empty.", nameof(personId));
        }

        // Get the person's contracts to determine primary contract
        var contracts = await GetContractsWithPrimaryAsync(personId);
        var primaryContract = contracts.FirstOrDefault();

        if (primaryContract == null)
        {
            // No contracts = no primary manager
            return null;
        }

        return logic switch
        {
            PrimaryManagerLogic.ContractBased => CalculateContractBased(primaryContract),
            PrimaryManagerLogic.DepartmentBased => CalculateDepartmentBased(primaryContract),
            PrimaryManagerLogic.FromJson => throw new NotSupportedException("FromJson logic should be handled during import, not calculation"),
            _ => null
        };
    }

    public async Task UpdatePrimaryManagerForPersonAsync(string personId, PrimaryManagerLogic logic)
    {
        if (string.IsNullOrWhiteSpace(personId))
        {
            throw new ArgumentException("Person ID cannot be null or empty.", nameof(personId));
        }

        var managerId = await CalculatePrimaryManagerAsync(personId, logic);
        var source = GetSourceFromLogic(logic);
        var updatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            UPDATE persons
            SET primary_manager_person_id = @PrimaryManagerPersonId,
                primary_manager_source = @PrimaryManagerSource,
                primary_manager_updated_at = @PrimaryManagerUpdatedAt
            WHERE person_id = @PersonId";

        await connection.ExecuteAsync(sql, new
        {
            PersonId = personId,
            PrimaryManagerPersonId = (object?)managerId ?? DBNull.Value,
            PrimaryManagerSource = (object?)source ?? DBNull.Value,
            PrimaryManagerUpdatedAt = updatedAt
        }).ConfigureAwait(false);
    }

    public async Task UpdatePrimaryManagerForDepartmentAsync(string departmentExternalId, string source, PrimaryManagerLogic logic)
    {
        if (string.IsNullOrWhiteSpace(departmentExternalId))
        {
            throw new ArgumentException("Department external ID cannot be null or empty.", nameof(departmentExternalId));
        }

        using var connection = _connectionFactory.CreateConnection();

        // Find all persons whose primary contract has this department
        var sql = @"
            SELECT DISTINCT p.person_id
            FROM persons p
            INNER JOIN contracts c ON c.person_id = p.person_id
            WHERE c.department_external_id = @DepartmentId
            AND c.source = @Source";

        var personIds = await connection.QueryAsync<string>(sql, new
        {
            DepartmentId = departmentExternalId,
            Source = source
        }).ConfigureAwait(false);

        // Update each person's primary manager
        foreach (var personId in personIds)
        {
            await UpdatePrimaryManagerForPersonAsync(personId, logic);
        }
    }

    public async Task<int> RefreshAllPrimaryManagersAsync(PrimaryManagerLogic logic)
    {
        using var connection = _connectionFactory.CreateConnection();

        // Get all person IDs
        var personIds = await connection.QueryAsync<string>(
            "SELECT person_id FROM persons").ConfigureAwait(false);

        var updateCount = 0;

        foreach (var personId in personIds)
        {
            await UpdatePrimaryManagerForPersonAsync(personId, logic);
            updateCount++;
        }

        return updateCount;
    }

    public async Task<Core.Models.DTOs.PrimaryManagerStatisticsDto> GetStatisticsAsync()
    {
        using var connection = _connectionFactory.CreateConnection();

        var totalPersons = await connection.QuerySingleOrDefaultAsync<int>(
            "SELECT COUNT(*) FROM persons").ConfigureAwait(false);

        var personsWithManager = await connection.QuerySingleOrDefaultAsync<int>(
            "SELECT COUNT(*) FROM persons WHERE primary_manager_person_id IS NOT NULL").ConfigureAwait(false);

        var contractBasedCount = await connection.QuerySingleOrDefaultAsync<int>(
            "SELECT COUNT(*) FROM persons WHERE primary_manager_source = 'contract'").ConfigureAwait(false);

        var departmentBasedCount = await connection.QuerySingleOrDefaultAsync<int>(
            "SELECT COUNT(*) FROM persons WHERE primary_manager_source = 'department'").ConfigureAwait(false);

        var fromJsonCount = await connection.QuerySingleOrDefaultAsync<int>(
            "SELECT COUNT(*) FROM persons WHERE primary_manager_source = 'import'").ConfigureAwait(false);

        return new Core.Models.DTOs.PrimaryManagerStatisticsDto
        {
            TotalPersons = totalPersons,
            PersonsWithManager = personsWithManager,
            PersonsWithoutManager = totalPersons - personsWithManager,
            ContractBasedCount = contractBasedCount,
            DepartmentBasedCount = departmentBasedCount,
            FromJsonCount = fromJsonCount
        };
    }

    /// <summary>
    /// Gets all contracts for a person with the primary contract marked.
    /// </summary>
    private async Task<List<ContractDto>> GetContractsWithPrimaryAsync(string personId)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            SELECT
                contract_id AS ContractId,
                external_id AS ExternalId,
                person_id AS PersonId,
                start_date AS StartDate,
                end_date AS EndDate,
                fte AS Fte,
                hours_per_week AS HoursPerWeek,
                sequence AS Sequence,
                manager_person_external_id AS ManagerPersonExternalId,
                department_external_id AS DepartmentExternalId,
                source AS Source,
                department_manager_person_id AS DepartmentManagerPersonId,
                CASE
                    WHEN start_date IS NULL THEN 'No Dates'
                    WHEN start_date > date('now') THEN 'Future'
                    WHEN end_date IS NOT NULL AND end_date < date('now') THEN 'Past'
                    ELSE 'Active'
                END AS ContractStatus
            FROM contract_details_view
            WHERE person_id = @PersonId
            ORDER BY
                CASE
                    WHEN start_date IS NULL THEN 1
                    WHEN start_date > date('now') THEN 2
                    WHEN end_date IS NOT NULL AND end_date < date('now') THEN 3
                    ELSE 0
                END,
                sequence DESC,
                start_date ASC";

        var contracts = await connection.QueryAsync<ContractDto>(sql, new { PersonId = personId }).ConfigureAwait(false);
        var contractsList = contracts.ToList();

        if (!contractsList.Any())
            return contractsList;

        // Mark primary contract
        var primaryContract = await DeterminePrimaryContractAsync(contractsList);
        if (primaryContract != null)
        {
            primaryContract.IsPrimary = true;
        }

        return contractsList;
    }

    /// <summary>
    /// Determines the primary contract based on database configuration.
    /// Same logic as PersonService but simplified version for internal use.
    /// </summary>
    private async Task<ContractDto?> DeterminePrimaryContractAsync(List<ContractDto> contracts)
    {
        if (!contracts.Any())
            return null;

        // Load active configuration
        var config = await _primaryContractConfigRepository.GetActiveConfigAsync();
        var configList = config.ToList();

        if (!configList.Any())
        {
            return contracts.First();
        }

        // Start with contract status priority
        var orderedContracts = contracts
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
    /// </summary>
    private IOrderedEnumerable<ContractDto> ApplyOrdering(
        IOrderedEnumerable<ContractDto> query,
        PrimaryContractConfig config)
    {
        var isDescending = config.SortOrder == "DESC";

        if (isDescending)
        {
            return query.ThenByDescending(c => GetFieldValue(c, config.FieldName));
        }
        else
        {
            return query.ThenBy(c => GetFieldValue(c, config.FieldName));
        }
    }

    /// <summary>
    /// Gets the value of a field for ordering purposes.
    /// </summary>
    private object? GetFieldValue(ContractDto contract, string fieldName)
    {
        return fieldName.ToLowerInvariant() switch
        {
            "fte" => contract.Fte,
            "hoursperweek" => contract.HoursPerWeek,
            "sequence" => contract.Sequence,
            "startdate" => contract.StartDate,
            "enddate" => contract.EndDate ?? "2999-01-01",
            _ => null
        };
    }

    /// <summary>
    /// Contract-Based Logic: Priority = contract manager → null
    /// </summary>
    private string? CalculateContractBased(ContractDto primaryContract)
    {
        // Return contract manager if exists
        if (!string.IsNullOrEmpty(primaryContract.ManagerPersonExternalId))
        {
            return primaryContract.ManagerPersonExternalId;
        }

        // Otherwise, no primary manager
        return null;
    }

    /// <summary>
    /// Department-Based Logic: Priority = department manager → null
    /// Note: Parent department manager support can be added later if needed.
    /// </summary>
    private string? CalculateDepartmentBased(ContractDto primaryContract)
    {
        // Return department manager if exists
        if (!string.IsNullOrEmpty(primaryContract.DepartmentManagerPersonId))
        {
            return primaryContract.DepartmentManagerPersonId;
        }

        // No department manager found
        return null;
    }

    /// <summary>
    /// Converts logic enum to source string for storage.
    /// </summary>
    private static string? GetSourceFromLogic(PrimaryManagerLogic logic)
    {
        return logic switch
        {
            PrimaryManagerLogic.ContractBased => "contract",
            PrimaryManagerLogic.DepartmentBased => "department",
            PrimaryManagerLogic.FromJson => "import",
            _ => null
        };
    }
}
