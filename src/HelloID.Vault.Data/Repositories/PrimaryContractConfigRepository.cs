using Dapper;
using HelloID.Vault.Core.Models.Entities;
using HelloID.Vault.Data.Connection;
using HelloID.Vault.Data.Repositories.Interfaces;

namespace HelloID.Vault.Data.Repositories;

/// <summary>
/// Repository implementation for primary contract configuration.
/// </summary>
public class PrimaryContractConfigRepository : IPrimaryContractConfigRepository
{
    private readonly ISqliteConnectionFactory _connectionFactory;

    public PrimaryContractConfigRepository(ISqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<IEnumerable<PrimaryContractConfig>> GetAllAsync()
    {
        using var connection = _connectionFactory.CreateConnection();

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

        return await connection.QueryAsync<PrimaryContractConfig>(sql);
    }

    public async Task<IEnumerable<PrimaryContractConfig>> GetActiveConfigAsync()
    {
        using var connection = _connectionFactory.CreateConnection();

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

        return await connection.QueryAsync<PrimaryContractConfig>(sql);
    }

    public async Task UpdateConfigAsync(IEnumerable<PrimaryContractConfig> configs)
    {
        using var connection = _connectionFactory.CreateConnection();

        foreach (var config in configs)
        {
            var sql = @"
                UPDATE primary_contract_config
                SET sort_order = @SortOrder,
                    priority_order = @PriorityOrder,
                    is_active = @IsActive,
                    updated_at = CURRENT_TIMESTAMP
                WHERE field_name = @FieldName";

            await connection.ExecuteAsync(sql, new
            {
                config.FieldName,
                config.SortOrder,
                config.PriorityOrder,
                IsActive = config.IsActive ? 1 : 0
            });
        }
    }

    public async Task AddConfigAsync(IEnumerable<PrimaryContractConfig> configs)
    {
        using var connection = _connectionFactory.CreateConnection();

        foreach (var config in configs)
        {
            var sql = @"
                INSERT INTO primary_contract_config (field_name, display_name, sort_order, priority_order, is_active, created_at, updated_at)
                VALUES (@FieldName, @DisplayName, @SortOrder, @PriorityOrder, @IsActive, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP)";

            await connection.ExecuteAsync(sql, new
            {
                config.FieldName,
                config.DisplayName,
                config.SortOrder,
                config.PriorityOrder,
                IsActive = config.IsActive ? 1 : 0
            });
        }
    }

    public async Task DeleteConfigAsync(IEnumerable<string> fieldNames)
    {
        using var connection = _connectionFactory.CreateConnection();

        var sql = @"
            DELETE FROM primary_contract_config
            WHERE field_name IN @FieldNames";

        await connection.ExecuteAsync(sql, new { FieldNames = fieldNames });
    }

    public async Task ResetToDefaultAsync()
    {
        using var connection = _connectionFactory.CreateConnection();

        // Reset to default configuration from Business-Logic-Rules.md
        var sql = @"
            UPDATE primary_contract_config SET sort_order = 'DESC', priority_order = 1, is_active = 1, updated_at = CURRENT_TIMESTAMP WHERE field_name = 'Fte';
            UPDATE primary_contract_config SET sort_order = 'DESC', priority_order = 2, is_active = 1, updated_at = CURRENT_TIMESTAMP WHERE field_name = 'HoursPerWeek';
            UPDATE primary_contract_config SET sort_order = 'DESC', priority_order = 3, is_active = 1, updated_at = CURRENT_TIMESTAMP WHERE field_name = 'Sequence';
            UPDATE primary_contract_config SET sort_order = 'DESC', priority_order = 4, is_active = 1, updated_at = CURRENT_TIMESTAMP WHERE field_name = 'EndDate';
            UPDATE primary_contract_config SET sort_order = 'ASC', priority_order = 5, is_active = 1, updated_at = CURRENT_TIMESTAMP WHERE field_name = 'StartDate';
            UPDATE primary_contract_config SET sort_order = 'ASC', priority_order = 6, is_active = 1, updated_at = CURRENT_TIMESTAMP WHERE field_name = 'ContractId';";

        await connection.ExecuteAsync(sql);
    }
}
