using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Dapper;
using Microsoft.Data.Sqlite;

namespace HelloID.Vault.Data.Connection;

/// <summary>
/// HTTP client for Turso database operations using Hrana v2 protocol.
/// </summary>
public class TursoClient : ITursoClient, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;
    private string _databaseUrl;
    private string _authToken;
    private bool _isConnected;
    private bool _disposed;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private const int MaxRetries = 3;
    private const int BaseDelayMs = 1000;
    private const string PipelineEndpoint = "/v2/pipeline";

    public string DatabaseUrl => _databaseUrl;
    public bool IsConnected => _isConnected;

    public event EventHandler<bool>? ConnectionStatusChanged;

    public TursoClient(string databaseUrl, string authToken)
    {
        if (string.IsNullOrWhiteSpace(databaseUrl))
            throw new ArgumentException("Database URL cannot be null or empty.", nameof(databaseUrl));
        if (string.IsNullOrWhiteSpace(authToken))
            throw new ArgumentException("Auth token cannot be null or empty.", nameof(authToken));

        _databaseUrl = NormalizeDatabaseUrl(databaseUrl);
        _authToken = authToken;

        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            PropertyNameCaseInsensitive = true,
            // Omit nulls so request items like {"type":"close"} serialize cleanly
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        UpdateHttpClientHeaders();
        Debug.WriteLine($"[TursoClient] Initialized with database URL: {_databaseUrl}");
    }

    private void UpdateHttpClientHeaders()
    {
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_authToken}");
    }

    public void UpdateCredentials(string databaseUrl, string authToken)
    {
        if (string.IsNullOrWhiteSpace(databaseUrl))
            throw new ArgumentException("Database URL cannot be null or empty.", nameof(databaseUrl));
        if (string.IsNullOrWhiteSpace(authToken))
            throw new ArgumentException("Auth token cannot be null or empty.", nameof(authToken));

        _databaseUrl = NormalizeDatabaseUrl(databaseUrl);
        _authToken = authToken;
        UpdateHttpClientHeaders();
        Debug.WriteLine($"[TursoClient] Credentials updated, URL: {_databaseUrl}");
    }

    public async Task<TursoQueryResult<T>> QueryAsync<T>(string sql, object? parameters = null, CancellationToken cancellationToken = default)
    {
        Debug.WriteLine($"[TursoClient] QueryAsync: {sql}");

        var statement = CreateStatement(sql, parameters, wantRows: true);
        var response = await ExecuteWithRetryAsync(statement, cancellationToken);

        return MapResult<T>(response);
    }

    public async Task<T?> QueryFirstOrDefaultAsync<T>(string sql, object? parameters = null, CancellationToken cancellationToken = default)
    {
        var result = await QueryAsync<T>(sql, parameters, cancellationToken);
        return result.Rows.FirstOrDefault();
    }

    public async Task<T?> ExecuteScalarAsync<T>(string sql, object? parameters = null, CancellationToken cancellationToken = default)
    {
        var result = await QueryAsync<T>(sql, parameters, cancellationToken);
        return result.Rows.FirstOrDefault();
    }

    public async Task<int> ExecuteAsync(string sql, object? parameters = null, CancellationToken cancellationToken = default)
    {
        Debug.WriteLine($"[TursoClient] ExecuteAsync: {sql}");

        var statement = CreateStatement(sql, parameters, wantRows: false);

        // Writes are executed without retries: a lost response may hide a
        // committed statement and re-running it could duplicate rows
        var response = await ExecuteWithRetryAsync(statement, cancellationToken, isWrite: true);

        var resultItem = response.Results.FirstOrDefault();
        return resultItem?.Response?.Result?.AffectedRowCount ?? 0;
    }

    public async Task<TursoTransactionResult> ExecuteTransactionAsync(IEnumerable<TursoStatement> statements, CancellationToken cancellationToken = default)
    {
        var statementList = statements.ToList();
        Debug.WriteLine($"[TursoClient] ExecuteTransactionAsync: {statementList.Count} statements");

        if (statementList.Count == 0)
        {
            return new TursoTransactionResult { Success = true, Errors = [] };
        }

        // BEGIN/COMMIT as statements inside ONE pipeline keep the group on a single
        // connection and commit atomically. Turso Cloud rolls back open transactions
        // after 5 seconds, so statements run in chunks of 25: each chunk is atomic,
        // a failed chunk aborts the remaining chunks and the error surfaces instead
        // of being silently dropped. Large bulk writes belong in temp-file + /v1/upload.
        const int chunkSize = 25;
        var totalAffectedRows = 0;

        for (var offset = 0; offset < statementList.Count; offset += chunkSize)
        {
            var chunk = statementList.Skip(offset).Take(chunkSize).ToList();
            var chunkNumber = offset / chunkSize + 1;

            var request = new TursoPipelineRequest
            {
                Requests =
                [
                    new TursoRequestItem { Type = "execute", Statement = new TursoStatement { Sql = "BEGIN", WantRows = false } },
                    ..chunk.Select(s => new TursoRequestItem { Type = "execute", Statement = s }),
                    new TursoRequestItem { Type = "execute", Statement = new TursoStatement { Sql = "COMMIT", WantRows = false } },
                    new TursoRequestItem { Type = "close" }
                ]
            };

            var errors = new List<(string Label, TursoError Error)>();

            try
            {
                var response = await SendPipelineRequestAsync(request, cancellationToken);

                // Result layout: [0]=BEGIN, [1..n]=chunk statements, [n+1]=COMMIT, [n+2]=close
                for (var i = 0; i < response.Results.Count; i++)
                {
                    var resultItem = response.Results[i];
                    if (resultItem.Error != null)
                    {
                        var label = i switch
                        {
                            0 => "BEGIN",
                            var n when n == chunk.Count + 1 => "COMMIT",
                            _ => $"statement {offset + i - 1} ({PreviewSql(statementList[offset + i - 1].Sql)})"
                        };
                        Debug.WriteLine($"[TursoClient] Transaction chunk {chunkNumber} {label} failed: {resultItem.Error.Message}");
                        errors.Add((label, resultItem.Error));
                    }
                    else if (resultItem.Response?.Result != null)
                    {
                        totalAffectedRows += resultItem.Response.Result.AffectedRowCount;
                    }
                }
            }
            catch (TursoException)
            {
                // Auth, network, rate-limit errors keep their type so
                // upstream handlers can react to the real cause
                throw;
            }
            catch (Exception ex)
            {
                throw new TursoConnectionException($"Transaction chunk {chunkNumber} failed: {ex.Message}", ex);
            }

            if (errors.Count > 0)
            {
                var detail = string.Join("; ", errors.Select(e => $"{e.Label}: {e.Error.Message}"));
                throw new TursoQueryException($"Transaction chunk {chunkNumber} failed: {detail}");
            }
        }

        return new TursoTransactionResult
        {
            Success = true,
            TotalAffectedRows = totalAffectedRows,
            Errors = []
        };
    }

    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        Debug.WriteLine($"[TursoClient] TestConnectionAsync - URL: {_databaseUrl}");

        try
        {
            var result = await QueryAsync<int>("SELECT 1", null, cancellationToken);
            Debug.WriteLine($"[TursoClient] TestConnectionAsync result: Success={result.Success}, Rows={result.Rows.Count}, Error={result.Error?.Message}");
            var success = result.Success && result.Rows.Count > 0;
            SetConnectionStatus(success);
            return success;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[TursoClient] TestConnectionAsync failed: {ex.GetType().Name} - {ex.Message}");
            SetConnectionStatus(false);
            throw;
        }
    }

    public async Task RefreshTokenAsync(CancellationToken cancellationToken = default)
    {
        Debug.WriteLine("[TursoClient] RefreshTokenAsync - Note: Token refresh requires Platform API");

        await Task.CompletedTask;
    }

    /// <summary>
    /// Checks if a table exists in the database.
    /// </summary>
    /// <param name="tableName">The table name to check</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the table exists</returns>
    public async Task<bool> TableExistsAsync(string tableName, CancellationToken cancellationToken = default)
    {
        Debug.WriteLine($"[TursoClient] Checking if table '{tableName}' exists");

        const string sql = "SELECT name FROM sqlite_master WHERE type='table' AND name=?";
        var result = await QueryFirstOrDefaultAsync<string>(sql, new { name = tableName }, cancellationToken);
        var exists = !string.IsNullOrEmpty(result);
        Debug.WriteLine($"[TursoClient] Table '{tableName}' exists: {exists}");
        return exists;
    }

    /// <summary>
    /// Checks if the database schema is initialized (persons table exists).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if schema is initialized</returns>
    public async Task<bool> IsSchemaInitializedAsync(CancellationToken cancellationToken = default)
    {
        Debug.WriteLine("[TursoClient] Checking if schema is initialized");
        return await TableExistsAsync("persons", cancellationToken);
    }

    /// <summary>
    /// Executes a SQL script with multiple statements.
    /// Splits by semicolon and executes each statement separately.
    /// </summary>
    /// <param name="sqlScript">The SQL script to execute</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of statements executed</returns>
    public async Task<int> ExecuteScriptAsync(string sqlScript, CancellationToken cancellationToken = default)
    {
        Debug.WriteLine("[TursoClient] ExecuteScriptAsync: Executing SQL script");

        if (string.IsNullOrWhiteSpace(sqlScript))
        {
            Debug.WriteLine("[TursoClient] Empty script, nothing to execute");
            return 0;
        }

        var statements = SplitSqlStatements(sqlScript);

        Debug.WriteLine($"[TursoClient] Found {statements.Count} statements to execute");

        var executed = 0;
        foreach (var statement in statements)
        {
            // No swallowing here - a failed CREATE TABLE must not silently produce
            // a half-initialized database
            await ExecuteAsync(statement, null, cancellationToken);
            executed++;
        }

        Debug.WriteLine($"[TursoClient] Executed {executed}/{statements.Count} statements");
        return executed;
    }

    /// <summary>
    /// Splits a SQL script into individual statements, keeping trigger bodies
    /// (CREATE TRIGGER ... BEGIN ... END;) intact despite their inner semicolons.
    /// </summary>
    internal static List<string> SplitSqlStatements(string sqlScript)
    {
        var statements = new List<string>();
        var current = new StringBuilder();
        var inTrigger = false;

        foreach (var rawLine in sqlScript.Split('\n'))
        {
            var trimmed = rawLine.Trim().TrimEnd('\r');

            // Skip standalone comment lines between statements
            if (trimmed.StartsWith("--", StringComparison.Ordinal) && current.Length == 0)
            {
                continue;
            }

            current.AppendLine(rawLine);

            if (inTrigger)
            {
                // The trigger body ends at a line containing only END;
                if (trimmed.Equals("END;", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.Equals("END", StringComparison.OrdinalIgnoreCase))
                {
                    inTrigger = false;
                    FlushStatement(statements, current);
                }
            }
            else if (trimmed.StartsWith("CREATE TRIGGER", StringComparison.OrdinalIgnoreCase))
            {
                inTrigger = true;
            }
            else if (trimmed.EndsWith(";"))
            {
                FlushStatement(statements, current);
            }
        }

        FlushStatement(statements, current);
        return statements;
    }

    private static void FlushStatement(List<string> statements, StringBuilder current)
    {
        var sql = current.ToString().Trim();
        current.Clear();
        if (!string.IsNullOrWhiteSpace(sql))
        {
            statements.Add(sql);
        }
    }

    public async Task<bool> UploadDatabaseAsync(string sqliteFilePath, CancellationToken cancellationToken = default)
    {
        Debug.WriteLine($"[TursoClient] UploadDatabaseAsync: {sqliteFilePath}");

        if (!File.Exists(sqliteFilePath))
        {
            Debug.WriteLine($"[TursoClient] Upload failed: File not found - {sqliteFilePath}");
            throw new FileNotFoundException($"SQLite file not found: {sqliteFilePath}");
        }

        try
        {
            var fileInfo = new FileInfo(sqliteFilePath);
            var fileSize = fileInfo.Length;
            Debug.WriteLine($"[TursoClient] File size: {fileSize} bytes ({fileSize / 1024.0 / 1024.0:F2} MB)");

            var uploadUrl = $"{ConvertLibSqlToHttps(_databaseUrl).TrimEnd('/')}/v1/upload";
            Debug.WriteLine($"[TursoClient] Uploading to: {uploadUrl}");

            // Read file into memory to avoid stream locking issues
            Debug.WriteLine($"[TursoClient] Reading file into memory...");
            byte[] fileBytes = await File.ReadAllBytesAsync(sqliteFilePath, cancellationToken);
            Debug.WriteLine($"[TursoClient] File loaded: {fileBytes.Length} bytes");

            using var content = new ByteArrayContent(fileBytes);
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
            content.Headers.ContentLength = fileBytes.Length;

            // Create a new HttpClient with longer timeout for uploads
            using var uploadClient = new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(10) // 10 minutes for large uploads
            };
            uploadClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _authToken);

            Debug.WriteLine($"[TursoClient] Starting upload...");
            var response = await uploadClient.PostAsync(uploadUrl, content, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                Debug.WriteLine($"[TursoClient] Upload successful: {(int)response.StatusCode}");
                SetConnectionStatus(true);
                return true;
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                Debug.WriteLine($"[TursoClient] Upload failed: {(int)response.StatusCode} - {errorContent}");
                SetConnectionStatus(false);

                // Throw exception so VaultImportService can handle it (e.g., auto-create database)
                throw new TursoConnectionException($"Upload failed: {(int)response.StatusCode} - {errorContent}");
            }
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            Debug.WriteLine($"[TursoClient] Upload timeout: {ex.Message}");
            SetConnectionStatus(false);
            throw new TursoConnectionException("Upload timed out. The database file may be too large or the connection is too slow.", ex);
        }
        catch (HttpRequestException ex)
        {
            Debug.WriteLine($"[TursoClient] Upload HTTP error: {ex.Message}");
            SetConnectionStatus(false);

            // Provide helpful message for common upload issues
            var errorMessage = ex.Message.Contains("copying content", StringComparison.OrdinalIgnoreCase)
                ? "Upload failed. The database file may be too large for direct upload. Try using 'turso db import' CLI command instead."
                : $"Failed to upload database: {ex.Message}";
            throw new TursoConnectionException(errorMessage, ex);
        }
        catch (IOException ex)
        {
            Debug.WriteLine($"[TursoClient] Upload IO error: {ex.Message}");
            SetConnectionStatus(false);
            throw new TursoConnectionException($"Failed to read database file: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[TursoClient] Upload exception: {ex.GetType().Name} - {ex.Message}");
            Debug.WriteLine($"[TursoClient] Stack trace: {ex.StackTrace}");
            SetConnectionStatus(false);
            throw new TursoConnectionException($"Failed to upload database: {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    public async Task<string> DownloadDatabaseAsync(string backupFilePath, CancellationToken cancellationToken = default)
    {
        Debug.WriteLine($"[TursoClient] DownloadDatabaseAsync: {backupFilePath}");

        var dumpUrl = $"{ConvertLibSqlToHttps(_databaseUrl).TrimEnd('/')}/dump";
        Debug.WriteLine($"[TursoClient] Downloading dump from: {dumpUrl}");

        string dumpSql;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, dumpUrl);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _authToken);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                Debug.WriteLine($"[TursoClient] Dump failed: {(int)response.StatusCode} - {errorContent}");
                throw new TursoConnectionException($"Dump failed: {(int)response.StatusCode} - {errorContent}", (int)response.StatusCode);
            }

            dumpSql = await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (TursoException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new TursoConnectionException($"Failed to download database dump: {ex.Message}", ex);
        }

        Debug.WriteLine($"[TursoClient] Dump downloaded: {dumpSql.Length} characters");

        // Build a local SQLite file from the dump. Remove the dump's own
        // BEGIN TRANSACTION/COMMIT wrapper, then execute statement by statement:
        // Turso dumps contain FTS5 virtual-table structures and internal libSQL
        // statements (with rowid syntax) that vanilla SQLite cannot recreate.
        // Those are derived index data, not user data, so they are skipped.
        var script = string.Join('\n', dumpSql.Split('\n')
            .Where(l => !l.Trim().Equals("BEGIN TRANSACTION;", StringComparison.OrdinalIgnoreCase) &&
                        !l.Trim().Equals("COMMIT;", StringComparison.OrdinalIgnoreCase)));

        var statements = SplitSqlStatements(script);
        var executed = 0;
        var skipped = 0;

        if (File.Exists(backupFilePath))
        {
            File.Delete(backupFilePath);
        }
        foreach (var sidecar in new[] { "-wal", "-shm" })
        {
            if (File.Exists(backupFilePath + sidecar))
            {
                File.Delete(backupFilePath + sidecar);
            }
        }

        try
        {
            var backupFactory = new SqliteConnectionFactory(backupFilePath);
            using var connection = backupFactory.CreateConnection(enforceForeignKeys: false);

            foreach (var statement in statements)
            {
                try
                {
                    await connection.ExecuteAsync(statement);
                    executed++;
                }
                catch (SqliteException ex) when (IsTursoInternalStatement(statement))
                {
                    skipped++;
                    Debug.WriteLine($"[TursoClient] Backup skipped Turso-internal statement ({ex.Message}): {statement[..Math.Min(60, statement.Length)]}...");
                }
            }

            SqliteConnection.ClearAllPools();
            await Task.Delay(200);

            Debug.WriteLine($"[TursoClient] Backup created: {backupFilePath} ({executed} statements executed, {skipped} internal skipped)");
            if (executed == 0)
            {
                throw new TursoConnectionException("Backup failed - no dump statements could be applied");
            }

            return backupFilePath;
        }
        catch (Exception ex) when (ex is not TursoException)
        {
            throw new TursoConnectionException($"Failed to write backup file '{backupFilePath}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// True for statements from a Turso dump that recreate derived/internal
    /// structures (FTS5 indexes, libSQL internals) rather than user data.
    /// </summary>
    private static string PreviewSql(string sql)
    {
        var trimmed = sql.Trim();
        return trimmed[..Math.Min(60, trimmed.Length)];
    }

    private static bool IsTursoInternalStatement(string statement)
    {
        // Skip only structural dump statements: virtual tables, access-method
        // indexes, libSQL internals and FTS5 shadow tables. Data statements
        // (INSERT/UPDATE/DELETE on user tables) always execute, even when
        // their values contain words like "USING" or "_fts".
        if (statement.StartsWith("CREATE VIRTUAL TABLE", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if ((statement.StartsWith("CREATE INDEX", StringComparison.OrdinalIgnoreCase) ||
             statement.StartsWith("CREATE UNIQUE INDEX", StringComparison.OrdinalIgnoreCase)) &&
            statement.Contains(" USING ", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (statement.StartsWith("CREATE TRIGGER", StringComparison.OrdinalIgnoreCase) &&
            statement.Contains("_fts", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return System.Text.RegularExpressions.Regex.IsMatch(
            statement,
            @"\b(?:INTO|TABLE|UPDATE)\s+""?(?:libsql_\w+|\w+_fts(?:_\w+)?)\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    private TursoStatement CreateStatement(string sql, object? parameters, bool wantRows = true)
    {
        var statement = new TursoStatement
        {
            Sql = sql,
            WantRows = wantRows
        };

        if (parameters != null)
        {
            statement.Args = ConvertParameters(parameters);
            Debug.WriteLine($"[TursoClient] Created statement with {statement.Args.Count} parameters");
        }

        return statement;
    }

    private List<TursoValue> ConvertParameters(object parameters)
    {
        var args = new List<TursoValue>();

        if (parameters is IDictionary<string, object?> dict)
        {
            foreach (var kvp in dict)
            {
                var tursoValue = ConvertToTursoValue(kvp.Value);
                // Log names and types only - values contain personal data and must not reach the log file
                Debug.WriteLine($"[TursoClient] Param: {kvp.Key} (type={kvp.Value?.GetType().Name ?? "null"}, turso={tursoValue.Type})");
                args.Add(tursoValue);
            }
        }
        else
        {
            var properties = parameters.GetType().GetProperties();

            foreach (var prop in properties)
            {
                var value = prop.GetValue(parameters);
                var tursoValue = ConvertToTursoValue(value);
                // Log names and types only - values contain personal data and must not reach the log file
                Debug.WriteLine($"[TursoClient] Param: {prop.Name} (type={value?.GetType().Name ?? "null"}, turso={tursoValue.Type})");
                args.Add(tursoValue);
            }
        }

        return args;
    }

    private static TursoValue ConvertToTursoValue(object? value)
    {
        if (value == null || value == DBNull.Value)
            return TursoValue.Null();

        if (value is string str && string.IsNullOrWhiteSpace(str))
            return TursoValue.Null();

        return value switch
        {
            long l => TursoValue.Integer(l),
            int i => TursoValue.Integer(i),
            double d => TursoValue.Float(d),
            float f => TursoValue.Float(f),
            decimal dec => TursoValue.Float((double)dec),
            bool b => TursoValue.Integer(b ? 1 : 0),
            byte[] bytes => TursoValue.Blob(bytes),
            DateTime dt => TursoValue.Text(dt.ToString("O")),
            DateTimeOffset dto => TursoValue.Text(dto.ToString("O")),
            Guid g => TursoValue.Text(g.ToString()),
            string s => TursoValue.Text(s),
            _ => TursoValue.Text(value.ToString() ?? string.Empty)
        };
    }

    private async Task<TursoPipelineResponse> ExecuteWithRetryAsync(TursoStatement statement, CancellationToken cancellationToken, bool isWrite = false)
    {
        var request = new TursoPipelineRequest
        {
            Requests = [new TursoRequestItem { Type = "execute", Statement = statement }]
        };

        Exception? lastException = null;

        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            try
            {
                var response = await SendPipelineRequestAsync(request, cancellationToken);
                SetConnectionStatus(true);
                return response;
            }
            catch (TursoRateLimitException ex)
            {
                // Safe to retry for writes too - a rate limit rejects the request before execution
                Debug.WriteLine($"[TursoClient] Rate limited, waiting {ex.RetryAfterSeconds}s");
                await Task.Delay(TimeSpan.FromSeconds(ex.RetryAfterSeconds), cancellationToken);
                lastException = ex;
            }
            catch (TursoAuthException)
            {
                throw;
            }
            catch (TursoNetworkException ex) when (ex.IsOffline)
            {
                SetConnectionStatus(false);

                // A write whose response was lost may have committed server-side;
                // re-running it could duplicate rows, so writes fail fast instead
                if (isWrite)
                {
                    throw;
                }

                Debug.WriteLine($"[TursoClient] Network offline, attempt {attempt + 1}/{MaxRetries}");
                lastException = ex;
                if (attempt < MaxRetries - 1)
                {
                    await Task.Delay(BaseDelayMs * (attempt + 1), cancellationToken);
                }
            }
            catch (TursoConnectionException ex)
            {
                SetConnectionStatus(false);

                if (isWrite)
                {
                    throw;
                }

                Debug.WriteLine($"[TursoClient] Connection error, attempt {attempt + 1}/{MaxRetries}: {ex.Message}");
                lastException = ex;
                if (attempt < MaxRetries - 1)
                {
                    await Task.Delay(BaseDelayMs * (attempt + 1), cancellationToken);
                }
            }
            catch (HttpRequestException ex)
            {
                SetConnectionStatus(false);

                if (isWrite)
                {
                    throw;
                }

                Debug.WriteLine($"[TursoClient] HTTP error, attempt {attempt + 1}/{MaxRetries}: {ex.Message}");
                lastException = new TursoConnectionException(ex.Message, ex);
                if (attempt < MaxRetries - 1)
                {
                    await Task.Delay(BaseDelayMs * (attempt + 1), cancellationToken);
                }
            }
        }

        SetConnectionStatus(false);
        throw lastException ?? new TursoConnectionException("Max retries exceeded");
    }

    private async Task<TursoPipelineResponse> SendPipelineRequestAsync(TursoPipelineRequest request, CancellationToken cancellationToken)
    {
        var url = $"{ConvertLibSqlToHttps(_databaseUrl).TrimEnd('/')}{PipelineEndpoint}";
        Debug.WriteLine($"[TursoClient] Sending request to: {url}");

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.PostAsJsonAsync(url, request, _jsonOptions, cancellationToken);
        }
        catch (HttpRequestException ex) when (ex.InnerException is System.Net.Sockets.SocketException)
        {
            throw new TursoNetworkException("Network is unreachable", ex, isOffline: true);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TursoConnectionException("Request timed out", ex);
        }

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new TursoAuthException("Authentication failed. Token may be expired or invalid.", isTokenExpired: true);
        }

        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            throw new TursoAuthException("Access forbidden. Check database permissions.", isTokenInvalid: true);
        }

        if ((int)response.StatusCode == 429)
        {
            var retryAfter = response.Headers.RetryAfter?.Delta?.TotalSeconds ?? 60;
            throw new TursoRateLimitException("Rate limit exceeded", (int)retryAfter);
        }

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            Debug.WriteLine($"[TursoClient] Error response: {response.StatusCode} - {errorContent}");
            Debug.WriteLine($"[TursoClient] Request URL was: {url}");
            throw new TursoConnectionException($"Request failed with status {response.StatusCode}. Response: {errorContent}. URL: {url}", (int)response.StatusCode);
        }

        var pipelineResponse = await response.Content.ReadFromJsonAsync<TursoPipelineResponse>(_jsonOptions, cancellationToken);
        if (pipelineResponse == null)
        {
            throw new TursoConnectionException("Failed to parse response");
        }

        Debug.WriteLine($"[TursoClient] Response received with {pipelineResponse.Results.Count} result(s)");

        var error = pipelineResponse.Results.FirstOrDefault()?.Error;
        if (error != null)
        {
            Debug.WriteLine($"[TursoClient] Query error: {error.Message} (type: {error.Type}, code: {error.Code})");
            throw new TursoQueryException(error.Message, error.Code, error.Type);
        }

        var tursoResult = pipelineResponse.Results.FirstOrDefault()?.Response?.Result;
        if (tursoResult != null)
        {
            Debug.WriteLine($"[TursoClient] Result: {tursoResult.Rows.Count} rows, {tursoResult.Columns.Count} columns, affected: {tursoResult.AffectedRowCount}");
            if (tursoResult.Rows.Count > 0)
            {
                Debug.WriteLine($"[TursoClient] First row has {tursoResult.Rows[0].Count} values");
            }
        }

        return pipelineResponse;
    }

    private TursoQueryResult<T> MapResult<T>(TursoPipelineResponse response)
    {
        var result = new TursoQueryResult<T>();

        var resultItem = response.Results.FirstOrDefault();
        if (resultItem?.Error != null)
        {
            result.Success = false;
            result.Error = resultItem.Error;
            return result;
        }

        var tursoResult = resultItem?.Response?.Result;
        if (tursoResult == null)
        {
            result.Success = true;
            return result;
        }

        result.AffectedRowCount = tursoResult.AffectedRowCount;
        result.LastInsertRowId = tursoResult.LastInsertRowId;
        result.Success = true;

        if (tursoResult.Rows.Count > 0 && typeof(T) != typeof(int) && typeof(T) != typeof(long) && typeof(T) != typeof(string))
        {
            var columnNames = tursoResult.Columns.Select(c => c.Name).ToList();
            var properties = typeof(T).GetProperties();

            foreach (var row in tursoResult.Rows)
            {
                var entity = Activator.CreateInstance<T>();

                // Dictionary-shaped results map by column name (used by the
                // custom field pivot); entity mapping relies on properties
                // and cannot fill an object/dynamic row
                if (entity is IDictionary<string, object?> rowDictionary)
                {
                    for (int i = 0; i < columnNames.Count && i < row.Count; i++)
                    {
                        rowDictionary[columnNames[i]] = row[i].Value;
                    }

                    result.Rows.Add(entity);
                    continue;
                }

                for (int i = 0; i < columnNames.Count && i < row.Count; i++)
                {
                    var prop = properties.FirstOrDefault(p => 
                        string.Equals(p.Name, columnNames[i], StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(p.Name, ToPascalCase(columnNames[i]), StringComparison.OrdinalIgnoreCase));

                    if (prop != null && prop.CanWrite)
                    {
                        var value = ConvertValue(row[i].Value, prop.PropertyType);
                        prop.SetValue(entity, value);
                    }
                }
                result.Rows.Add(entity);
            }
        }
        else if (tursoResult.Rows.Count > 0)
        {
            Debug.WriteLine($"[TursoClient] MapResult: Mapping {tursoResult.Rows.Count} rows to type {typeof(T).Name}");
            foreach (var row in tursoResult.Rows)
            {
                if (row.Count > 0)
                {
                    // Log the type only - row values contain personal data and must not reach the log file
                    Debug.WriteLine($"[TursoClient] MapResult: Row value type={row[0].Value?.GetType().Name ?? "null"}");
                    var value = ConvertValue(row[0].Value, typeof(T));
                    if (value != null)
                    {
                        result.Rows.Add((T)value);
                    }
                }
            }
        }

        return result;
    }

    private static object? ConvertValue(object? value, Type targetType)
    {
        if (value == null)
            return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;

        var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (value is System.Text.Json.JsonElement jsonElement)
        {
            return ConvertJsonElement(jsonElement, underlyingType);
        }

        if (underlyingType == typeof(string))
            return value?.ToString();

        if (underlyingType == typeof(int))
            return Convert.ToInt32(value);

        if (underlyingType == typeof(long))
            return Convert.ToInt64(value);

        if (underlyingType == typeof(double))
            return Convert.ToDouble(value);

        if (underlyingType == typeof(decimal))
            return Convert.ToDecimal(value);

        if (underlyingType == typeof(bool))
            return Convert.ToBoolean(value);

        if (underlyingType == typeof(DateTime))
        {
            if (value is string s && DateTime.TryParse(s, out var dt))
                return dt;
            return Convert.ToDateTime(value);
        }

        if (underlyingType == typeof(DateTimeOffset))
        {
            if (value is string str && DateTimeOffset.TryParse(str, out var dto))
                return dto;
            return Convert.ChangeType(value, underlyingType);
        }

        if (underlyingType == typeof(Guid) && value is string guidStr)
            return Guid.Parse(guidStr);

        if (underlyingType == typeof(byte[]))
        {
            if (value is string base64)
                return Convert.FromBase64String(base64);
            return value as byte[];
        }

        return Convert.ChangeType(value, underlyingType);
    }

    private static object? ConvertJsonElement(System.Text.Json.JsonElement element, Type targetType)
    {
        if (element.ValueKind == System.Text.Json.JsonValueKind.Null ||
            element.ValueKind == System.Text.Json.JsonValueKind.Undefined)
        {
            return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
        }

        if (targetType == typeof(string))
        {
            return element.GetString() ?? element.ToString();
        }

        if (targetType == typeof(int) || targetType == typeof(int?))
        {
            if (element.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                var str = element.GetString();
                return int.TryParse(str, out var intVal) ? intVal : Convert.ToInt32(str);
            }
            return element.GetInt32();
        }

        if (targetType == typeof(long) || targetType == typeof(long?))
        {
            if (element.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                var str = element.GetString();
                return long.TryParse(str, out var longVal) ? longVal : Convert.ToInt64(str);
            }
            return element.GetInt64();
        }

        if (targetType == typeof(double) || targetType == typeof(double?))
        {
            if (element.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                var str = element.GetString();
                return double.TryParse(str, out var doubleVal) ? doubleVal : Convert.ToDouble(str);
            }
            return element.GetDouble();
        }

        if (targetType == typeof(decimal) || targetType == typeof(decimal?))
        {
            if (element.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                var str = element.GetString();
                return decimal.TryParse(str, out var decVal) ? decVal : Convert.ToDecimal(str);
            }
            return element.GetDecimal();
        }

        if (targetType == typeof(bool) || targetType == typeof(bool?))
        {
            if (element.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                var str = element.GetString();
                // Handle "0"/"1" strings from SQLite/Turso
                if (str == "0") return false;
                if (str == "1") return true;
                // Handle "true"/"false" strings
                if (bool.TryParse(str, out var boolVal)) return boolVal;
                // Fallback to Convert
                return Convert.ToBoolean(str);
            }
            return element.GetBoolean();
        }

        if (targetType == typeof(DateTime) || targetType == typeof(DateTime?))
        {
            var str = element.GetString();
            if (str != null && DateTime.TryParse(str, out var dt))
                return dt;
            if (element.ValueKind == System.Text.Json.JsonValueKind.String)
                return DateTime.MinValue;
            return element.GetDateTime();
        }

        if (targetType == typeof(DateTimeOffset) || targetType == typeof(DateTimeOffset?))
        {
            var str = element.GetString();
            if (str != null && DateTimeOffset.TryParse(str, out var dto))
                return dto;
            if (element.ValueKind == System.Text.Json.JsonValueKind.String)
                return DateTimeOffset.MinValue;
            return element.GetDateTimeOffset();
        }

        if (targetType == typeof(Guid) || targetType == typeof(Guid?))
        {
            if (element.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                return Guid.Parse(element.GetString()!);
            }
            return element.GetGuid();
        }

        if (targetType == typeof(byte[]))
        {
            var base64 = element.GetString();
            return base64 != null ? Convert.FromBase64String(base64) : null;
        }

        return element.ToString();
    }

    private static string ToPascalCase(string snakeCase)
    {
        if (string.IsNullOrEmpty(snakeCase))
            return snakeCase;

        var parts = snakeCase.Split('_');
        return string.Concat(parts.Select(p => char.ToUpper(p[0]) + p[1..].ToLower()));
    }

    private static string NormalizeDatabaseUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return url;

        url = url.Trim();

        if (url.StartsWith("libsql://", StringComparison.OrdinalIgnoreCase))
            return "https://" + url[9..];

        if (url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return url;

        if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            return "https://" + url[7..];

        return "https://" + url;
    }

    private static string ConvertLibSqlToHttps(string url)
    {
        if (string.IsNullOrEmpty(url))
            return url;

        if (url.StartsWith("libsql://", StringComparison.OrdinalIgnoreCase))
            return "https://" + url[9..];

        return url;
    }

    private void SetConnectionStatus(bool connected)
    {
        if (_isConnected != connected)
        {
            _isConnected = connected;
            ConnectionStatusChanged?.Invoke(this, connected);
            Debug.WriteLine($"[TursoClient] Connection status changed to: {connected}");
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _httpClient.Dispose();
        _lock.Dispose();
        _disposed = true;

        Debug.WriteLine("[TursoClient] Disposed");
        GC.SuppressFinalize(this);
    }
}
