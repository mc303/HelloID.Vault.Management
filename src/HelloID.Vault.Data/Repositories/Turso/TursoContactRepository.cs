using System.Data;
using System.Diagnostics;
using HelloID.Vault.Core.Models.Entities;
using HelloID.Vault.Core.Models.DTOs;
using HelloID.Vault.Core.Models.Filters;
using HelloID.Vault.Data.Connection;
using HelloID.Vault.Data.Repositories.Interfaces;

namespace HelloID.Vault.Data.Repositories.Turso;

public class TursoContactRepository : IContactRepository
{
    private readonly ITursoClient _client;

    public TursoContactRepository(ITursoClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    public async Task<IEnumerable<ContactDto>> GetByPersonIdAsync(string personId)
    {
        Debug.WriteLine($"[TursoContactRepository] GetByPersonIdAsync: {personId}");
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
            WHERE person_id = ?
            ORDER BY type";
        var result = await _client.QueryAsync<ContactDto>(sql, new { personId });
        return result.Rows;
    }

    public async Task<Contact?> GetByIdAsync(int contactId)
    {
        Debug.WriteLine($"[TursoContactRepository] GetByIdAsync: {contactId}");
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
            WHERE contact_id = ?";
        return await _client.QueryFirstOrDefaultAsync<Contact>(sql, new { contactId });
    }

    public async Task<int> InsertAsync(Contact contact)
    {
        Debug.WriteLine($"[TursoContactRepository] InsertAsync: {contact.PersonId}");
        var sql = @"
            INSERT INTO contacts (
                person_id, type, email, phone_mobile, phone_fixed,
                address_street, address_street_ext, address_house_number, address_house_number_ext,
                address_postal, address_locality, address_country
            ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)";
        return await _client.ExecuteAsync(sql, contact);
    }

    public async Task<int> InsertAsync(Contact contact, IDbConnection connection, IDbTransaction transaction)
    {
        return await InsertAsync(contact);
    }

    public async Task<int> UpdateAsync(Contact contact)
    {
        Debug.WriteLine($"[TursoContactRepository] UpdateAsync: {contact.ContactId}");
        var sql = @"
            UPDATE contacts SET
                person_id = ?,
                type = ?,
                email = ?,
                phone_mobile = ?,
                phone_fixed = ?,
                address_street = ?,
                address_street_ext = ?,
                address_house_number = ?,
                address_house_number_ext = ?,
                address_postal = ?,
                address_locality = ?,
                address_country = ?
            WHERE contact_id = ?";
        return await _client.ExecuteAsync(sql, contact);
    }

    public async Task<int> DeleteAsync(int contactId)
    {
        Debug.WriteLine($"[TursoContactRepository] DeleteAsync: {contactId}");
        var sql = "DELETE FROM contacts WHERE contact_id = ?";
        return await _client.ExecuteAsync(sql, new { contactId });
    }

    public async Task<IEnumerable<ContactDto>> GetPagedAsync(int page, int pageSize)
    {
        Debug.WriteLine($"[TursoContactRepository] GetPagedAsync: page={page}");
        var offset = (page - 1) * pageSize;
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
            LIMIT ? OFFSET ?";
        var result = await _client.QueryAsync<ContactDto>(sql, new { Limit = pageSize, Offset = offset });
        return result.Rows;
    }

    public async Task<int> GetCountAsync()
    {
        Debug.WriteLine("[TursoContactRepository] GetCountAsync");
        var sql = "SELECT COUNT(*) AS Count FROM contacts";
        var result = await _client.QueryFirstOrDefaultAsync<CountResult>(sql);
        return result?.Count ?? 0;
    }

    public async Task<IEnumerable<ContactDto>> GetAllAsync()
    {
        Debug.WriteLine("[TursoContactRepository] GetAllAsync");
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
        var result = await _client.QueryAsync<ContactDto>(sql);
        return result.Rows;
    }
}
