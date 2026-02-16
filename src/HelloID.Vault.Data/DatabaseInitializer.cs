using System.Data;
using Dapper;
using HelloID.Vault.Data.Connection;

namespace HelloID.Vault.Data;

/// <summary>
/// Initializes the database by executing the schema SQL script if needed.
/// Supports both SQLite and PostgreSQL databases.
/// </summary>
public class DatabaseInitializer
{
    private readonly IDatabaseConnectionFactory _connectionFactory;
    private readonly string _schemaFilePath;
    private readonly string? _databasePath; // Only used for SQLite

    /// <summary>
    /// Initializes a new instance for SQLite.
    /// </summary>
    public DatabaseInitializer(IDatabaseConnectionFactory connectionFactory, string databasePath, string schemaFilePath)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _databasePath = databasePath ?? throw new ArgumentNullException(nameof(databasePath));
        _schemaFilePath = schemaFilePath ?? throw new ArgumentNullException(nameof(schemaFilePath));
    }

    /// <summary>
    /// Initializes a new instance for PostgreSQL (no database path needed).
    /// </summary>
    public DatabaseInitializer(IDatabaseConnectionFactory connectionFactory, string schemaFilePath)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _schemaFilePath = schemaFilePath ?? throw new ArgumentNullException(nameof(schemaFilePath));
    }

    /// <summary>
    /// Initializes the database by creating it and executing the schema if it doesn't exist.
    /// </summary>
    public async Task InitializeAsync()
    {
        var dbType = _connectionFactory.DatabaseType;
        System.Diagnostics.Debug.WriteLine($"[DatabaseInitializer] InitializeAsync called - DatabaseType: {dbType}");

        if (dbType == DatabaseType.Sqlite)
        {
            System.Diagnostics.Debug.WriteLine($"[DatabaseInitializer] Calling InitializeSqliteAsync");
            await InitializeSqliteAsync();
        }
        else if (dbType == DatabaseType.PostgreSql)
        {
            System.Diagnostics.Debug.WriteLine($"[DatabaseInitializer] Calling InitializePostgreSqlAsync");
            await InitializePostgreSqlAsync();
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"[DatabaseInitializer] Unknown database type: {dbType}");
        }
    }

    private async Task InitializeSqliteAsync()
    {
        var databaseExists = File.Exists(_databasePath!);

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

    private async Task InitializePostgreSqlAsync()
    {
        System.Diagnostics.Debug.WriteLine("Initializing PostgreSQL database...");

        // Check if schema exists by testing for critical tables
        var schemaExists = await CheckSchemaExistsAsync();

        if (!schemaExists)
        {
            System.Diagnostics.Debug.WriteLine("Schema does not exist in PostgreSQL database. Creating schema...");
            await CreateDatabaseAsync();
        }
        else
        {
            // Double-check that critical tables actually exist (not just schema_version)
            var criticalTablesExist = await CheckCriticalTablesExistAsync();
            if (!criticalTablesExist)
            {
                System.Diagnostics.Debug.WriteLine("Schema exists but critical tables are missing. Recreating schema...");
                await CreateDatabaseAsync();
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Schema already exists in PostgreSQL database.");
            }
        }

        // Verify schema version
        await VerifySchemaAsync();

        System.Diagnostics.Debug.WriteLine("PostgreSQL database initialized.");
    }

    /// <summary>
    /// Checks if critical tables exist in the database.
    /// Used to detect partial/incomplete schema states.
    /// </summary>
    private async Task<bool> CheckCriticalTablesExistAsync()
    {
        try
        {
            using var connection = _connectionFactory.CreateConnection();

            // Check for a few critical tables that must exist for the app to work
            var criticalTables = new[] { "source_system", "persons", "contracts" };

            foreach (var table in criticalTables)
            {
                var tableExists = await connection.QueryFirstOrDefaultAsync<int?>(
                    "SELECT 1 FROM information_schema.tables WHERE table_name = @table LIMIT 1",
                    new { table = table })
                    .ConfigureAwait(false);

                if (tableExists != 1)
                {
                    System.Diagnostics.Debug.WriteLine($"[DatabaseInitializer] Critical table '{table}' does not exist.");
                    return false;
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error checking critical tables: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Checks if the database schema exists by testing for the schema_version table.
    /// </summary>
    /// <returns>True if schema exists, false otherwise.</returns>
    private async Task<bool> CheckSchemaExistsAsync()
    {
        try
        {
            using var connection = _connectionFactory.CreateConnection();

            // Check if schema_version table exists
            var tableExists = await connection.QueryFirstOrDefaultAsync<int?>(
                "SELECT 1 FROM information_schema.tables WHERE table_name = 'schema_version' LIMIT 1")
                .ConfigureAwait(false);

            return tableExists == 1;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error checking if schema exists: {ex.Message}");
            return false;
        }
    }

    private async Task CreateDatabaseAsync()
    {
        System.Diagnostics.Debug.WriteLine($"[DatabaseInitializer] CreateDatabaseAsync called - Schema path: {_schemaFilePath}");

        if (!File.Exists(_schemaFilePath))
        {
            System.Diagnostics.Debug.WriteLine($"[DatabaseInitializer] SCHEMA FILE NOT FOUND: {_schemaFilePath}");
            throw new FileNotFoundException($"Schema file not found: {_schemaFilePath}");
        }

        System.Diagnostics.Debug.WriteLine($"[DatabaseInitializer] Schema file exists, reading SQL...");

        // Read schema SQL
        var schemaSql = await File.ReadAllTextAsync(_schemaFilePath).ConfigureAwait(false);
        System.Diagnostics.Debug.WriteLine($"[DatabaseInitializer] Schema SQL loaded: {schemaSql.Length} characters");

        using var connection = _connectionFactory.CreateConnection();

        // For PostgreSQL, split script into individual statements
        // Dapper ExecuteAsync only handles single statements
        if (_connectionFactory.DatabaseType == DatabaseType.PostgreSql)
        {
            await ExecutePostgreSqlScriptAsync(connection, schemaSql);
        }
        else
        {
            // SQLite can handle the entire script at once
            await connection.ExecuteAsync(schemaSql).ConfigureAwait(false);
        }

        System.Diagnostics.Debug.WriteLine("Database schema created successfully.");
    }

    /// <summary>
    /// Executes a multi-statement PostgreSQL SQL script by splitting it into individual statements.
    /// </summary>
    private async Task ExecutePostgreSqlScriptAsync(IDbConnection connection, string script)
    {
        // Split script into individual statements, handling PL/pgSQL dollar quotes
        var statements = SplitPostgreSqlStatements(script);

        System.Diagnostics.Debug.WriteLine($"[DatabaseInitializer] Split script into {statements.Count} statements");

        // Log CREATE VIEW/FUNCTION statements found
        int viewCount = 0;
        int functionCount = 0;
        foreach (var stmt in statements)
        {
            var trimmed = stmt.Trim();
            if (trimmed.StartsWith("CREATE VIEW") || trimmed.StartsWith("CREATE OR REPLACE VIEW") || trimmed.StartsWith("DROP VIEW"))
            {
                viewCount++;
                var viewName = ExtractObjectName(trimmed, "VIEW");
                System.Diagnostics.Debug.WriteLine($"[DatabaseInitializer] Found VIEW statement: {viewName} (length: {stmt.Length})");
            }
            else if (trimmed.StartsWith("CREATE FUNCTION") || trimmed.StartsWith("CREATE OR REPLACE FUNCTION"))
            {
                functionCount++;
                var funcName = ExtractObjectName(trimmed, "FUNCTION");
                System.Diagnostics.Debug.WriteLine($"[DatabaseInitializer] Found FUNCTION statement: {funcName} (length: {stmt.Length})");
            }
        }

        System.Diagnostics.Debug.WriteLine($"[DatabaseInitializer] Total: {viewCount} views, {functionCount} functions");
        System.Diagnostics.Debug.WriteLine($"[DatabaseInitializer] Executing {statements.Count} SQL statements...");

        // Log the actual CREATE VIEW statement for person_details_view to verify date casts
        foreach (var stmt in statements)
        {
            var trimmed = stmt.Trim();
            if (trimmed.StartsWith("CREATE OR REPLACE VIEW person_details_view"))
            {
                System.Diagnostics.Debug.WriteLine($"[DatabaseInitializer] person_details_view SQL (first 1000 chars):\n{stmt.Substring(0, Math.Min(1000, stmt.Length))}...");
                // Check if it contains the ::date cast fix
                if (stmt.Contains("c.end_date::date, '2999-01-01'::date"))
                {
                    System.Diagnostics.Debug.WriteLine($"[DatabaseInitializer] ✓ person_details_view contains CORRECT ::date cast on end_date");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[DatabaseInitializer] ✗ person_details_view MISSING ::date cast on end_date!");
                }
            }
        }

        var executedCount = 0;
        var errorCount = 0;

        for (int i = 0; i < statements.Count; i++)
        {
            var statement = statements[i];
            if (string.IsNullOrWhiteSpace(statement))
                continue;

            try
            {
                // Log VIEW and FUNCTION statements for debugging
                var trimmedStatement = statement.Trim();
                var statementType = trimmedStatement.StartsWith("CREATE") ? trimmedStatement.Split()[1] : "";
                if (statementType == "VIEW" || statementType == "DROP" || statementType == "OR" || statementType == "FUNCTION")
                {
                    var viewName = ExtractObjectName(trimmedStatement, "VIEW");
                    System.Diagnostics.Debug.WriteLine($"[DatabaseInitializer] [{executedCount + 1}/{statements.Count}] Executing {statementType} statement: {viewName} (length: {statement.Length})");

                    // Log the first VIEW statement in detail for debugging date casting issues
                    if (executedCount == 0 && (statementType == "VIEW" || statementType == "DROP"))
                    {
                        System.Diagnostics.Debug.WriteLine($"[DatabaseInitializer] VIEW SQL preview:\n{statement.Substring(0, Math.Min(500, statement.Length))}...");
                    }
                }

                await connection.ExecuteAsync(statement).ConfigureAwait(false);
                executedCount++;

                // Log every 50 statements to track progress
                if (executedCount % 50 == 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[DatabaseInitializer] Progress: {executedCount}/{statements.Count} statements executed...");
                }
            }
            catch (Exception ex)
            {
                errorCount++;
                var trimmedStatement = statement.Trim();
                var statementType = trimmedStatement.StartsWith("CREATE") ? trimmedStatement.Split()[1] : "UNKNOWN";
                var objectName = ExtractObjectName(trimmedStatement, statementType == "DROP" ? "VIEW" : statementType);

                System.Diagnostics.Debug.WriteLine($"[DatabaseInitializer] ERROR executing SQL statement #{i + 1}: {objectName}");
                System.Diagnostics.Debug.WriteLine($"[DatabaseInitializer] Error Type: {ex.GetType().Name}");
                System.Diagnostics.Debug.WriteLine($"[DatabaseInitializer] Error Message: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[DatabaseInitializer] Statement preview (first 300 chars): {statement.Substring(0, Math.Min(300, statement.Length))}...");

                // Log full statement for debugging
                System.Diagnostics.Debug.WriteLine($"[DatabaseInitializer] Full statement:\n{statement}");

                // For development, re-throw to see full errors
                throw;
            }
        }

        System.Diagnostics.Debug.WriteLine($"[DatabaseInitializer] Executed {executedCount} statements successfully.");
        if (errorCount > 0)
        {
            System.Diagnostics.Debug.WriteLine($"[DatabaseInitializer] Failed to execute {errorCount} statements.");
        }
    }

    /// <summary>
    /// Splits a PostgreSQL SQL script into individual statements, handling PL/pgSQL dollar-quoted strings.
    /// </summary>
    private static List<string> SplitPostgreSqlStatements(string script)
    {
        var statements = new List<string>();
        var currentStatement = new System.Text.StringBuilder();
        var i = 0;

        while (i < script.Length)
        {
            // Check for dollar-quoted string ($$...$$ or $tag$...$tag$)
            if (script[i] == '$' && i + 1 < script.Length && script[i + 1] == '$')
            {
                // Find the closing $$
                var tagStart = i;
                var tagEnd = script.IndexOf("$$", i + 2);
                if (tagEnd == -1)
                {
                    // Unclosed dollar quote - append rest and break
                    currentStatement.Append(script.AsSpan(i));
                    break;
                }
                // Append the entire dollar-quoted string including delimiters
                currentStatement.Append(script.AsSpan(i, tagEnd + 2 - i));
                i = tagEnd + 2;
                continue;
            }

            // Check for tagged dollar quote $tag$...$tag$
            if (script[i] == '$' && i + 1 < script.Length && char.IsLetter(script[i + 1]))
            {
                var tagStart = i;
                var tagNameEnd = script.IndexOf('$', i + 1);
                if (tagNameEnd != -1)
                {
                    var tag = script.Substring(i, tagNameEnd + 1 - i);
                    var closingTag = script.IndexOf(tag, tagNameEnd + 1);
                    if (closingTag != -1)
                    {
                        currentStatement.Append(script.AsSpan(i, closingTag + tag.Length - i));
                        i = closingTag + tag.Length;
                        continue;
                    }
                }
            }

            // Check for single-quoted string
            if (script[i] == '\'')
            {
                currentStatement.Append(script[i]);
                i++;
                while (i < script.Length && script[i] != '\'')
                {
                    if (script[i] == '\\' && i + 1 < script.Length)
                    {
                        currentStatement.Append(script[i]);
                        i++;
                    }
                    currentStatement.Append(script[i]);
                    i++;
                }
                if (i < script.Length)
                {
                    currentStatement.Append(script[i]);
                    i++;
                }
                continue;
            }

            // Check for statement terminator
            if (script[i] == ';')
            {
                currentStatement.Append(script[i]);
                i++;

                // Trim and add if not empty
                var stmt = currentStatement.ToString().Trim();
                if (!string.IsNullOrWhiteSpace(stmt))
                {
                    statements.Add(stmt);
                }
                currentStatement.Clear();
                continue;
            }

            currentStatement.Append(script[i]);
            i++;
        }

        // Add any remaining statement (might not have trailing semicolon)
        var remaining = currentStatement.ToString().Trim();
        if (!string.IsNullOrWhiteSpace(remaining))
        {
            statements.Add(remaining);
        }

        return statements;
    }

    /// <summary>
    /// Extracts the name of a VIEW or FUNCTION from a CREATE statement.
    /// </summary>
    private static string ExtractObjectName(string statement, string objectType)
    {
        try
        {
            // Find "VIEW" or "FUNCTION" keyword
            var keywordIndex = statement.IndexOf(objectType);
            if (keywordIndex == -1)
                return "???";

            // Find the next word after the keyword (skip "OR REPLACE", "IF NOT EXISTS", etc.)
            var afterKeyword = statement.Substring(keywordIndex + objectType.Length).Trim();
            var words = afterKeyword.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            // First word should be the object name
            if (words.Length > 0)
            {
                var name = words[0].Trim();
                // Remove schema prefix if present
                var dotIndex = name.LastIndexOf('.');
                if (dotIndex > 0)
                {
                    name = name.Substring(dotIndex + 1);
                }
                return name;
            }

            return "???";
        }
        catch
        {
            return "???";
        }
    }

    private async Task VerifySchemaAsync()
    {
        try
        {
            using var connection = _connectionFactory.CreateConnection();

            System.Diagnostics.Debug.WriteLine($"[DatabaseInitializer] VerifySchemaAsync: Checking schema version...");

            // Use Dapper for database-agnostic query execution
            var version = await connection.QueryFirstOrDefaultAsync<string?>(
                "SELECT version FROM schema_version ORDER BY version DESC LIMIT 1")
                .ConfigureAwait(false);

            System.Diagnostics.Debug.WriteLine($"Database schema version: {version}");

            // For PostgreSQL, also verify that views exist by checking person_list_view
            if (_connectionFactory.DatabaseType == DatabaseType.PostgreSql)
            {
                try
                {
                    var viewExists = await connection.QueryFirstOrDefaultAsync<int?>(
                        "SELECT 1 FROM information_schema.views WHERE table_name = 'person_list_view' LIMIT 1")
                        .ConfigureAwait(false);

                    if (viewExists == 1)
                    {
                        System.Diagnostics.Debug.WriteLine($"[DatabaseInitializer] ✓ person_list_view exists");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[DatabaseInitializer] ✗ person_list_view DOES NOT EXIST - schema may be incomplete!");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[DatabaseInitializer] Warning: Could not verify view existence: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Warning: Could not verify schema version: {ex.Message}");
        }
    }
}
