namespace HelloID.Vault.Data.Connection;

/// <summary>
/// Interface for Turso database client operations.
/// </summary>
public interface ITursoClient
{
    /// <summary>
    /// Gets the database URL.
    /// </summary>
    string DatabaseUrl { get; }

    /// <summary>
    /// Gets a value indicating whether the client is connected.
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Executes a query and returns the results.
    /// </summary>
    /// <typeparam name="T">The type of entity to map results to.</typeparam>
    /// <param name="sql">The SQL query.</param>
    /// <param name="parameters">Optional parameters for the query.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The query result with mapped entities.</returns>
    Task<TursoQueryResult<T>> QueryAsync<T>(string sql, object? parameters = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a query and returns the first result or default.
    /// </summary>
    /// <typeparam name="T">The type of entity to map results to.</typeparam>
    /// <param name="sql">The SQL query.</param>
    /// <param name="parameters">Optional parameters for the query.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The first result or default value.</returns>
    Task<T?> QueryFirstOrDefaultAsync<T>(string sql, object? parameters = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a query and returns a single scalar value.
    /// </summary>
    /// <typeparam name="T">The type of the scalar value.</typeparam>
    /// <param name="sql">The SQL query.</param>
    /// <param name="parameters">Optional parameters for the query.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The scalar value.</returns>
    Task<T?> ExecuteScalarAsync<T>(string sql, object? parameters = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a non-query statement (INSERT, UPDATE, DELETE).
    /// </summary>
    /// <param name="sql">The SQL statement.</param>
    /// <param name="parameters">Optional parameters for the statement.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of affected rows.</returns>
    Task<int> ExecuteAsync(string sql, object? parameters = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes multiple statements in a transaction.
    /// </summary>
    /// <param name="statements">The statements to execute.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The transaction result.</returns>
    Task<TursoTransactionResult> ExecuteTransactionAsync(IEnumerable<TursoStatement> statements, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tests the connection to the Turso database.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if connection is successful.</returns>
    Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Refreshes the authentication token.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RefreshTokenAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a table exists in the database.
    /// </summary>
    /// <param name="tableName">The table name to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the table exists.</returns>
    Task<bool> TableExistsAsync(string tableName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if the database schema is initialized (persons table exists).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if schema is initialized.</returns>
    Task<bool> IsSchemaInitializedAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a SQL script with multiple statements.
    /// </summary>
    /// <param name="sqlScript">The SQL script to execute.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of statements executed.</returns>
    Task<int> ExecuteScriptAsync(string sqlScript, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the credentials for the client.
    /// </summary>
    /// <param name="databaseUrl">The database URL.</param>
    /// <param name="authToken">The authentication token.</param>
    void UpdateCredentials(string databaseUrl, string authToken);

    /// <summary>
    /// Uploads a SQLite database file to replace the current Turso database.
    /// </summary>
    /// <param name="sqliteFilePath">Path to the SQLite database file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if upload was successful.</returns>
    Task<bool> UploadDatabaseAsync(string sqliteFilePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Event raised when connection status changes.
    /// </summary>
    event EventHandler<bool>? ConnectionStatusChanged;
}
