using Dapper;
using HelloID.Vault.Core.Models.DTOs;
using HelloID.Vault.Core.Models.Entities;
using HelloID.Vault.Data.Connection;
using HelloID.Vault.Data.Repositories.Base;
using HelloID.Vault.Data.Repositories.Interfaces;

namespace HelloID.Vault.Data.Repositories;

/// <summary>
/// Contact repository for SQLite/PostgreSQL: Dapper execution plus the
/// ambient-transaction insert used during imports.
/// </summary>
public class ContactRepository : AbstractContactRepository, IContactRepository
{
    private readonly IDatabaseConnectionFactory _connectionFactory;

    public ContactRepository(IDatabaseConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    protected override async Task<IEnumerable<TResult>> QueryRowsAsync<TResult>(string sql, object? parameters = null)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QueryAsync<TResult>(sql, parameters).ConfigureAwait(false);
    }

    protected override async Task<TResult?> QueryFirstRowAsync<TResult>(string sql, object? parameters = null) where TResult : class
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QueryFirstOrDefaultAsync<TResult>(sql, parameters).ConfigureAwait(false);
    }

    protected override async Task<int> ExecuteRowsAsync(string sql, object? parameters = null)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.ExecuteAsync(sql, parameters).ConfigureAwait(false);
    }

    protected override async Task<TResult> ExecuteScalarValueAsync<TResult>(string sql, object? parameters = null)
    {
        using var connection = _connectionFactory.CreateConnection();
        var result = await connection.ExecuteScalarAsync<TResult>(sql, parameters).ConfigureAwait(false);
        return result ?? throw new InvalidOperationException("Query returned no value.");
    }

    public override async Task<int> InsertAsync(Contact contact, System.Data.IDbConnection connection, System.Data.IDbTransaction transaction)
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
}
