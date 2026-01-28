using Dapper;
using HelloID.Vault.Data;
using HelloID.Vault.Data.Connection;
using Microsoft.Data.Sqlite;

namespace HelloID.Vault.Services.Database;

/// <summary>
/// Manages database operations including backup, deletion, and data checks.
/// </summary>
public class DatabaseManager : IDatabaseManager
{
    private readonly ISqliteConnectionFactory _connectionFactory;
    private readonly DatabaseInitializer _databaseInitializer;

    public DatabaseManager(
        ISqliteConnectionFactory connectionFactory,
        DatabaseInitializer databaseInitializer)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _databaseInitializer = databaseInitializer ?? throw new ArgumentNullException(nameof(databaseInitializer));
    }

    /// <inheritdoc/>
    public async Task<bool> HasDataAsync()
    {
        try
        {
            using var connection = _connectionFactory.CreateConnection();

            // Check if any of the main tables have data
            var tables = new[] { "persons", "contracts", "departments", "contacts", "custom_field_schemas" };

            foreach (var table in tables)
            {
                var count = await connection.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM {table}");
                if (count > 0)
                {
                    return true;
                }
            }

            return false;
        }
        catch
        {
            // If database doesn't exist or tables don't exist, consider it empty
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task DeleteDatabaseAsync()
    {
        string? dbPath = null;

        try
        {
            // Get the database file path
            using (var connection = _connectionFactory.CreateConnection(enforceForeignKeys: false))
            {
                var sqliteConnection = connection as SqliteConnection;
                if (sqliteConnection == null)
                {
                    throw new Exception("Connection is not a SQLite connection");
                }

                dbPath = sqliteConnection.DataSource;

                if (string.IsNullOrEmpty(dbPath) || !File.Exists(dbPath))
                {
                    // Database doesn't exist, nothing to delete
                    return;
                }

                // Close connection explicitly
                connection.Close();
            } // Dispose connection here

            // Clear all connections from the pool to release file locks
            SqliteConnection.ClearAllPools();

            // Wait for file locks to be fully released
            await Task.Delay(500);

            // Delete database files
            File.Delete(dbPath);

            var walPath = dbPath + "-wal";
            var shmPath = dbPath + "-shm";

            if (File.Exists(walPath))
            {
                File.Delete(walPath);
            }

            if (File.Exists(shmPath))
            {
                File.Delete(shmPath);
            }

            // Recreate the database schema after deletion
            await _databaseInitializer.InitializeAsync();
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to delete database: {ex.Message}", ex);
        }
    }

    /// <inheritdoc/>
    public async Task CreateDatabaseBackupAsync()
    {
        string? dbPath = null;

        try
        {
            // Get the database file path from connection factory
            using (var connection = _connectionFactory.CreateConnection(enforceForeignKeys: false))
            {
                var sqliteConnection = connection as SqliteConnection;
                if (sqliteConnection == null)
                {
                    throw new Exception("Connection is not a SQLite connection");
                }

                dbPath = sqliteConnection.DataSource;

                if (string.IsNullOrEmpty(dbPath) || !File.Exists(dbPath))
                {
                    throw new Exception("Database file not found: " + dbPath);
                }

                // Close connection explicitly
                connection.Close();
            } // Dispose connection here

            // Clear all connections from the pool to release file locks
            SqliteConnection.ClearAllPools();

            // Wait for file locks to be fully released
            await Task.Delay(500);

            // Create backup filename with timestamp: vault_backup_{yyyymmdd}_{hhmmss}.db
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var dbDirectory = Path.GetDirectoryName(dbPath) ?? string.Empty;
            var backupPath = Path.Combine(dbDirectory, $"vault_backup_{timestamp}.db");

            // Copy database file to backup location
            File.Copy(dbPath, backupPath, overwrite: false);

            // Also copy WAL and SHM files if they exist
            var walPath = dbPath + "-wal";
            var shmPath = dbPath + "-shm";

            if (File.Exists(walPath))
            {
                File.Copy(walPath, backupPath + "-wal", overwrite: false);
            }

            if (File.Exists(shmPath))
            {
                File.Copy(shmPath, backupPath + "-shm", overwrite: false);
            }

            // Now delete the original database files to start fresh
            File.Delete(dbPath);

            if (File.Exists(walPath))
            {
                File.Delete(walPath);
            }

            if (File.Exists(shmPath))
            {
                File.Delete(shmPath);
            }

            // Recreate the database schema after deletion
            await _databaseInitializer.InitializeAsync();
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to create database backup: {ex.Message}", ex);
        }
    }
}
