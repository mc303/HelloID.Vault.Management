using System.Data;
using System.Net.Sockets;
using Dapper;
using Npgsql;

namespace HelloID.Vault.Data.Connection;

/// <summary>
/// Factory for creating and configuring PostgreSQL database connections.
/// </summary>
public class PostgreSqlConnectionFactory : IDatabaseConnectionFactory
{
    private readonly string _connectionString;

    /// <summary>
    /// Initializes a new instance of the <see cref="PostgreSqlConnectionFactory"/> class.
    /// </summary>
    /// <param name="connectionString">The PostgreSQL connection string.</param>
    public PostgreSqlConnectionFactory(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("Connection string cannot be null or whitespace.", nameof(connectionString));
        }

        _connectionString = connectionString;
    }

    /// <inheritdoc />
    public DatabaseType DatabaseType => DatabaseType.PostgreSql;

    /// <inheritdoc />
    public IDbConnection CreateConnection()
    {
        return CreateConnection(enforceForeignKeys: true);
    }

    /// <summary>
    /// Creates a connection with optional foreign key enforcement control.
    /// </summary>
    /// <param name="enforceForeignKeys">Whether to enforce foreign key constraints (default: true).
    /// Note: PostgreSQL always enforces foreign keys by default. This parameter is kept for
    /// interface compatibility but has no effect on PostgreSQL connections.</param>
    /// <returns>An open database connection.</returns>
    public IDbConnection CreateConnection(bool enforceForeignKeys = true)
    {
        var connectionString = ResolveToIPv4(_connectionString);
        var connection = new NpgsqlConnection(connectionString);

        try
        {
            connection.Open();
        }
        catch (SocketException ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PostgreSqlConnectionFactory] SocketException: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[PostgreSqlConnectionFactory] Connection string (masked): {MaskConnectionString(connectionString)}");
            throw;
        }
        catch (Npgsql.NpgsqlException ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PostgreSqlConnectionFactory] NpgsqlException: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[PostgreSqlConnectionFactory] SQL State: {ex.SqlState}");
            System.Diagnostics.Debug.WriteLine($"[PostgreSqlConnectionFactory] Data: {string.Join(", ", ex.Data.Keys.Cast<string>().Select(k => $"{k}={ex.Data[k]}"))}");
            throw;
        }

        // PostgreSQL enforces foreign keys by default - no configuration needed
        // The enforceForeignKeys parameter exists for interface compatibility

        return connection;
    }

    /// <summary>
    /// Resolves the hostname in the connection string to an IPv4 address to avoid IPv6 timeout issues.
    /// </summary>
    /// <param name="connectionString">The original connection string.</param>
    /// <returns>Connection string with hostname resolved to IPv4 if possible.</returns>
    private static string ResolveToIPv4(string connectionString)
    {
        try
        {
            var builder = new NpgsqlConnectionStringBuilder(connectionString);

            if (string.IsNullOrEmpty(builder.Host))
            {
                return connectionString;
            }

            // If already an IP address, return as-is
            if (System.Net.IPAddress.TryParse(builder.Host, out var ipAddress))
            {
                return connectionString;
            }

            // Resolve to IPv4 addresses only
            var ipAddressList = System.Net.Dns.GetHostAddresses(builder.Host);
            var ipv4Address = ipAddressList.FirstOrDefault(ip =>
                ip.AddressFamily == AddressFamily.InterNetwork);

            if (ipv4Address != null)
            {
                builder.Host = ipv4Address.ToString();
                System.Diagnostics.Debug.WriteLine($"[PostgreSqlConnectionFactory] Resolved {builder.Host} -> {ipv4Address}");
                return builder.ToString();
            }

            return connectionString;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PostgreSqlConnectionFactory] DNS resolution failed: {ex.Message}");
            return connectionString;
        }
    }

    /// <summary>
    /// Masks the password in a connection string for secure logging.
    /// </summary>
    /// <param name="connectionString">The connection string to mask.</param>
    /// <returns>Connection string with password masked.</returns>
    private static string MaskConnectionString(string connectionString)
    {
        try
        {
            var builder = new NpgsqlConnectionStringBuilder(connectionString);
            if (!string.IsNullOrEmpty(builder.Password))
            {
                builder.Password = "***";
            }
            return builder.ToString();
        }
        catch
        {
            return connectionString;
        }
    }
}
