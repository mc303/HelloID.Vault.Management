using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace HelloID.Vault.Services;

/// <summary>
/// Service for interacting with the Turso Platform API.
/// Used for creating databases with upload support and managing tokens.
/// </summary>
public class TursoPlatformService
{
    private const string PlatformApiBaseUrl = "https://api.turso.tech/v1";
    private readonly HttpClient _httpClient;

    public TursoPlatformService()
    {
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    /// <summary>
    /// Sets the Platform API token for authentication.
    /// </summary>
    public void SetAuthToken(string token)
    {
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
    }

    /// <summary>
    /// Creates a new Turso database with upload support.
    /// </summary>
    /// <param name="databaseName">Name for the new database</param>
    /// <param name="group">Optional group name (defaults to "default")</param>
    /// <param name="organization">Optional organization slug</param>
    /// <returns>The created database details</returns>
    public async Task<TursoDatabaseResult?> CreateDatabaseAsync(
        string databaseName,
        string group = "default",
        string? organization = null,
        bool forUpload = false)
    {
        Debug.WriteLine($"[TursoPlatformService] CreateDatabaseAsync: {databaseName}, group={group}, org={organization}, forUpload={forUpload}");

        // Create database - include seed for upload workflow
        object request = forUpload
            ? new { name = databaseName, group = group, seed = new { type = "database_upload" } }
            : new { name = databaseName, group = group };

        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Use organization-specific endpoint if provided
        var url = string.IsNullOrEmpty(organization)
            ? $"{PlatformApiBaseUrl}/databases"
            : $"https://api.turso.tech/v1/organizations/{organization}/databases";

        Debug.WriteLine($"[TursoPlatformService] POST {url}");
        Debug.WriteLine($"[TursoPlatformService] Request: {json}");

        var response = await _httpClient.PostAsync(url, content);
        var responseBody = await response.Content.ReadAsStringAsync();

        Debug.WriteLine($"[TursoPlatformService] Response: {(int)response.StatusCode} - {responseBody}");

        if (!response.IsSuccessStatusCode)
        {
            var error = TryParseError(responseBody);
            throw new TursoPlatformException(
                $"Failed to create database: {error ?? responseBody}",
                (int)response.StatusCode);
        }

        var result = JsonSerializer.Deserialize<TursoDatabaseResponse>(responseBody,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (result?.Database == null)
        {
            throw new TursoPlatformException("Invalid response from Turso API", (int)response.StatusCode);
        }

        return new TursoDatabaseResult
        {
            Name = result.Database.Name,
            Hostname = result.Database.Hostname,
            Group = result.Database.Group,
            Url = $"libsql://{result.Database.Hostname}"
        };
    }

    /// <summary>
    /// Creates a new authentication token for a database.
    /// </summary>
    /// <param name="databaseName">The database name</param>
    /// <param name="organization">Optional organization slug</param>
    /// <returns>The created token</returns>
    public async Task<string> CreateDatabaseTokenAsync(
        string databaseName,
        string? organization = null)
    {
        Debug.WriteLine($"[TursoPlatformService] CreateDatabaseTokenAsync: {databaseName}, org={organization}");

        // Use organization-specific endpoint if provided
        var url = string.IsNullOrEmpty(organization)
            ? $"{PlatformApiBaseUrl}/databases/{databaseName}/auth/tokens"
            : $"https://api.turso.tech/v1/organizations/{organization}/databases/{databaseName}/auth/tokens";

        Debug.WriteLine($"[TursoPlatformService] POST {url}");

        var response = await _httpClient.PostAsync(url, null);
        var responseBody = await response.Content.ReadAsStringAsync();

        Debug.WriteLine($"[TursoPlatformService] Response: {(int)response.StatusCode} - {responseBody}");

        if (!response.IsSuccessStatusCode)
        {
            var error = TryParseError(responseBody);
            throw new TursoPlatformException(
                $"Failed to create token: {error ?? responseBody}",
                (int)response.StatusCode);
        }

        var result = JsonSerializer.Deserialize<TursoTokenResponse>(responseBody,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (result?.Jwt == null)
        {
            throw new TursoPlatformException("Invalid token response from Turso API", (int)response.StatusCode);
        }

        return result.Jwt;
    }

    /// <summary>
    /// Lists databases to verify a database exists.
    /// </summary>
    /// <param name="organization">Optional organization slug</param>
    /// <returns>List of databases</returns>
    public async Task<List<TursoDatabaseResult>> ListDatabasesAsync(string? organization = null)
    {
        var url = string.IsNullOrEmpty(organization)
            ? $"{PlatformApiBaseUrl}/databases"
            : $"https://api.turso.tech/v1/organizations/{organization}/databases";

        var response = await _httpClient.GetAsync(url);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            var error = TryParseError(responseBody);
            throw new TursoPlatformException(
                $"Failed to list databases: {error ?? responseBody}",
                (int)response.StatusCode);
        }

        var result = JsonSerializer.Deserialize<TursoDatabaseListResponse>(responseBody,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        return result?.Databases?.Select(d => new TursoDatabaseResult
        {
            Name = d.Name,
            Hostname = d.Hostname,
            Group = d.Group,
            Url = $"libsql://{d.Hostname}"
        }).ToList() ?? new List<TursoDatabaseResult>();
    }

    /// <summary>
    /// Checks if a database with the given name exists.
    /// </summary>
    /// <param name="databaseName">The database name to check</param>
    /// <param name="organization">Optional organization slug</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if database exists</returns>
    public async Task<bool> DatabaseExistsAsync(
        string databaseName,
        string? organization = null,
        CancellationToken cancellationToken = default)
    {
        Debug.WriteLine($"[TursoPlatformService] Checking if database '{databaseName}' exists");

        var databases = await ListDatabasesAsync(organization);
        return databases.Any(d => d.Name.Equals(databaseName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Gets the database URL for the specified database.
    /// </summary>
    /// <param name="databaseName">The database name</param>
    /// <param name="organization">Optional organization slug</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The database URL (libsql://...) or null if not found</returns>
    public async Task<string?> GetDatabaseUrlAsync(
        string databaseName,
        string? organization = null,
        CancellationToken cancellationToken = default)
    {
        Debug.WriteLine($"[TursoPlatformService] Getting URL for database '{databaseName}'");

        var databases = await ListDatabasesAsync(organization);
        var db = databases.FirstOrDefault(d => d.Name.Equals(databaseName, StringComparison.OrdinalIgnoreCase));

        if (db != null && !string.IsNullOrEmpty(db.Hostname))
        {
            var url = $"libsql://{db.Hostname}";
            Debug.WriteLine($"[TursoPlatformService] Database URL: {url}");
            return url;
        }

        Debug.WriteLine($"[TursoPlatformService] Database '{databaseName}' not found");
        return null;
    }

    /// <summary>
    /// Deletes a database.
    /// WARNING: This operation is irreversible and will delete all data.
    /// </summary>
    /// <param name="databaseName">The database name to delete</param>
    /// <param name="organization">Optional organization slug</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if database was deleted successfully</returns>
    public async Task<bool> DeleteDatabaseAsync(
        string databaseName,
        string? organization = null,
        CancellationToken cancellationToken = default)
    {
        Debug.WriteLine($"[TursoPlatformService] Deleting database '{databaseName}'");

        // Use organization-specific endpoint if provided
        var url = string.IsNullOrEmpty(organization)
            ? $"{PlatformApiBaseUrl}/databases/{databaseName}"
            : $"https://api.turso.tech/v1/organizations/{organization}/databases/{databaseName}";

        Debug.WriteLine($"[TursoPlatformService] DELETE {url}");

        var response = await _httpClient.DeleteAsync(url, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        Debug.WriteLine($"[TursoPlatformService] Response: {(int)response.StatusCode} - {responseBody}");

        if (!response.IsSuccessStatusCode)
        {
            var error = TryParseError(responseBody);
            throw new TursoPlatformException(
                $"Failed to delete database: {error ?? responseBody}",
                (int)response.StatusCode);
        }

        Debug.WriteLine($"[TursoPlatformService] Database '{databaseName}' deleted successfully");
        return true;
    }

    private static string? TryParseError(string responseBody)
    {
        try
        {
            var error = JsonSerializer.Deserialize<TursoErrorResponse>(responseBody,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return error?.Error ?? error?.Message;
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>
/// Result of a database creation or listing operation.
/// </summary>
public class TursoDatabaseResult
{
    public string Name { get; set; } = string.Empty;
    public string Hostname { get; set; } = string.Empty;
    public string Group { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}

/// <summary>
/// Exception thrown when Turso Platform API operations fail.
/// </summary>
public class TursoPlatformException : Exception
{
    public int? StatusCode { get; }

    public TursoPlatformException(string message, int? statusCode = null) : base(message)
    {
        StatusCode = statusCode;
    }

    public TursoPlatformException(string message, Exception innerException, int? statusCode = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
    }
}

// Response models for JSON deserialization
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor.

internal class TursoDatabaseResponse
{
    public TursoDatabaseInfo Database { get; set; }
}

internal class TursoDatabaseListResponse
{
    public List<TursoDatabaseInfo> Databases { get; set; }
}

internal class TursoDatabaseInfo
{
    public string Name { get; set; }
    public string Hostname { get; set; }
    public string Group { get; set; }
}

internal class TursoTokenResponse
{
    public string Jwt { get; set; }
}

internal class TursoErrorResponse
{
    public string? Error { get; set; }
    public string? Message { get; set; }
}

#pragma warning restore CS8618
