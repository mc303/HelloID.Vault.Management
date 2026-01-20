using HelloID.Vault.Core.Models.Entities;
using HelloID.Vault.Core.Models.DTOs;
using HelloID.Vault.Core.Models.Filters;

namespace HelloID.Vault.Services.Interfaces;

/// <summary>
/// Service interface for Person-related business logic.
/// </summary>
public interface IPersonService
{
    /// <summary>
    /// Gets a paged list of persons with total count.
    /// </summary>
    Task<(IEnumerable<PersonListDto> Items, int TotalCount)> GetPagedAsync(PersonFilter filter, int page, int pageSize);

    /// <summary>
    /// Gets a person by their ID with full details.
    /// </summary>
    Task<Person?> GetByIdAsync(string personId);

    /// <summary>
    /// Gets complete person details including primary contract and business contact from person_details_view.
    /// </summary>
    Task<PersonDetailDto?> GetPersonDetailAsync(string personId);

    /// <summary>
    /// Gets a person by their external ID.
    /// </summary>
    Task<Person?> GetByExternalIdAsync(string externalId);

    /// <summary>
    /// Creates a new person.
    /// </summary>
    Task<string> CreateAsync(Person person);

    /// <summary>
    /// Updates an existing person.
    /// </summary>
    Task UpdateAsync(Person person);

    /// <summary>
    /// Deletes a person by ID.
    /// </summary>
    Task DeleteAsync(string personId);

    /// <summary>
    /// Searches persons by term.
    /// </summary>
    Task<IEnumerable<PersonListDto>> SearchAsync(string searchTerm, int maxResults = 50);

    /// <summary>
    /// Gets custom field values for a person.
    /// </summary>
    Task<IEnumerable<CustomFieldDto>> GetCustomFieldsAsync(string personId);

    /// <summary>
    /// Gets all contracts for a person with full details.
    /// </summary>
    Task<IEnumerable<ContractDetailDto>> GetContractsByPersonIdAsync(string personId);

    /// <summary>
    /// Gets all contacts for a person.
    /// </summary>
    Task<IEnumerable<ContactDto>> GetContactsByPersonIdAsync(string personId);

    /// <summary>
    /// Gets a person with the most contracts, skipping the specified number of persons.
    /// Used for cycling through test persons for preview functionality.
    /// </summary>
    Task<PersonDetailDto?> GetPersonWithMostContractsAsync(int skip = 0);

    /// <summary>
    /// Runs the primary contract selection algorithm with step-by-step breakdown for preview.
    /// </summary>
    Task<PrimaryContractPreviewResult?> PreviewPrimaryContractAsync(string personId);
}
