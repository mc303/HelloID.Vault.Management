using Dapper;
using HelloID.Vault.Core.Models.Entities;
using HelloID.Vault.Core.Models.DTOs;
using HelloID.Vault.Data.Connection;
using HelloID.Vault.Data.Repositories.Interfaces;

namespace HelloID.Vault.Data.Repositories;

/// <summary>
/// Repository implementation for Contact entity using Dapper.
/// </summary>
public class ContactRepository : IContactRepository
{
    private readonly ISqliteConnectionFactory _connectionFactory;

    public ContactRepository(ISqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<IEnumerable<ContactDto>> GetByPersonIdAsync(string personId)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            SELECT
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
                address_country AS AddressCountry
            FROM contacts
            WHERE person_id = @PersonId
            ORDER BY type";

        return await connection.QueryAsync<ContactDto>(sql, new { PersonId = personId }).ConfigureAwait(false);
    }

    public async Task<Contact?> GetByIdAsync(int contactId)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            SELECT
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
                address_country AS AddressCountry
            FROM contacts
            WHERE contact_id = @ContactId";

        return await connection.QuerySingleOrDefaultAsync<Contact>(sql, new { ContactId = contactId }).ConfigureAwait(false);
    }

    public async Task<int> InsertAsync(Contact contact)
    {
        using var connection = _connectionFactory.CreateConnection();

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

        return await connection.ExecuteAsync(sql, contact).ConfigureAwait(false);
    }

    public async Task<int> InsertAsync(Contact contact, System.Data.IDbConnection connection, System.Data.IDbTransaction transaction)
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

        return await connection.ExecuteAsync(sql, contact, transaction).ConfigureAwait(false);
    }

    public async Task<int> UpdateAsync(Contact contact)
    {
        using var connection = _connectionFactory.CreateConnection();

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

        return await connection.ExecuteAsync(sql, contact).ConfigureAwait(false);
    }

    public async Task<int> DeleteAsync(int contactId)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = "DELETE FROM contacts WHERE contact_id = @ContactId";

        return await connection.ExecuteAsync(sql, new { ContactId = contactId }).ConfigureAwait(false);
    }

    public async Task<IEnumerable<ContactDto>> GetPagedAsync(int page, int pageSize)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            SELECT
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
                address_country AS AddressCountry
            FROM contacts
            ORDER BY contact_id
            LIMIT @Limit OFFSET @Offset";

        return await connection.QueryAsync<ContactDto>(sql, new
        {
            Limit = pageSize,
            Offset = (page - 1) * pageSize
        }).ConfigureAwait(false);
    }

    public async Task<int> GetCountAsync()
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = "SELECT COUNT(*) FROM contacts";

        return await connection.ExecuteScalarAsync<int>(sql).ConfigureAwait(false);
    }

    public async Task<IEnumerable<ContactDto>> GetAllAsync()
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
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

        return await connection.QueryAsync<ContactDto>(sql).ConfigureAwait(false);
    }
}
