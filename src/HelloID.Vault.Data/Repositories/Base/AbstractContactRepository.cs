using HelloID.Vault.Core.Models.DTOs;
using HelloID.Vault.Core.Models.Entities;

namespace HelloID.Vault.Data.Repositories.Base;

/// <summary>
/// Contact repository implementation shared by all backends. The SQL lives
/// here once; parameter objects list properties in placeholder-appearance
/// order so both Dapper (name binding) and Turso (index binding) work.
/// </summary>
public abstract class AbstractContactRepository : RepositoryExecutionBase
{
    private const string ContactColumns = @"
                contact_id AS ContactId,
                person_id AS PersonId,
                type AS Type,
                email AS Email,
                phone_mobile AS PhoneMobile,
                phone_fixed AS PhoneFixed,
                address_street AS AddressStreet,
                address_street_ext AS AddressStreetExt,
                address_house_number AS AddressHouseNumber,
                address_house_number_ext AS AddressHouseNumberExt,
                address_postal AS AddressPostal,
                address_locality AS AddressLocality,
                address_country AS AddressCountry";

    public virtual async Task<IEnumerable<ContactDto>> GetByPersonIdAsync(string personId)
    {
        var sql = $@"
            SELECT {ContactColumns}
            FROM contacts
            WHERE person_id = @PersonId
            ORDER BY type";

        return await QueryRowsAsync<ContactDto>(sql, new { PersonId = personId }).ConfigureAwait(false);
    }

    public virtual async Task<Contact?> GetByIdAsync(int contactId)
    {
        var sql = $@"
            SELECT {ContactColumns}
            FROM contacts
            WHERE contact_id = @ContactId";

        return await QueryFirstRowAsync<Contact>(sql, new { ContactId = contactId }).ConfigureAwait(false);
    }

    public virtual async Task<int> InsertAsync(Contact contact)
    {
        var sql = @"
            INSERT INTO contacts (
                person_id, type, email, phone_mobile, phone_fixed,
                address_street, address_street_ext, address_house_number, address_house_number_ext,
                address_postal, address_locality, address_country
            ) VALUES (
                @PersonId, @Type, @Email, @PhoneMobile, @PhoneFixed,
                @AddressStreet, @AddressStreetExt, @AddressHouseNumber, @AddressHouseNumberExt,
                @AddressPostal, @AddressLocality, @AddressCountry
            )";

        // Explicit projection in placeholder order - the entity declares
        // ContactId first, which positional binding cannot use
        return await ExecuteRowsAsync(sql, new
        {
            contact.PersonId,
            contact.Type,
            contact.Email,
            contact.PhoneMobile,
            contact.PhoneFixed,
            contact.AddressStreet,
            contact.AddressStreetExt,
            contact.AddressHouseNumber,
            contact.AddressHouseNumberExt,
            contact.AddressPostal,
            contact.AddressLocality,
            contact.AddressCountry
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Inserts within an existing transaction. Backends without ambient
    /// transactions (Turso) fall back to the plain insert.
    /// </summary>
    public virtual Task<int> InsertAsync(Contact contact, System.Data.IDbConnection connection, System.Data.IDbTransaction transaction)
    {
        return InsertAsync(contact);
    }

    public virtual async Task<int> UpdateAsync(Contact contact)
    {
        var sql = @"
            UPDATE contacts SET
                person_id = @PersonId,
                type = @Type,
                email = @Email,
                phone_mobile = @PhoneMobile,
                phone_fixed = @PhoneFixed,
                address_street = @AddressStreet,
                address_street_ext = @AddressStreetExt,
                address_house_number = @AddressHouseNumber,
                address_house_number_ext = @AddressHouseNumberExt,
                address_postal = @AddressPostal,
                address_locality = @AddressLocality,
                address_country = @AddressCountry
            WHERE contact_id = @ContactId";

        // ContactId binds LAST (WHERE clause) - passing the whole entity bound
        // it first and shifted every column, so updates matched no rows
        return await ExecuteRowsAsync(sql, new
        {
            contact.PersonId,
            contact.Type,
            contact.Email,
            contact.PhoneMobile,
            contact.PhoneFixed,
            contact.AddressStreet,
            contact.AddressStreetExt,
            contact.AddressHouseNumber,
            contact.AddressHouseNumberExt,
            contact.AddressPostal,
            contact.AddressLocality,
            contact.AddressCountry,
            contact.ContactId
        }).ConfigureAwait(false);
    }

    public virtual async Task<int> DeleteAsync(int contactId)
    {
        return await ExecuteRowsAsync(
            "DELETE FROM contacts WHERE contact_id = @ContactId",
            new { ContactId = contactId }).ConfigureAwait(false);
    }

    public virtual async Task<IEnumerable<ContactDto>> GetPagedAsync(int page, int pageSize)
    {
        var sql = $@"
            SELECT {ContactColumns}
            FROM contacts
            ORDER BY contact_id
            LIMIT @Limit OFFSET @Offset";

        return await QueryRowsAsync<ContactDto>(sql, new
        {
            Limit = pageSize,
            Offset = (page - 1) * pageSize
        }).ConfigureAwait(false);
    }

    public virtual async Task<int> GetCountAsync()
    {
        return await ExecuteScalarValueAsync<int>("SELECT COUNT(*) FROM contacts").ConfigureAwait(false);
    }

    public virtual async Task<IEnumerable<ContactDto>> GetAllAsync()
    {
        var sql = $@"
            SELECT
                c.contact_id AS ContactId,
                c.person_id AS PersonId,
                p.display_name AS PersonDisplayName,
                c.type AS Type,
                c.email AS Email,
                c.phone_mobile AS PhoneMobile,
                c.phone_fixed AS PhoneFixed,
                c.address_street AS AddressStreet,
                c.address_street_ext AS AddressStreetExt,
                c.address_house_number AS AddressHouseNumber,
                c.address_house_number_ext AS AddressHouseNumberExt,
                c.address_postal AS AddressPostal,
                c.address_locality AS AddressLocality,
                c.address_country AS AddressCountry
            FROM contacts c
            LEFT JOIN persons p ON c.person_id = p.person_id
            ORDER BY p.display_name, c.type";

        return await QueryRowsAsync<ContactDto>(sql).ConfigureAwait(false);
    }
}
