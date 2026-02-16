using System.Data;
using System.Diagnostics;
using Dapper;
using HelloID.Vault.Data.Connection;
using Npgsql;

namespace HelloID.Vault.Services.Import.Strategies;

/// <summary>
/// Factory for creating the appropriate import strategy based on database type and capabilities.
/// </summary>
public static class ImportStrategyFactory
{
    /// <summary>
    /// Creates the appropriate import strategy for the given database connection.
    /// </summary>
    /// <param name="connectionFactory">The database connection factory</param>
    /// <param name="connection">An open database connection for capability testing</param>
    /// <returns>The appropriate import strategy</returns>
    public static async Task<IImportStrategy> CreateAsync(IDatabaseConnectionFactory connectionFactory, IDbConnection connection)
    {
        return connectionFactory.DatabaseType switch
        {
            DatabaseType.Sqlite => CreateSqliteStrategy(),
            DatabaseType.PostgreSql => await CreatePostgresStrategyAsync(connection),
            _ => throw new NotSupportedException($"Database type {connectionFactory.DatabaseType} is not supported")
        };
    }

    /// <summary>
    /// Creates the appropriate import strategy synchronously (without capability testing).
    /// Use CreateAsync when possible to detect superuser privileges.
    /// </summary>
    /// <param name="connectionFactory">The database connection factory</param>
    /// <param name="canDisableFkConstraints">Whether FK constraints can be disabled (superuser)</param>
    /// <returns>The appropriate import strategy</returns>
    public static IImportStrategy Create(IDatabaseConnectionFactory connectionFactory, bool? canDisableFkConstraints = null)
    {
        return connectionFactory.DatabaseType switch
        {
            DatabaseType.Sqlite => CreateSqliteStrategy(),
            DatabaseType.PostgreSql => CreatePostgresStrategy(canDisableFkConstraints),
            _ => throw new NotSupportedException($"Database type {connectionFactory.DatabaseType} is not supported")
        };
    }

    private static IImportStrategy CreateSqliteStrategy()
    {
        Debug.WriteLine("[ImportStrategyFactory] Creating SQLite strategy (PRAGMA foreign_keys)");
        return new SqliteImportStrategy();
    }

    private static IImportStrategy CreatePostgresStrategy(bool? canDisableFkConstraints)
    {
        if (canDisableFkConstraints == true)
        {
            Debug.WriteLine("[ImportStrategyFactory] Creating PostgreSQL Superuser strategy (session_replication_role)");
            return new PostgresSuperuserImportStrategy();
        }
        else
        {
            Debug.WriteLine("[ImportStrategyFactory] Creating PostgreSQL Managed strategy (two-pass)");
            return new PostgresManagedImportStrategy();
        }
    }

    private static async Task<IImportStrategy> CreatePostgresStrategyAsync(IDbConnection connection)
    {
        // Test if we have superuser privileges by trying to set session_replication_role
        bool canDisableFkConstraints = await TestSuperuserPrivilegesAsync(connection);

        return CreatePostgresStrategy(canDisableFkConstraints);
    }

    /// <summary>
    /// Tests if the current PostgreSQL user has superuser privileges
    /// by attempting to set session_replication_role.
    /// </summary>
    private static async Task<bool> TestSuperuserPrivilegesAsync(IDbConnection connection)
    {
        Debug.WriteLine("[ImportStrategyFactory] Testing PostgreSQL superuser privileges...");

        try
        {
            // Try to set session_replication_role - requires superuser
            await connection.ExecuteAsync("SET session_replication_role = 'replica'");
            Debug.WriteLine("[ImportStrategyFactory] Superuser privileges available - can disable FK constraints");

            // Reset immediately
            await connection.ExecuteAsync("SET session_replication_role = 'origin'");
            return true;
        }
        catch (PostgresException ex) when (ex.SqlState == "42501") // insufficient_privilege
        {
            Debug.WriteLine("[ImportStrategyFactory] No superuser privileges - using two-pass import");
            return false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ImportStrategyFactory] Error testing privileges: {ex.Message} - defaulting to two-pass");
            return false;
        }
    }
}
