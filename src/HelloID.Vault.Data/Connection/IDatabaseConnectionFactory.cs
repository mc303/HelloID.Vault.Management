using System.Data;

namespace HelloID.Vault.Data.Connection;

/// <summary>
/// Generic factory interface for creating database connections across different database types.
/// </summary>
public interface IDatabaseConnectionFactory
{
    /// <summary>
    /// Gets the database type this factory creates connections for.
    /// </summary>
    DatabaseType DatabaseType { get; }

    /// <summary>
    /// Creates and opens a new database connection with default settings.
    /// </summary>
    /// <returns>An open database connection.</returns>
    IDbConnection CreateConnection();

    /// <summary>
    /// Creates and opens a new database connection with optional foreign key enforcement.
    /// </summary>
    /// <param name="enforceForeignKeys">Whether to enforce foreign key constraints (default: true).</param>
    /// <returns>An open database connection.</returns>
    IDbConnection CreateConnection(bool enforceForeignKeys);
}
