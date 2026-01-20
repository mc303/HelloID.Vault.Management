using HelloID.Vault.Core.Models.Entities;
using HelloID.Vault.Core.Models.DTOs;

namespace HelloID.Vault.Data.Repositories.Interfaces;

/// <summary>
/// Repository interface for Contact entity operations.
/// </summary>
public interface IContactRepository
{
    /// <summary>
    /// Gets all contacts for a specific person.
    /// </summary>
    Task<IEnumerable<ContactDto>> GetByPersonIdAsync(string personId);

    /// <summary>
    /// Gets a contact by its ID.
    /// </summary>
    Task<Contact?> GetByIdAsync(int contactId);

    /// <summary>
    /// Inserts a new contact.
    /// </summary>
    Task<int> InsertAsync(Contact contact);

    /// <summary>
    /// Inserts a new contact using the specified connection and transaction.
    /// </summary>
    Task<int> InsertAsync(Contact contact, System.Data.IDbConnection connection, System.Data.IDbTransaction transaction);

    /// <summary>
    /// Updates an existing contact.
    /// </summary>
    Task<int> UpdateAsync(Contact contact);

    /// <summary>
    /// Deletes a contact by ID.
    /// </summary>
    Task<int> DeleteAsync(int contactId);

    /// <summary>
    /// Gets all contacts (paged).
    /// </summary>
    Task<IEnumerable<ContactDto>> GetPagedAsync(int page, int pageSize);

    /// <summary>
    /// Gets the total count of contacts.
    /// </summary>
    Task<int> GetCountAsync();

    /// <summary>
    /// Gets all contacts with person display names.
    /// </summary>
    Task<IEnumerable<ContactDto>> GetAllAsync();
}
