using System.Data;
using Dapper;
using Microsoft.Data.Sqlite;

namespace HelloID.Vault.Data.Connection;

/// <summary>
/// Factory for creating and configuring SQLite database connections.
/// </summary>
public class SqliteConnectionFactory : ISqliteConnectionFactory, IDatabaseConnectionFactory
{
    private readonly string _connectionString;

    /// <summary>
    /// Initializes a new instance of the <see cref="SqliteConnectionFactory"/> class.
    /// </summary>
    /// <param name="databasePath">The absolute path to the SQLite database file.</param>
    public SqliteConnectionFactory(string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            throw new ArgumentException("Database path cannot be null or whitespace.", nameof(databasePath));
        }

        _connectionString = $"Data Source={databasePath};";
        System.Diagnostics.Debug.WriteLine($"[SqliteConnectionFactory] Created with database path: {databasePath}");
    }

    /// <inheritdoc />
    public DatabaseType DatabaseType => DatabaseType.Sqlite;

    /// <inheritdoc />
    public IDbConnection CreateConnection()
    {
        return CreateConnection(enforceForeignKeys: true);
    }

    /// <summary>
    /// Creates a connection with optional foreign key enforcement control.
    /// </summary>
    /// <param name="enforceForeignKeys">Whether to enforce foreign key constraints (default: true).</param>
    /// <returns>An open database connection.</returns>
    public IDbConnection CreateConnection(bool enforceForeignKeys = true)
    {
        System.Diagnostics.Debug.WriteLine($"[SqliteConnectionFactory] CreateConnection called (enforceForeignKeys={enforceForeignKeys})");

        try
        {
            var connection = new SqliteConnection(_connectionString);
            connection.Open();
            System.Diagnostics.Debug.WriteLine($"[SqliteConnectionFactory] Connection opened successfully");

            // Configure WAL mode for optimal performance and concurrency
            connection.Execute("PRAGMA journal_mode = WAL;");
            connection.Execute("PRAGMA synchronous = NORMAL;");
            connection.Execute("PRAGMA journal_size_limit = 6144000;");

            // FK enforcement enabled by default for data integrity
            // Disable for import operations to handle missing references gracefully
            connection.Execute($"PRAGMA foreign_keys = {(enforceForeignKeys ? "ON" : "OFF")};");

            System.Diagnostics.Debug.WriteLine($"[SqliteConnectionFactory] PRAGMA configured successfully");
            return connection;
        }
        catch (SqliteException ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SqliteConnectionFactory] SqliteException: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"  SQLite Code: {ex.SqliteErrorCode}");
            System.Diagnostics.Debug.WriteLine($"  ErrorCode: {ex.ErrorCode}");
            throw;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SqliteConnectionFactory] Unexpected error: {ex.GetType().Name} - {ex.Message}");
            throw;
        }
    }
}
