using HelloID.Vault.Data.Connection;
using Microsoft.Data.Sqlite;

namespace HelloID.Vault.Data;

/// <summary>
/// Initializes the SQLite database by executing the schema SQL script if needed.
/// </summary>
public class DatabaseInitializer
{
    private readonly ISqliteConnectionFactory _connectionFactory;
    private readonly string _schemaFilePath;
    private readonly string _databasePath;

    public DatabaseInitializer(ISqliteConnectionFactory connectionFactory, string databasePath, string schemaFilePath)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _databasePath = databasePath ?? throw new ArgumentNullException(nameof(databasePath));
        _schemaFilePath = schemaFilePath ?? throw new ArgumentNullException(nameof(schemaFilePath));
    }

    /// <summary>
    /// Initializes the database by creating it and executing the schema if it doesn't exist.
    /// </summary>
    public async Task InitializeAsync()
    {
        var databaseExists = File.Exists(_databasePath);

        if (!databaseExists)
        {
            System.Diagnostics.Debug.WriteLine($"Database does not exist. Creating new database at: {_databasePath}");

            // Ensure directory exists
            var directory = Path.GetDirectoryName(_databasePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Create database file and execute schema
            await CreateDatabaseAsync();
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"Database already exists at: {_databasePath}");
        }

        // Verify schema version
        await VerifySchemaAsync();
    }

    private async Task CreateDatabaseAsync()
    {
        if (!File.Exists(_schemaFilePath))
        {
            throw new FileNotFoundException($"Schema file not found: {_schemaFilePath}");
        }

        // Read schema SQL
        var schemaSql = await File.ReadAllTextAsync(_schemaFilePath).ConfigureAwait(false);

        // Execute schema - explicitly open connection to ensure fresh database file
        using var connection = (SqliteConnection)_connectionFactory.CreateConnection();
        connection.Open(); // Explicitly open to create the database file
        using var command = connection.CreateCommand();
        command.CommandText = schemaSql;
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);

        System.Diagnostics.Debug.WriteLine("Database schema created successfully.");
    }

    private async Task VerifySchemaAsync()
    {
        try
        {
            using var connection = (SqliteConnection)_connectionFactory.CreateConnection();
            connection.Open(); // Explicitly open connection
            using var command = connection.CreateCommand();

            command.CommandText = "SELECT version FROM schema_version ORDER BY version DESC LIMIT 1";
            var version = await command.ExecuteScalarAsync().ConfigureAwait(false);

            System.Diagnostics.Debug.WriteLine($"Database schema version: {version}");
        }
        catch (SqliteException ex)
        {
            System.Diagnostics.Debug.WriteLine($"Warning: Could not verify schema version: {ex.Message}");
        }
    }
}
