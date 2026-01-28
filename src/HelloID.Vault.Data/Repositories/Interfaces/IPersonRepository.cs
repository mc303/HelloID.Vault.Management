using HelloID.Vault.Core.Models.Entities;
using HelloID.Vault.Core.Models.DTOs;
using HelloID.Vault.Core.Models.Filters;

namespace HelloID.Vault.Data.Repositories.Interfaces;

/// <summary>
/// Repository interface for Person entity operations.
/// </summary>
public interface IPersonRepository
{
    /// <summary>
    /// Gets a paged list of persons based on filter criteria.
    /// </summary>
    Task<IEnumerable<PersonListDto>> GetPagedAsync(PersonFilter filter, int page, int pageSize);

    /// <summary>
    /// Gets the total count of persons matching the filter.
    /// </summary>
    Task<int> GetCountAsync(PersonFilter filter);

    /// <summary>
    /// Gets a person by their ID.
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
    /// Inserts a new person.
    /// </summary>
    Task<int> InsertAsync(Person person);

    /// <summary>
    /// Inserts a new person using specified connection and transaction.
    /// </summary>
    Task<int> InsertAsync(Person person, System.Data.IDbConnection connection, System.Data.IDbTransaction transaction);

    /// <summary>
    /// Updates an existing person (for manual edits, not for import).
    /// </summary>
    Task<int> UpdateAsync(Person person);

    /// <summary>
    /// Deletes a person by ID.
    /// </summary>
    Task<int> DeleteAsync(string personId);

    /// <summary>
    /// Gets all persons (use with caution for large datasets).
    /// </summary>
    Task<IEnumerable<Person>> GetAllAsync();

    /// <summary>
    /// Gets custom field values for a person.
    /// </summary>
    Task<IEnumerable<CustomFieldDto>> GetCustomFieldsAsync(string personId);

    /// <summary>
    /// Gets a person with the most contracts, skipping the specified number of persons.
    /// Used for cycling through test persons for preview functionality.
    /// </summary>
    /// <param name="skip">Number of persons to skip (0 for first, 1 for second, etc.)</param>
    /// <returns>Person detail with contracts, or null if no person found</returns>
    Task<PersonDetailDto?> GetPersonWithMostContractsAsync(int skip = 0);
}
