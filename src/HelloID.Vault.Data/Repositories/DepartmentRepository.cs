using Dapper;
using HelloID.Vault.Core.Models.Entities;
using HelloID.Vault.Core.Models.DTOs;
using HelloID.Vault.Data.Connection;
using HelloID.Vault.Data.Repositories.Interfaces;

namespace HelloID.Vault.Data.Repositories;

/// <summary>
/// Repository implementation for Department entity using Dapper.
/// </summary>
public class DepartmentRepository : IDepartmentRepository
{
    private readonly ISqliteConnectionFactory _connectionFactory;

    public DepartmentRepository(ISqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<IEnumerable<DepartmentDto>> GetAllAsync()
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            SELECT
                d.external_id AS ExternalId,
                d.display_name AS DisplayName,
                d.code AS Code,
                d.parent_external_id AS ParentExternalId,
                p.display_name AS ParentName,
                d.manager_person_id AS ManagerPersonId,
                m.display_name AS ManagerName,
                d.source AS Source
            FROM departments d
            LEFT JOIN departments p ON d.parent_external_id = p.external_id AND d.source = p.source
            LEFT JOIN persons m ON d.manager_person_id = m.person_id
            ORDER BY d.display_name";

        return await connection.QueryAsync<DepartmentDto>(sql);
    }

    public async Task<Department?> GetByIdAsync(string externalId, string source)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            SELECT
                external_id AS ExternalId,
                display_name AS DisplayName,
                code AS Code,
                parent_external_id AS ParentExternalId,
                manager_person_id AS ManagerPersonId,
                source AS Source
            FROM departments
            WHERE external_id = @ExternalId AND source = @Source";

        return await connection.QuerySingleOrDefaultAsync<Department>(sql, new { ExternalId = externalId, Source = source });
    }

    public async Task<IEnumerable<DepartmentDto>> GetChildrenAsync(string parentExternalId, string source)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            SELECT
                d.external_id AS ExternalId,
                d.display_name AS DisplayName,
                d.code AS Code,
                d.parent_external_id AS ParentExternalId,
                p.display_name AS ParentName,
                d.manager_person_id AS ManagerPersonId,
                m.display_name AS ManagerName,
                d.source AS Source
            FROM departments d
            LEFT JOIN departments p ON d.parent_external_id = p.external_id AND d.source = p.source
            LEFT JOIN persons m ON d.manager_person_id = m.person_id
            WHERE d.parent_external_id = @ParentExternalId AND d.source = @Source
            ORDER BY d.display_name";

        return await connection.QueryAsync<DepartmentDto>(sql, new { ParentExternalId = parentExternalId, Source = source });
    }

    public async Task<int> InsertAsync(Department department)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            INSERT INTO departments (
                external_id, display_name, code, parent_external_id, manager_person_id, source
            ) VALUES (
                @ExternalId, @DisplayName, @Code, @ParentExternalId, @ManagerPersonId, @Source
            )";

        return await connection.ExecuteAsync(sql, department);
    }

    public async Task<int> InsertAsync(Department department, System.Data.IDbConnection connection, System.Data.IDbTransaction transaction)
    {
        var sql = @"
            INSERT INTO departments (
                external_id, display_name, code, parent_external_id, manager_person_id, source
            ) VALUES (
                @ExternalId, @DisplayName, @Code, @ParentExternalId, @ManagerPersonId, @Source
            )";

        return await connection.ExecuteAsync(sql, department, transaction);
    }

    public async Task<int> InsertBatchAsync(IEnumerable<Department> departments, System.Data.IDbConnection connection, System.Data.IDbTransaction transaction)
    {
        var sql = @"
            INSERT INTO departments (
                external_id, display_name, code, parent_external_id, manager_person_id, source
            ) VALUES (
                @ExternalId, @DisplayName, @Code, @ParentExternalId, @ManagerPersonId, @Source
            )";

        int totalInserted = 0;
        foreach (var department in departments)
        {
            totalInserted += await connection.ExecuteAsync(sql, department, transaction);
        }

        return totalInserted;
    }

    public async Task<int> UpdateAsync(Department department)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            UPDATE departments SET
                display_name = @DisplayName,
                code = @Code,
                parent_external_id = @ParentExternalId,
                manager_person_id = @ManagerPersonId,
                source = @Source
            WHERE external_id = @ExternalId AND source = @Source";

        return await connection.ExecuteAsync(sql, department);
    }

    public async Task<int> DeleteAsync(string externalId, string source)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = "DELETE FROM departments WHERE external_id = @ExternalId AND source = @Source";

        return await connection.ExecuteAsync(sql, new { ExternalId = externalId, Source = source });
    }

    public async Task<IEnumerable<DepartmentDto>> GetPagedAsync(int page, int pageSize, string? source = null)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            SELECT
                d.external_id AS ExternalId,
                d.display_name AS DisplayName,
                d.code AS Code,
                d.parent_external_id AS ParentExternalId,
                p.display_name AS ParentName,
                d.manager_person_id AS ManagerPersonId,
                m.display_name AS ManagerName,
                d.source AS Source
            FROM departments d
            LEFT JOIN departments p ON d.parent_external_id = p.external_id AND d.source = p.source
            LEFT JOIN persons m ON d.manager_person_id = m.person_id";

        if (!string.IsNullOrEmpty(source))
        {
            sql += " WHERE d.source = @Source";
        }

        sql += @"
            ORDER BY d.display_name
            LIMIT @Limit OFFSET @Offset";

        var parameters = new
        {
            Limit = pageSize,
            Offset = (page - 1) * pageSize,
            Source = source
        };

        return await connection.QueryAsync<DepartmentDto>(sql, parameters);
    }

    public async Task<int> GetCountAsync()
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = "SELECT COUNT(*) FROM departments";

        return await connection.ExecuteScalarAsync<int>(sql);
    }
}
