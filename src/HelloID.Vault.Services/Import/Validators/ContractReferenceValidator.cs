using System.Data;
using System.Diagnostics;
using Dapper;
using HelloID.Vault.Data.Connection;
using HelloID.Vault.Services.Interfaces;

namespace HelloID.Vault.Services.Import.Validators;

/// <summary>
/// Validates contract references against master tables to detect orphaned references.
/// </summary>
public static class ContractReferenceValidator
{
    /// <summary>
    /// Validates all contract references using source-aware lookups.
    /// Detects orphaned references (contract references entity that doesn't exist in master table with matching source).
    /// </summary>
    public static async Task ValidateAsync(IDatabaseConnectionFactory connectionFactory, ImportResult result)
    {
        using var connection = connectionFactory.CreateConnection();

        // Debug: Check total contracts in database
        var totalContractsInDb = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM contracts");
        Debug.WriteLine($"[Import Validation] Database contains {totalContractsInDb} total contracts");

        // Validate each reference type
        await ValidateReferenceAsync(connection, result,
            "departments", "department_external_id", "d",
            r => r.OrphanedDepartmentsDetected = r.OrphanedDepartmentsDetected + 1);

        await ValidateReferenceAsync(connection, result,
            "locations", "location_external_id", "l",
            r => r.OrphanedLocationsDetected = r.OrphanedLocationsDetected + 1);

        await ValidateReferenceAsync(connection, result,
            "cost_centers", "cost_center_external_id", "cc",
            r => r.OrphanedCostCentersDetected = r.OrphanedCostCentersDetected + 1);

        await ValidateReferenceAsync(connection, result,
            "cost_bearers", "cost_bearer_external_id", "cb",
            r => r.OrphanedCostBearersDetected = r.OrphanedCostBearersDetected + 1);

        await ValidateReferenceAsync(connection, result,
            "employers", "employer_external_id", "e",
            r => r.OrphanedEmployersDetected = r.OrphanedEmployersDetected + 1);

        await ValidateReferenceAsync(connection, result,
            "teams", "team_external_id", "t",
            r => r.OrphanedTeamsDetected = r.OrphanedTeamsDetected + 1);

        await ValidateReferenceAsync(connection, result,
            "divisions", "division_external_id", "d",
            r => r.OrphanedDivisionsDetected = r.OrphanedDivisionsDetected + 1);

        await ValidateReferenceAsync(connection, result,
            "titles", "title_external_id", "t",
            r => r.OrphanedTitlesDetected = r.OrphanedTitlesDetected + 1);

        await ValidateReferenceAsync(connection, result,
            "organizations", "organization_external_id", "o",
            r => r.OrphanedOrganizationsDetected = r.OrphanedOrganizationsDetected + 1);

        // Summary log
        var totalOrphaned = result.OrphanedDepartmentsDetected + result.OrphanedLocationsDetected +
                           result.OrphanedCostCentersDetected + result.OrphanedCostBearersDetected +
                           result.OrphanedEmployersDetected + result.OrphanedTeamsDetected +
                           result.OrphanedDivisionsDetected + result.OrphanedTitlesDetected +
                           result.OrphanedOrganizationsDetected;

        if (totalOrphaned > 0)
        {
            Debug.WriteLine($"\n[Import Validation] Total orphaned references: {totalOrphaned}");
            Debug.WriteLine("[Import Validation] Orphaned references are contracts that reference entities not in master tables with matching source.");
            Debug.WriteLine("[Import Validation] This is acceptable by design - source is inherited from contract.");
        }
        else
        {
            Debug.WriteLine("[Import Validation] No orphaned references detected - all contract references match master table entries.");
        }
    }

    /// <summary>
    /// Validates references to a specific table and updates the result with orphaned count.
    /// </summary>
    private static async Task ValidateReferenceAsync(
        IDbConnection connection,
        ImportResult result,
        string tableName,
        string fkColumn,
        string tableAlias,
        Action<ImportResult> incrementAction)
    {
        var sql = $@"
            SELECT
                c.external_id AS ContractId,
                c.{fkColumn} AS ReferenceId,
                c.source AS Source
            FROM contracts c
            WHERE c.{fkColumn} IS NOT NULL
            AND NOT EXISTS (
                SELECT 1 FROM {tableName} {tableAlias}
                WHERE {tableAlias}.external_id = c.{fkColumn}
                AND {tableAlias}.source = c.source
            )";

        var orphaned = await connection.QueryAsync<(string ContractId, string ReferenceId, string Source)>(sql);

        if (orphaned.Any())
        {
            var count = orphaned.Count();
            for (int i = 0; i < count; i++)
            {
                incrementAction(result);
            }

            Debug.WriteLine($"[Import Validation] Found {count} orphaned {tableName} reference(s):");
            foreach (var orphan in orphaned.Take(10))
            {
                Debug.WriteLine($"  - Contract {orphan.ContractId} → {tableName.TrimEnd('s')} {orphan.ReferenceId} (Source: {orphan.Source}) - not in master table");
            }
            if (count > 10)
            {
                Debug.WriteLine($"  ... and {count - 10} more");
            }
        }
    }
}
