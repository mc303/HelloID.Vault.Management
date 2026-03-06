using System.Diagnostics;
using HelloID.Vault.Core.Models.Entities;
using HelloID.Vault.Core.Models.DTOs;
using HelloID.Vault.Data.Connection;
using HelloID.Vault.Data.Repositories.Interfaces;

namespace HelloID.Vault.Data.Repositories.Turso;

/// <summary>
/// Turso implementation of DepartmentRepository using HTTP API.
/// </summary>
public class TursoDepartmentRepository : IDepartmentRepository
{
    private readonly ITursoClient _client;

    public TursoDepartmentRepository(ITursoClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    public async Task<IEnumerable<DepartmentDto>> GetAllAsync()
    {
        Debug.WriteLine("[TursoDepartmentRepository] GetAllAsync");

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

        var result = await _client.QueryAsync<DepartmentDto>(sql);
        return result.Rows;
    }

    public async Task<Department?> GetByIdAsync(string externalId, string source)
    {
        Debug.WriteLine($"[TursoDepartmentRepository] GetByIdAsync: {externalId}, {source}");

        var sql = @"
            SELECT
                external_id AS ExternalId,
                display_name AS DisplayName,
                code AS Code,
                parent_external_id AS ParentExternalId,
                manager_person_id AS ManagerPersonId,
                source AS Source
            FROM departments
            WHERE external_id = ? AND source = ?";

        return await _client.QueryFirstOrDefaultAsync<Department>(sql, new { externalId, source });
    }

    public async Task<IEnumerable<DepartmentDto>> GetChildrenAsync(string parentExternalId, string source)
    {
        Debug.WriteLine($"[TursoDepartmentRepository] GetChildrenAsync: {parentExternalId}");

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
            WHERE d.parent_external_id = ? AND d.source = ?
            ORDER BY d.display_name";

        var result = await _client.QueryAsync<DepartmentDto>(sql, new { parentExternalId, source });
        return result.Rows;
    }

    public async Task<int> InsertAsync(Department department)
    {
        Debug.WriteLine($"[TursoDepartmentRepository] InsertAsync: {department.ExternalId}");

        var sql = @"
            INSERT INTO departments (
                external_id, display_name, code, parent_external_id, manager_person_id, source
            ) VALUES (?, ?, ?, ?, ?, ?)";

        return await _client.ExecuteAsync(sql, department);
    }

    public async Task<int> InsertAsync(Department department, System.Data.IDbConnection connection, System.Data.IDbTransaction transaction)
    {
        return await InsertAsync(department);
    }

    public async Task<int> InsertBatchAsync(IEnumerable<Department> departments, System.Data.IDbConnection connection, System.Data.IDbTransaction transaction)
    {
        var statements = departments.Select(d => new TursoStatement
        {
            Sql = @"
                INSERT INTO departments (
                    external_id, display_name, code, parent_external_id, manager_person_id, source
                ) VALUES (?, ?, ?, ?, ?, ?)",
            Args =
            [
                TursoValue.Text(d.ExternalId ?? string.Empty),
                TursoValue.Text(d.DisplayName ?? string.Empty),
                TursoValue.Text(d.Code ?? string.Empty),
                TursoValue.Text(d.ParentExternalId ?? string.Empty),
                TursoValue.Text(d.ManagerPersonId ?? string.Empty),
                TursoValue.Text(d.Source ?? string.Empty)
            ],
            WantRows = false
        }).ToList();

        var result = await _client.ExecuteTransactionAsync(statements);
        return result.TotalAffectedRows;
    }

    public async Task<int> UpdateAsync(Department department)
    {
        Debug.WriteLine($"[TursoDepartmentRepository] UpdateAsync: {department.ExternalId}");

        var sql = @"
            UPDATE departments SET
                display_name = ?,
                code = ?,
                parent_external_id = ?,
                manager_person_id = ?,
                source = ?
            WHERE external_id = ? AND source = ?";

        return await _client.ExecuteAsync(sql, new
        {
            department.DisplayName,
            department.Code,
            department.ParentExternalId,
            department.ManagerPersonId,
            department.Source,
            department.ExternalId
        });
    }

    public async Task<int> DeleteAsync(string externalId, string source)
    {
        Debug.WriteLine($"[TursoDepartmentRepository] DeleteAsync: {externalId}");

        var sql = "DELETE FROM departments WHERE external_id = ? AND source = ?";
        return await _client.ExecuteAsync(sql, new { externalId, source });
    }

    public async Task<IEnumerable<DepartmentDto>> GetPagedAsync(int page, int pageSize, string? source = null)
    {
        Debug.WriteLine($"[TursoDepartmentRepository] GetPagedAsync: page={page}, pageSize={pageSize}");

        var offset = (page - 1) * pageSize;
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
            sql += " WHERE d.source = ?";
        }

        sql += " ORDER BY d.display_name LIMIT ? OFFSET ?";

        object parameters = string.IsNullOrEmpty(source)
            ? new { Limit = pageSize, Offset = offset }
            : new { Source = source, Limit = pageSize, Offset = offset };

        var result = await _client.QueryAsync<DepartmentDto>(sql, parameters);
        return result.Rows;
    }

    public async Task<int> GetCountAsync()
    {
        Debug.WriteLine("[TursoDepartmentRepository] GetCountAsync");

        var sql = "SELECT COUNT(*) AS count FROM departments";
        var result = await _client.QueryFirstOrDefaultAsync<CountResult>(sql);
        return result?.Count ?? 0;
    }
}

internal class CountResult
{
    public int Count { get; set; }
}
