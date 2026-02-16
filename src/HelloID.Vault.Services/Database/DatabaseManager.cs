using Dapper;
using HelloID.Vault.Data;
using HelloID.Vault.Data.Connection;
using Microsoft.Data.Sqlite;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

namespace HelloID.Vault.Services.Database;

/// <summary>
/// Manages database operations including backup, deletion, and data checks.
/// Supports both SQLite (file-based operations) and PostgreSQL (DROP TABLE/SEQUENCE).
/// </summary>
public class DatabaseManager : IDatabaseManager
{
    private readonly IDatabaseConnectionFactory _connectionFactory;
    private readonly DatabaseInitializer _databaseInitializer;

    public DatabaseManager(
        IDatabaseConnectionFactory connectionFactory,
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
        try
        {
            if (_connectionFactory.DatabaseType == DatabaseType.Sqlite)
            {
                await DeleteSqliteDatabaseAsync();
            }
            else if (_connectionFactory.DatabaseType == DatabaseType.PostgreSql)
            {
                await DropPostgreSqlTablesAsync();
            }
            else
            {
                throw new NotSupportedException($"Database deletion is not supported for {_connectionFactory.DatabaseType}");
            }

            // Recreate the database schema after deletion
            await _databaseInitializer.InitializeAsync();
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to delete database: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Deletes the SQLite database file.
    /// </summary>
    private async Task DeleteSqliteDatabaseAsync()
    {
        string? dbPath = null;

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
    }

    /// <summary>
    /// Drops and recreates the entire PostgreSQL public schema.
    /// This removes ALL database objects: tables, views, functions, types, enums, sequences, etc.
    /// </summary>
    private async Task DropPostgreSqlTablesAsync()
    {
        var connection = _connectionFactory.CreateConnection();

        try
        {
            // Drop the entire public schema and recreate it
            // This removes ALL objects: tables, views, functions, types, enums, sequences, etc.
            await connection.ExecuteAsync("DROP SCHEMA public CASCADE");
            await connection.ExecuteAsync("CREATE SCHEMA public");

            // Grant necessary permissions to the public schema
            await connection.ExecuteAsync("GRANT ALL ON SCHEMA public TO public");
            await connection.ExecuteAsync("GRANT ALL ON SCHEMA public TO postgres");
        }
        finally
        {
            // Explicitly close and dispose connection
            connection.Close();
            connection.Dispose();
        }

        // Clear all Npgsql connection pools to ensure fresh connections
        NpgsqlConnection.ClearAllPools();

        // Wait for connection pool to fully release
        await Task.Delay(500);
    }

    /// <inheritdoc/>
    public async Task CreateDatabaseBackupAsync()
    {
        if (_connectionFactory.DatabaseType == DatabaseType.Sqlite)
        {
            await CreateSqliteBackupAsync();
        }
        else if (_connectionFactory.DatabaseType == DatabaseType.PostgreSql)
        {
            await CreatePostgreSqlDumpAsync();
        }
        else
        {
            throw new NotSupportedException($"Database backup is not supported for {_connectionFactory.DatabaseType}");
        }
    }

    /// <summary>
    /// Creates a backup of the SQLite database by copying the database file.
    /// </summary>
    private async Task CreateSqliteBackupAsync()
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
            }

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

    /// <summary>
    /// Creates a SQL dump backup of the PostgreSQL database.
    /// </summary>
    private async Task CreatePostgreSqlDumpAsync()
    {
        string? backupPath = null;

        try
        {
            // Create backup directory in user's AppData
            var backupDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "HelloID.Vault.Management",
                "Backups");

            Directory.CreateDirectory(backupDir);

            // Create backup filename with timestamp: vault_backup_{yyyymmdd}_{hhmmss}.sql
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            backupPath = Path.Combine(backupDir, $"vault_backup_{timestamp}.sql");

            // Generate SQL dump
            var sqlDump = await GeneratePostgreSqlDumpAsync();

            // Write to file
            await File.WriteAllTextAsync(backupPath, sqlDump);
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to create PostgreSQL backup: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Generates a SQL dump of the PostgreSQL database including schema and data.
    /// </summary>
    private async Task<string> GeneratePostgreSqlDumpAsync()
    {
        var dump = new System.Text.StringBuilder();

        // Header
        dump.AppendLine("-- HelloID Vault Backup");
        dump.AppendLine($"-- Created: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        dump.AppendLine("-- Database Type: PostgreSQL");
        dump.AppendLine();
        dump.AppendLine("SET client_encoding = 'UTF8';");
        dump.AppendLine("SET standard_conforming_strings = on;");
        dump.AppendLine();

        using var connection = _connectionFactory.CreateConnection();

        // Export schema
        dump.AppendLine("-- ================================================");
        dump.AppendLine("-- Schema");
        dump.AppendLine("-- ================================================");
        dump.AppendLine(await ExportSchemaAsync(connection));
        dump.AppendLine();

        // Export data
        dump.AppendLine("-- ================================================");
        dump.AppendLine("-- Data");
        dump.AppendLine("-- ================================================");
        dump.AppendLine(await ExportDataAsync(connection));

        return dump.ToString();
    }

    /// <summary>
    /// Exports the database schema (CREATE TABLE statements).
    /// </summary>
    private async Task<string> ExportSchemaAsync(IDbConnection connection)
    {
        var schema = new System.Text.StringBuilder();

        // Get all tables in the public schema, excluding the schema_version table
        var tables = await connection.QueryAsync<string>(
            "SELECT table_name FROM information_schema.tables " +
            "WHERE table_schema = 'public' AND table_type = 'BASE TABLE' " +
            "AND table_name != 'schema_version' " +
            "ORDER BY table_name");

        foreach (var table in tables)
        {
            schema.AppendLine(await ExportTableSchemaAsync(connection, table));
        }

        return schema.ToString();
    }

    /// <summary>
    /// Exports the CREATE TABLE statement for a specific table.
    /// </summary>
    private async Task<string> ExportTableSchemaAsync(IDbConnection connection, string tableName)
    {
        var schema = new System.Text.StringBuilder();

        // Get column definitions
        var columns = await connection.QueryAsync(
            @"SELECT column_name, data_type, is_nullable, column_default
              FROM information_schema.columns
              WHERE table_name = @tableName
              ORDER BY ordinal_position",
            new { tableName });

        schema.AppendLine($"DROP TABLE IF EXISTS {tableName} CASCADE;");
        schema.AppendLine($"CREATE TABLE {tableName} (");

        var columnDefs = new List<string>();
        foreach (var col in columns)
        {
            var colDef = $"    {col.column_name} {MapPostgreSqlType(col.data_type)}";

            if (col.is_nullable == "NO")
                colDef += " NOT NULL";

            if (!string.IsNullOrEmpty(col.column_default))
                colDef += $" DEFAULT {col.column_default}";

            columnDefs.Add(colDef);
        }

        schema.AppendLine(string.Join(",\n", columnDefs));
        schema.AppendLine(");");
        schema.AppendLine();

        return schema.ToString();
    }

    /// <summary>
    /// Maps PostgreSQL data types to SQL dump format.
    /// </summary>
    private string MapPostgreSqlType(string dataType)
    {
        return dataType.ToUpper() switch
        {
            "INTEGER" => "INTEGER",
            "TEXT" => "TEXT",
            "BOOLEAN" => "BOOLEAN",
            "TIMESTAMP" => "TIMESTAMP",
            _ => dataType
        };
    }

    /// <summary>
    /// Exports data from all tables as INSERT statements.
    /// </summary>
    private async Task<string> ExportDataAsync(IDbConnection connection)
    {
        var data = new System.Text.StringBuilder();

        // Get all tables in dependency order (source_system first, then others)
        var tables = new[]
        {
            "source_system",
            "persons",
            "organizations",
            "locations",
            "cost_centers",
            "cost_bearers",
            "employers",
            "teams",
            "divisions",
            "titles",
            "departments",
            "contacts",
            "contracts",
            "custom_field_schemas",
            "primary_contract_config"
        };

        foreach (var table in tables)
        {
            var tableData = await ExportTableDataAsync(connection, table);
            if (!string.IsNullOrEmpty(tableData))
            {
                data.AppendLine(tableData);
            }
        }

        return data.ToString();
    }

    /// <summary>
    /// Exports data from a specific table as INSERT statements.
    /// </summary>
    private async Task<string> ExportTableDataAsync(IDbConnection connection, string tableName)
    {
        var data = new System.Text.StringBuilder();

        // Check if table has data
        var count = await connection.QueryFirstOrDefaultAsync<int?>(
            $"SELECT COUNT(*) FROM {tableName}");

        if (count == null || count == 0)
        {
            return $"-- {tableName}: no data\n";
        }

        data.AppendLine($"-- {tableName}: {count} rows");

        // Get all data from the table
        var rows = await connection.QueryAsync($"SELECT * FROM {tableName}");

        if (rows.Any())
        {
            // Get column names from first row
            var columns = ((IDictionary<string, object>)rows.First()).Keys.ToArray();

            foreach (var row in rows)
            {
                var values = new List<string>();
                var dict = (IDictionary<string, object>)row;

                foreach (var col in columns)
                {
                    var value = dict[col];
                    values.Add(FormatSqlValue(value));
                }

                var columnList = string.Join(", ", columns);
                var valueList = string.Join(", ", values);

                data.AppendLine($"INSERT INTO {tableName} ({columnList}) VALUES ({valueList});");
            }
        }

        data.AppendLine();
        return data.ToString();
    }

    /// <summary>
    /// Formats a value for SQL output (properly escaping strings, handling NULL, etc.).
    /// </summary>
    private string FormatSqlValue(object? value)
    {
        if (value == null || value == DBNull.Value)
            return "NULL";

        if (value is bool b)
            return b ? "TRUE" : "FALSE";

        if (value is DateTime dt)
            return $"'{dt:yyyy-MM-dd HH:mm:ss}'";

        if (value is string str)
        {
            // Escape single quotes by doubling them
            return $"'{str.Replace("'", "''")}'";
        }

        if (value is IConvertible)
            return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? "NULL";

        return value.ToString() ?? "NULL";
    }
}
