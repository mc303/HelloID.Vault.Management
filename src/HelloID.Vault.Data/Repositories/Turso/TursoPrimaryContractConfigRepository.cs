using System.Diagnostics;
using HelloID.Vault.Core.Models.Entities;
using HelloID.Vault.Data.Connection;
using HelloID.Vault.Data.Repositories.Interfaces;

namespace HelloID.Vault.Data.Repositories.Turso;

public class TursoPrimaryContractConfigRepository : IPrimaryContractConfigRepository
{
    private readonly ITursoClient _client;

    public TursoPrimaryContractConfigRepository(ITursoClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    public async Task<IEnumerable<PrimaryContractConfig>> GetAllAsync()
    {
        Debug.WriteLine("[TursoPrimaryContractConfigRepository] GetAllAsync");
        var sql = @"
            SELECT
                id AS Id,
                field_name AS FieldName,
                display_name AS DisplayName,
                sort_order AS SortOrder,
                priority_order AS PriorityOrder,
                is_active AS IsActive,
                created_at AS CreatedAt,
                updated_at AS UpdatedAt
            FROM primary_contract_config
            ORDER BY priority_order ASC";
        var result = await _client.QueryAsync<PrimaryContractConfig>(sql);
        return result.Rows;
    }

    public async Task<IEnumerable<PrimaryContractConfig>> GetActiveConfigAsync()
    {
        Debug.WriteLine("[TursoPrimaryContractConfigRepository] GetActiveConfigAsync");
        var sql = @"
            SELECT
                id AS Id,
                field_name AS FieldName,
                display_name AS DisplayName,
                sort_order AS SortOrder,
                priority_order AS PriorityOrder,
                is_active AS IsActive,
                created_at AS CreatedAt,
                updated_at AS UpdatedAt
            FROM primary_contract_config
            WHERE is_active = 1
            ORDER BY priority_order ASC";
        var result = await _client.QueryAsync<PrimaryContractConfig>(sql);
        return result.Rows;
    }

    public async Task UpdateConfigAsync(IEnumerable<PrimaryContractConfig> configs)
    {
        Debug.WriteLine("[TursoPrimaryContractConfigRepository] UpdateConfigAsync");
        var statements = configs.Select(config => new TursoStatement
        {
            Sql = @"
                UPDATE primary_contract_config
                SET sort_order = ?,
                    priority_order = ?,
                    is_active = ?,
                    updated_at = CURRENT_TIMESTAMP
                WHERE field_name = ?",
            Args =
            [
                TursoValue.Text(config.SortOrder ?? string.Empty),
                TursoValue.Integer(config.PriorityOrder),
                TursoValue.Integer(config.IsActive ? 1 : 0),
                TursoValue.Text(config.FieldName ?? string.Empty)
            ],
            WantRows = false
        }).ToList();

        await _client.ExecuteTransactionAsync(statements);
    }

    public async Task AddConfigAsync(IEnumerable<PrimaryContractConfig> configs)
    {
        Debug.WriteLine("[TursoPrimaryContractConfigRepository] AddConfigAsync");
        var statements = configs.Select(config => new TursoStatement
        {
            Sql = @"
                INSERT INTO primary_contract_config (field_name, display_name, sort_order, priority_order, is_active, created_at, updated_at)
                VALUES (?, ?, ?, ?, ?, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP)",
            Args =
            [
                TursoValue.Text(config.FieldName ?? string.Empty),
                TursoValue.Text(config.DisplayName ?? string.Empty),
                TursoValue.Text(config.SortOrder ?? string.Empty),
                TursoValue.Integer(config.PriorityOrder),
                TursoValue.Integer(config.IsActive ? 1 : 0)
            ],
            WantRows = false
        }).ToList();

        await _client.ExecuteTransactionAsync(statements);
    }

    public async Task DeleteConfigAsync(IEnumerable<string> fieldNames)
    {
        Debug.WriteLine("[TursoPrimaryContractConfigRepository] DeleteConfigAsync");
        var fieldList = fieldNames.ToList();
        
        var statements = fieldList.Select(fieldName => new TursoStatement
        {
            Sql = "DELETE FROM primary_contract_config WHERE field_name = ?",
            Args = [TursoValue.Text(fieldName)],
            WantRows = false
        }).ToList();

        await _client.ExecuteTransactionAsync(statements);
    }

    public async Task ResetToDefaultAsync()
    {
        Debug.WriteLine("[TursoPrimaryContractConfigRepository] ResetToDefaultAsync");
        var statements = new List<TursoStatement>
        {
            CreateUpdateStatement("Fte", "DESC", 1),
            CreateUpdateStatement("HoursPerWeek", "DESC", 2),
            CreateUpdateStatement("Sequence", "DESC", 3),
            CreateUpdateStatement("EndDate", "DESC", 4),
            CreateUpdateStatement("StartDate", "ASC", 5),
            CreateUpdateStatement("ContractId", "ASC", 6)
        };

        await _client.ExecuteTransactionAsync(statements);
    }

    private static TursoStatement CreateUpdateStatement(string fieldName, string sortOrder, int priorityOrder)
    {
        return new TursoStatement
        {
            Sql = "UPDATE primary_contract_config SET sort_order = ?, priority_order = ?, is_active = 1, updated_at = CURRENT_TIMESTAMP WHERE field_name = ?",
            Args =
            [
                TursoValue.Text(sortOrder),
                TursoValue.Integer(priorityOrder),
                TursoValue.Text(fieldName)
            ],
            WantRows = false
        };
    }
}
