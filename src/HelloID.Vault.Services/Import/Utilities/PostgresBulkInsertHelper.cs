using System.Data;
using System.Diagnostics;
using HelloID.Vault.Core.Models.Entities;
using Npgsql;

namespace HelloID.Vault.Services.Import.Utilities;

/// <summary>
/// High-performance bulk insert helper using PostgreSQL COPY (binary import).
/// Falls back to Dapper batch insert for non-PostgreSQL connections.
/// COPY is 10-100x faster than individual INSERT statements.
/// </summary>
public static class PostgresBulkInsertHelper
{
    /// <summary>
    /// Bulk inserts persons using COPY if Npgsql, otherwise falls back to Dapper.
    /// </summary>
    public static async Task<int> BulkInsertPersonsAsync(
        IEnumerable<Person> persons,
        IDbConnection connection,
        IDbTransaction? transaction = null)
    {
        var personList = persons.ToList();
        if (personList.Count == 0) return 0;

        if (connection is NpgsqlConnection npgsqlConn)
        {
            return await CopyPersonsAsync(npgsqlConn, personList);
        }

        // Fallback: Dapper batch insert
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

        return await Dapper.SqlMapper.ExecuteAsync(connection, sql, personList, transaction);
    }

    private static async Task<int> CopyPersonsAsync(NpgsqlConnection connection, List<Person> persons)
    {
        Debug.WriteLine($"[BulkInsert] COPY persons: {persons.Count} rows");

        using var writer = connection.BeginBinaryImport(
            "COPY persons (person_id, display_name, external_id, user_name, gender, " +
            "honorific_prefix, honorific_suffix, birth_date, birth_locality, marital_status, " +
            "initials, given_name, family_name, family_name_prefix, convention, " +
            "nick_name, family_name_partner, family_name_partner_prefix, " +
            "blocked, status_reason, excluded, hr_excluded, manual_excluded, source, " +
            "primary_manager_person_id, primary_manager_source, primary_manager_updated_at) " +
            "FROM STDIN (FORMAT BINARY)");

        foreach (var p in persons)
        {
            writer.StartRow();
            writer.Write(p.PersonId, NpgsqlTypes.NpgsqlDbType.Text);
            writer.Write(p.DisplayName, NpgsqlTypes.NpgsqlDbType.Text);
            WriteNullable(writer, p.ExternalId);
            WriteNullable(writer, p.UserName);
            WriteNullable(writer, p.Gender);
            WriteNullable(writer, p.HonorificPrefix);
            WriteNullable(writer, p.HonorificSuffix);
            WriteNullable(writer, p.BirthDate);
            WriteNullable(writer, p.BirthLocality);
            WriteNullable(writer, p.MaritalStatus);
            WriteNullable(writer, p.Initials);
            WriteNullable(writer, p.GivenName);
            WriteNullable(writer, p.FamilyName);
            WriteNullable(writer, p.FamilyNamePrefix);
            WriteNullable(writer, p.Convention);
            WriteNullable(writer, p.NickName);
            WriteNullable(writer, p.FamilyNamePartner);
            WriteNullable(writer, p.FamilyNamePartnerPrefix);
            writer.Write(p.Blocked, NpgsqlTypes.NpgsqlDbType.Boolean);
            WriteNullable(writer, p.StatusReason);
            writer.Write(p.Excluded, NpgsqlTypes.NpgsqlDbType.Boolean);
            writer.Write(p.HrExcluded, NpgsqlTypes.NpgsqlDbType.Boolean);
            writer.Write(p.ManualExcluded, NpgsqlTypes.NpgsqlDbType.Boolean);
            WriteNullable(writer, p.Source);
            WriteNullable(writer, p.PrimaryManagerPersonId);
            WriteNullable(writer, p.PrimaryManagerSource);
            WriteNullable(writer, p.PrimaryManagerUpdatedAt);
        }

        return (int)writer.Complete();
    }

    /// <summary>
    /// Bulk inserts contracts using COPY if Npgsql, otherwise falls back to Dapper.
    /// </summary>
    public static async Task<int> BulkInsertContractsAsync(
        IEnumerable<Contract> contracts,
        IDbConnection connection,
        IDbTransaction? transaction = null)
    {
        var contractList = contracts.ToList();
        if (contractList.Count == 0) return 0;

        if (connection is NpgsqlConnection npgsqlConn)
        {
            return await CopyContractsAsync(npgsqlConn, contractList);
        }

        // Fallback: Dapper batch insert
        var sql = @"
            INSERT INTO contracts (
                external_id, person_id, start_date, end_date, type_code, type_description,
                fte, hours_per_week, percentage, sequence, manager_person_external_id,
                location_external_id, location_source, cost_center_external_id, cost_center_source,
                cost_bearer_external_id, cost_bearer_source, employer_external_id, employer_source,
                team_external_id, team_source, department_external_id, department_source,
                division_external_id, division_source, title_external_id, title_source,
                organization_external_id, organization_source, source
            ) VALUES (
                @ExternalId, @PersonId, @StartDate, @EndDate, @TypeCode, @TypeDescription,
                @Fte, @HoursPerWeek, @Percentage, @Sequence, @ManagerPersonExternalId,
                @LocationExternalId, @LocationSource, @CostCenterExternalId, @CostCenterSource,
                @CostBearerExternalId, @CostBearerSource, @EmployerExternalId, @EmployerSource,
                @TeamExternalId, @TeamSource, @DepartmentExternalId, @DepartmentSource,
                @DivisionExternalId, @DivisionSource, @TitleExternalId, @TitleSource,
                @OrganizationExternalId, @OrganizationSource, @Source
            )";

        return await Dapper.SqlMapper.ExecuteAsync(connection, sql, contractList, transaction);
    }

    private static async Task<int> CopyContractsAsync(NpgsqlConnection connection, List<Contract> contracts)
    {
        Debug.WriteLine($"[BulkInsert] COPY contracts: {contracts.Count} rows");

        using var writer = connection.BeginBinaryImport(
            "COPY contracts (external_id, person_id, start_date, end_date, type_code, type_description, " +
            "fte, hours_per_week, percentage, sequence, manager_person_external_id, " +
            "location_external_id, location_source, cost_center_external_id, cost_center_source, " +
            "cost_bearer_external_id, cost_bearer_source, employer_external_id, employer_source, " +
            "team_external_id, team_source, department_external_id, department_source, " +
            "division_external_id, division_source, title_external_id, title_source, " +
            "organization_external_id, organization_source, source) " +
            "FROM STDIN (FORMAT BINARY)");

        foreach (var c in contracts)
        {
            writer.StartRow();
            WriteNullable(writer, c.ExternalId);
            writer.Write(c.PersonId, NpgsqlTypes.NpgsqlDbType.Text);
            WriteNullable(writer, c.StartDate);
            WriteNullable(writer, c.EndDate);
            WriteNullable(writer, c.TypeCode);
            WriteNullable(writer, c.TypeDescription);
            WriteNullableDouble(writer, c.Fte);
            WriteNullableDouble(writer, c.HoursPerWeek);
            WriteNullableDouble(writer, c.Percentage);
            WriteNullableInt(writer, c.Sequence);
            WriteNullable(writer, c.ManagerPersonExternalId);
            WriteNullable(writer, c.LocationExternalId);
            WriteNullable(writer, c.LocationSource);
            WriteNullable(writer, c.CostCenterExternalId);
            WriteNullable(writer, c.CostCenterSource);
            WriteNullable(writer, c.CostBearerExternalId);
            WriteNullable(writer, c.CostBearerSource);
            WriteNullable(writer, c.EmployerExternalId);
            WriteNullable(writer, c.EmployerSource);
            WriteNullable(writer, c.TeamExternalId);
            WriteNullable(writer, c.TeamSource);
            WriteNullable(writer, c.DepartmentExternalId);
            WriteNullable(writer, c.DepartmentSource);
            WriteNullable(writer, c.DivisionExternalId);
            WriteNullable(writer, c.DivisionSource);
            WriteNullable(writer, c.TitleExternalId);
            WriteNullable(writer, c.TitleSource);
            WriteNullable(writer, c.OrganizationExternalId);
            WriteNullable(writer, c.OrganizationSource);
            WriteNullable(writer, c.Source);
        }

        return (int)writer.Complete();
    }

    /// <summary>
    /// Bulk inserts contacts using COPY if Npgsql, otherwise falls back to Dapper.
    /// </summary>
    public static async Task<int> BulkInsertContactsAsync(
        IEnumerable<Contact> contacts,
        IDbConnection connection,
        IDbTransaction? transaction = null)
    {
        var contactList = contacts.ToList();
        if (contactList.Count == 0) return 0;

        if (connection is NpgsqlConnection npgsqlConn)
        {
            return await CopyContactsAsync(npgsqlConn, contactList);
        }

        // Fallback: Dapper batch insert
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

        return await Dapper.SqlMapper.ExecuteAsync(connection, sql, contactList, transaction);
    }

    private static async Task<int> CopyContactsAsync(NpgsqlConnection connection, List<Contact> contacts)
    {
        Debug.WriteLine($"[BulkInsert] COPY contacts: {contacts.Count} rows");

        using var writer = connection.BeginBinaryImport(
            "COPY contacts (person_id, type, email, phone_mobile, phone_fixed, " +
            "address_street, address_street_ext, address_house_number, address_house_number_ext, " +
            "address_postal, address_locality, address_country) " +
            "FROM STDIN (FORMAT BINARY)");

        foreach (var c in contacts)
        {
            writer.StartRow();
            writer.Write(c.PersonId, NpgsqlTypes.NpgsqlDbType.Text);
            WriteNullable(writer, c.Type);
            WriteNullable(writer, c.Email);
            WriteNullable(writer, c.PhoneMobile);
            WriteNullable(writer, c.PhoneFixed);
            WriteNullable(writer, c.AddressStreet);
            WriteNullable(writer, c.AddressStreetExt);
            WriteNullable(writer, c.AddressHouseNumber);
            WriteNullable(writer, c.AddressHouseNumberExt);
            WriteNullable(writer, c.AddressPostal);
            WriteNullable(writer, c.AddressLocality);
            WriteNullable(writer, c.AddressCountry);
        }

        return (int)writer.Complete();
    }

    private static void WriteNullable(NpgsqlBinaryImporter writer, string? value)
    {
        if (value != null)
            writer.Write(value, NpgsqlTypes.NpgsqlDbType.Text);
        else
            writer.WriteNull();
    }

    private static void WriteNullableDouble(NpgsqlBinaryImporter writer, double? value)
    {
        if (value.HasValue)
            writer.Write(value.Value, NpgsqlTypes.NpgsqlDbType.Double);
        else
            writer.WriteNull();
    }

    private static void WriteNullableInt(NpgsqlBinaryImporter writer, int? value)
    {
        if (value.HasValue)
            writer.Write(value.Value, NpgsqlTypes.NpgsqlDbType.Integer);
        else
            writer.WriteNull();
    }

    /// <summary>
    /// Bulk updates custom_fields for entities using temp table + COPY + JOIN UPDATE.
    /// Reduces N individual UPDATEs to 3 queries (create temp + COPY + UPDATE...FROM).
    /// Falls back to Dapper batch for non-PostgreSQL connections.
    /// </summary>
    /// <param name="tableName">"persons" or "contracts"</param>
    /// <param name="updates">List of (externalId, json) pairs</param>
    public static async Task BulkUpdateCustomFieldsAsync(
        string tableName,
        IEnumerable<(string ExternalId, string Json)> updates,
        IDbConnection connection,
        IDbTransaction? transaction = null)
    {
        var updateList = updates.ToList();
        if (updateList.Count == 0) return;

        if (connection is NpgsqlConnection npgsqlConn)
        {
            await CopyCustomFieldsAsync(npgsqlConn, tableName, updateList, transaction);
        }
        else
        {
            // Fallback: Dapper batch
            var sql = tableName == "persons"
                ? "UPDATE persons SET custom_fields = json(@Json) WHERE external_id = @ExternalId"
                : "UPDATE contracts SET custom_fields = json(@Json) WHERE external_id = @ExternalId";

            await Dapper.SqlMapper.ExecuteAsync(connection, sql,
                updateList.Select(u => new { u.ExternalId, u.Json }), transaction);
        }
    }

    private static async Task CopyCustomFieldsAsync(
        NpgsqlConnection connection,
        string tableName,
        List<(string ExternalId, string Json)> updates,
        IDbTransaction? transaction)
    {
        var tempTableName = $"tmp_{tableName}_cf";

        Debug.WriteLine($"[BulkUpdate] COPY custom fields for {tableName}: {updates.Count} rows");

        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction as NpgsqlTransaction;
        cmd.CommandText = $"CREATE TEMP TABLE {tempTableName} (external_id TEXT, custom_fields JSONB) ON COMMIT DROP";
        await cmd.ExecuteNonQueryAsync();

        // COPY all rows into temp table
        using (var writer = connection.BeginBinaryImport(
            $"COPY {tempTableName} (external_id, custom_fields) FROM STDIN (FORMAT BINARY)"))
        {
            foreach (var (externalId, json) in updates)
            {
                writer.StartRow();
                writer.Write(externalId, NpgsqlTypes.NpgsqlDbType.Text);
                writer.Write(json, NpgsqlTypes.NpgsqlDbType.Jsonb);
            }
            writer.Complete();
        }

        // Single UPDATE...FROM join
        using var updateCmd = connection.CreateCommand();
        updateCmd.Transaction = transaction as NpgsqlTransaction;
        updateCmd.CommandText =
            $"UPDATE {tableName} SET custom_fields = tmp.custom_fields " +
            $"FROM {tempTableName} tmp WHERE {tableName}.external_id = tmp.external_id";
        await updateCmd.ExecuteNonQueryAsync();
    }
}
