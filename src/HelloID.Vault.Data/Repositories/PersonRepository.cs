using Dapper;
using HelloID.Vault.Core.Models.DTOs;
using HelloID.Vault.Core.Models.Entities;
using HelloID.Vault.Data.Connection;
using HelloID.Vault.Data.Repositories.Base;
using HelloID.Vault.Data.Repositories.Interfaces;

namespace HelloID.Vault.Data.Repositories;

/// <summary>
/// Person repository for SQLite/PostgreSQL: Dapper execution plus the
/// ambient-transaction insert used during imports and the FK-relaxed update
/// (primary_manager_person_id is a self-referencing foreign key).
/// </summary>
public class PersonRepository : AbstractPersonRepository, IPersonRepository
{
    private readonly IDatabaseConnectionFactory _connectionFactory;

    public PersonRepository(IDatabaseConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    protected override bool UsePostgresJsonSyntax => _connectionFactory.DatabaseType.IsPostgreSqlBackend();

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

    public override async Task<int> InsertAsync(Person person, System.Data.IDbConnection connection, System.Data.IDbTransaction transaction)
    {
        var sql = @"
            INSERT INTO persons (
                person_id, display_name, external_id, user_name, gender,
                honorific_prefix, honorific_suffix, birth_date, birth_locality, marital_status,
                initials, given_name, family_name, family_name_prefix, convention,
                nick_name, family_name_partner, family_name_partner_prefix,
                blocked, status_reason, excluded, hr_excluded, manual_excluded, source,
                primary_manager_person_id, primary_manager_source, primary_manager_updated_at
            ) VALUES (
                @PersonId, @DisplayName, @ExternalId, @UserName, @Gender,
                @HonorificPrefix, @HonorificSuffix, @BirthDate, @BirthLocality, @MaritalStatus,
                @Initials, @GivenName, @FamilyName, @FamilyNamePrefix, @Convention,
                @NickName, @FamilyNamePartner, @FamilyNamePartnerPrefix,
                @Blocked, @StatusReason, @Excluded, @HrExcluded, @ManualExcluded, @Source,
                @PrimaryManagerPersonId, @PrimaryManagerSource, @PrimaryManagerUpdatedAt
            )";

        return await connection.ExecuteAsync(sql, person, transaction).ConfigureAwait(false);
    }

    public override async Task<int> UpdateAsync(Person person)
    {
        // Foreign keys relaxed for the self-referencing primary_manager_person_id
        using var connection = _connectionFactory.CreateConnection(enforceForeignKeys: false);

        return await connection.ExecuteAsync(UpdatePersonSql, new
        {
            person.DisplayName,
            person.UserName,
            person.Gender,
            person.HonorificPrefix,
            person.HonorificSuffix,
            person.BirthDate,
            person.BirthLocality,
            person.MaritalStatus,
            person.Initials,
            person.GivenName,
            person.FamilyName,
            person.FamilyNamePrefix,
            person.Convention,
            person.NickName,
            person.FamilyNamePartner,
            person.FamilyNamePartnerPrefix,
            person.Blocked,
            person.StatusReason,
            person.Excluded,
            person.HrExcluded,
            person.ManualExcluded,
            person.PrimaryManagerPersonId,
            person.PrimaryManagerSource,
            person.PrimaryManagerUpdatedAt,
            person.PersonId
        }).ConfigureAwait(false);
    }
}
