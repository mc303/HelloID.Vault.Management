using System.Data;

namespace HelloID.Vault.Data.Connection;

/// <summary>
/// Factory interface for creating SQLite database connections.
/// </summary>
public interface ISqliteConnectionFactory
{
    /// <summary>
    /// Creates and opens a new SQLite database connection.
    /// </summary>
    /// <returns>An open database connection.</returns>
    IDbConnection CreateConnection();

    /// <summary>
    /// Creates and opens a new SQLite database connection with optional foreign key enforcement.
    /// </summary>
    /// <param name="enforceForeignKeys">Whether to enforce foreign key constraints (default: true).</param>
    /// <returns>An open database connection.</returns>
    IDbConnection CreateConnection(bool enforceForeignKeys);
}
