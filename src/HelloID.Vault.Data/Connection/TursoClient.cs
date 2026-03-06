using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

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

        _databaseUrl = databaseUrl;
        _authToken = authToken;

        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            PropertyNameCaseInsensitive = true
        };

        UpdateHttpClientHeaders();
        Debug.WriteLine("[TursoClient] Initialized with database URL");
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

        _databaseUrl = databaseUrl;
        _authToken = authToken;
        UpdateHttpClientHeaders();
        Debug.WriteLine("[TursoClient] Credentials updated");
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
        var response = await ExecuteWithRetryAsync(statement, cancellationToken);

        var resultItem = response.Results.FirstOrDefault();
        return resultItem?.Response?.Result?.AffectedRowCount ?? 0;
    }

    public async Task<TursoTransactionResult> ExecuteTransactionAsync(IEnumerable<TursoStatement> statements, CancellationToken cancellationToken = default)
    {
        Debug.WriteLine($"[TursoClient] ExecuteTransactionAsync: {statements.Count()} statements");

        var request = new TursoPipelineRequest
        {
            Requests = statements.Select(s => new TursoRequestItem
            {
                Type = "execute",
                Statement = s
            }).ToList()
        };

        var totalAffectedRows = 0;
        var errors = new List<TursoError>();

        try
        {
            var response = await SendPipelineRequestAsync(request, cancellationToken);

            foreach (var resultItem in response.Results)
            {
                if (resultItem.Error != null)
                {
                    errors.Add(resultItem.Error);
                }
                else if (resultItem.Response?.Result != null)
                {
                    totalAffectedRows += resultItem.Response.Result.AffectedRowCount;
                }
            }

            return new TursoTransactionResult
            {
                Success = errors.Count == 0,
                TotalAffectedRows = totalAffectedRows,
                Errors = errors
            };
        }
        catch (Exception ex)
        {
            return new TursoTransactionResult
            {
                Success = false,
                Errors = [new TursoError { Message = ex.Message, Type = ex.GetType().Name }]
            };
        }
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
            return false;
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

        // Split by semicolons, but handle them carefully
        var statements = sqlScript
            .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrWhiteSpace(s) && !s.StartsWith("--"))
            .ToList();

        Debug.WriteLine($"[TursoClient] Found {statements.Count} statements to execute");

        var executed = 0;
        foreach (var statement in statements)
        {
            try
            {
                await ExecuteAsync(statement, null, cancellationToken);
                executed++;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TursoClient] Statement execution error: {ex.Message}");
                // Continue with next statement (some may be VIEWs that already exist, etc.)
            }
        }

        Debug.WriteLine($"[TursoClient] Executed {executed}/{statements.Count} statements");
        return executed;
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
                args.Add(ConvertToTursoValue(kvp.Value));
            }
        }
        else
        {
            var properties = parameters.GetType().GetProperties();

            foreach (var prop in properties)
            {
                var value = prop.GetValue(parameters);
                args.Add(ConvertToTursoValue(value));
            }
        }

        return args;
    }

    private static TursoValue ConvertToTursoValue(object? value)
    {
        if (value == null || value == DBNull.Value)
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
            _ => TursoValue.Text(value.ToString() ?? string.Empty)
        };
    }

    private async Task<TursoPipelineResponse> ExecuteWithRetryAsync(TursoStatement statement, CancellationToken cancellationToken)
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
                Debug.WriteLine($"[TursoClient] Network offline, attempt {attempt + 1}/{MaxRetries}");
                lastException = ex;
                if (attempt < MaxRetries - 1)
                {
                    await Task.Delay(BaseDelayMs * (attempt + 1), cancellationToken);
                }
            }
            catch (TursoConnectionException ex)
            {
                Debug.WriteLine($"[TursoClient] Connection error, attempt {attempt + 1}/{MaxRetries}: {ex.Message}");
                lastException = ex;
                if (attempt < MaxRetries - 1)
                {
                    await Task.Delay(BaseDelayMs * (attempt + 1), cancellationToken);
                }
            }
            catch (HttpRequestException ex)
            {
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
            throw new TursoConnectionException($"Request failed with status {response.StatusCode}", (int)response.StatusCode);
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
                    Debug.WriteLine($"[TursoClient] MapResult: Row value type={row[0].Value?.GetType().Name ?? "null"}, value={row[0].Value}");
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
