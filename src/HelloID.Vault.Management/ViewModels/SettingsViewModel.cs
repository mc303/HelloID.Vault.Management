using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HelloID.Vault.Data.Connection;
using HelloID.Vault.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using Npgsql;

namespace HelloID.Vault.Management.ViewModels;

/// <summary>
/// ViewModel for application settings, including database configuration.
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly IUserPreferencesService _preferencesService;
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    private DatabaseType _selectedDatabaseType;

    [ObservableProperty]
    private string _supabaseConnectionString = string.Empty;

    [ObservableProperty]
    private string _supabaseUrl = string.Empty;

    [ObservableProperty]
    private bool _isSupabaseSelected;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _hasUnsavedChanges;

    private DatabaseType _originalDatabaseType;

    public SettingsViewModel(IUserPreferencesService preferencesService, IDialogService dialogService)
    {
        _preferencesService = preferencesService ?? throw new ArgumentNullException(nameof(preferencesService));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));

        // Load current settings
        _selectedDatabaseType = _preferencesService.DatabaseType;
        _originalDatabaseType = _selectedDatabaseType;
        _supabaseConnectionString = _preferencesService.SupabaseConnectionString ?? string.Empty;
        _supabaseUrl = _preferencesService.SupabaseUrl ?? string.Empty;

        UpdateSupabaseVisibility();
    }

    /// <summary>
    /// Updates the visibility of Supabase-related controls based on selected database type.
    /// </summary>
    partial void OnSelectedDatabaseTypeChanged(DatabaseType value)
    {
        UpdateSupabaseVisibility();
        HasUnsavedChanges = true;
    }

    /// <summary>
    /// Tracks unsaved changes when connection string changes.
    /// </summary>
    partial void OnSupabaseConnectionStringChanged(string value)
    {
        HasUnsavedChanges = true;
    }

    /// <summary>
    /// Tracks unsaved changes when Supabase URL changes.
    /// </summary>
    partial void OnSupabaseUrlChanged(string value)
    {
        HasUnsavedChanges = true;
    }

    private void UpdateSupabaseVisibility()
    {
        IsSupabaseSelected = SelectedDatabaseType == DatabaseType.PostgreSql;
    }

    /// <summary>
    /// Tests the Supabase connection with the provided credentials.
    /// </summary>
    [RelayCommand]
    private async Task TestConnectionAsync()
    {
        if (SelectedDatabaseType != DatabaseType.PostgreSql)
        {
            StatusMessage = "Connection testing is only applicable for Supabase (PostgreSQL).";
            return;
        }

        if (string.IsNullOrWhiteSpace(SupabaseConnectionString))
        {
            StatusMessage = "Please enter a connection string first.";
            return;
        }

        IsLoading = true;
        StatusMessage = "Testing connection...";

        try
        {
            // Normalize connection string (convert URI to key-value format if needed)
            var normalizedConnectionString = NormalizePostgreConnectionString(SupabaseConnectionString);
            if (normalizedConnectionString != SupabaseConnectionString)
            {
                System.Diagnostics.Debug.WriteLine("[SettingsViewModel] Connection string was normalized from URI to key-value format");
            }

            // Log connection attempt (without exposing password)
            var maskedConnectionString = MaskConnectionString(normalizedConnectionString);
            System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] Testing connection with: {maskedConnectionString}");

            // Parse and validate connection string, ensure SSL settings are correct
            var connectionStringBuilder = new NpgsqlConnectionStringBuilder(normalizedConnectionString);

            // Log connection details
            System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] Connection details:");
            System.Diagnostics.Debug.WriteLine($"  - Original Host: {connectionStringBuilder.Host}");
            System.Diagnostics.Debug.WriteLine($"  - Port: {connectionStringBuilder.Port}");
            System.Diagnostics.Debug.WriteLine($"  - Database: {connectionStringBuilder.Database}");
            System.Diagnostics.Debug.WriteLine($"  - Username: {connectionStringBuilder.Username}");
            System.Diagnostics.Debug.WriteLine($"  - Timeout: {connectionStringBuilder.Timeout} seconds");
            System.Diagnostics.Debug.WriteLine($"  - SSL Mode: {connectionStringBuilder.SslMode}");

            // Resolve hostname to IPv4 to avoid IPv6 timeout issues
            if (!string.IsNullOrEmpty(connectionStringBuilder.Host))
            {
                string resolvedHost = await ResolveToIPv4Async(connectionStringBuilder.Host);
                if (resolvedHost != connectionStringBuilder.Host)
                {
                    System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] Resolved {connectionStringBuilder.Host} -> {resolvedHost} (IPv4)");
                    connectionStringBuilder.Host = resolvedHost;
                }
            }

            // Ensure SSL is enabled for Supabase
            if (!connectionStringBuilder.ContainsKey("SSL Mode") &&
                !connectionStringBuilder.ContainsKey("SSL Mode") &&
                !connectionStringBuilder.ContainsKey("SslMode"))
            {
                connectionStringBuilder.SslMode = SslMode.Require;
                System.Diagnostics.Debug.WriteLine("[SettingsViewModel] Added SSL Mode=Require to connection string");
            }

            // Set reasonable timeout (15 seconds)
            if (connectionStringBuilder.Timeout == 0)
            {
                connectionStringBuilder.Timeout = 15;
                System.Diagnostics.Debug.WriteLine("[SettingsViewModel] Set connection timeout to 15 seconds");
            }

            System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] Final connection string: {MaskConnectionString(connectionStringBuilder.ToString())}");

            // Use direct Npgsql connection with proper async pattern
            using var connection = new NpgsqlConnection(connectionStringBuilder.ToString());

            // Try to open the connection asynchronously
            System.Diagnostics.Debug.WriteLine("[SettingsViewModel] Attempting to open connection...");
            await connection.OpenAsync();
            System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] Connection opened successfully.");
            System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] Server version: {connection.PostgreSqlVersion}");
            System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] Database: {connection.Database}");
            System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] DataSource: {connection.DataSource}");

            // Successfully connected
            await connection.CloseAsync();

            StatusMessage = "✓ Connection successful! Supabase is reachable.";
        }
        catch (NpgsqlException npgsqlEx)
        {
            // Handle PostgreSQL-specific exceptions with detailed error info
            System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] === NpgsqlException Details ===");
            System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] Message: {npgsqlEx.Message}");
            System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] SQL State: {npgsqlEx.SqlState}");
            System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] Error Code: {npgsqlEx.ErrorCode}");
            System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] InnerException: {npgsqlEx.InnerException?.Message}");

            // Log inner exception details if available
            if (npgsqlEx.InnerException != null)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] InnerException Type: {npgsqlEx.InnerException.GetType().Name}");
                System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] InnerException StackTrace: {npgsqlEx.InnerException.StackTrace}");
            }

            System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] StackTrace: {npgsqlEx.StackTrace}");
            System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] =================================");

            StatusMessage = $"✗ PostgreSQL connection failed: {npgsqlEx.Message}";
        }
        catch (System.InvalidOperationException invalidOpEx)
        {
            // Handle invalid connection string format
            System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] === InvalidOperationException ===");
            System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] Message: {invalidOpEx.Message}");
            System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] InnerException: {invalidOpEx.InnerException?.Message}");
            System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] StackTrace: {invalidOpEx.StackTrace}");
            System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] =================================");
            StatusMessage = $"✗ Invalid connection string format: {invalidOpEx.Message}";
        }
        catch (System.TimeoutException timeoutEx)
        {
            // Handle timeout specifically
            System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] === TimeoutException ===");
            System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] Message: {timeoutEx.Message}");
            System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] The connection attempt timed out. This could be due to:");
            System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] - Network connectivity issues");
            System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] - Firewall blocking the connection");
            System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] - Incorrect host or port");
            System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] - Supabase service unavailable");
            System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] =================================");
            StatusMessage = $"✗ Connection timeout: Server did not respond within timeout period.";
        }
        catch (System.Net.Sockets.SocketException sockEx)
        {
            // Handle socket errors (network issues)
            System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] === SocketException ===");
            System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] Message: {sockEx.Message}");
            System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] Socket ErrorCode: {sockEx.SocketErrorCode}");
            System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] This usually indicates a network connectivity issue.");
            System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] =================================");
            StatusMessage = $"✗ Network error: {sockEx.Message}";
        }
        catch (Exception ex)
        {
            // Handle any other exceptions
            System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] === Exception Details ===");
            System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] Type: {ex.GetType().FullName}");
            System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] Message: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] InnerException: {ex.InnerException?.Message}");
            System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] StackTrace: {ex.StackTrace}");
            System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] =================================");
            StatusMessage = $"✗ Connection failed: {ex.GetType().Name} - {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Converts PostgreSQL URI format to Npgsql key-value format.
    /// Input: postgresql://user:pass@host:port/database
    /// Output: Host=host;Port=port;Database=database;Username=user;Password=pass;SSL Mode=Require
    /// </summary>
    private string NormalizePostgreConnectionString(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return input;

        // Already in key-value format?
        if (input.Contains("Host=", StringComparison.OrdinalIgnoreCase) ||
            input.Contains("Server=", StringComparison.OrdinalIgnoreCase))
            return input;

        // URI format detected
        if (input.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase) ||
            input.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var uri = new Uri(input);
                var userInfo = uri.UserInfo.Split(':');

                var builder = new NpgsqlConnectionStringBuilder
                {
                    Host = uri.Host,
                    Port = uri.Port > 0 ? uri.Port : 5432,
                    Database = uri.AbsolutePath.TrimStart('/'),
                    Username = userInfo.Length > 0 ? Uri.UnescapeDataString(userInfo[0]) : "",
                    Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "",
                    SslMode = SslMode.Require
                };

                System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] Converted URI to key-value format: {uri.Host}:{builder.Port}/{builder.Database}");
                return builder.ConnectionString;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] Failed to parse URI connection string: {ex.Message}");
                return input; // Return original if parsing fails
            }
        }

        return input;
    }

    /// <summary>
    /// Masks the password in connection string for logging.
    /// </summary>
    private string MaskConnectionString(string connectionString)
    {
        if (string.IsNullOrEmpty(connectionString)) return string.Empty;

        // Try to parse as Npgsql connection string
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
            // If parsing fails, try regex masking
            var masked = System.Text.RegularExpressions.Regex.Replace(
                connectionString,
                @"Password=([^;]+)",
                "Password=***");
            return masked;
        }
    }

    /// <summary>
    /// Resolves a hostname to its IPv4 address to avoid IPv6 connection issues.
    /// Returns the original hostname if IPv4 resolution fails or if it's already an IP.
    /// </summary>
    private async Task<string> ResolveToIPv4Async(string hostname)
    {
        try
        {
            // If it's already an IP address (IPv4 or IPv6), return as-is
            if (System.Net.IPAddress.TryParse(hostname, out var ipAddress))
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] {hostname} is already an IP address ({ipAddress.AddressFamily})");
                return hostname;
            }

            System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] Resolving {hostname} to IPv4...");

            // Resolve hostname to IPv4 addresses only
            var ipv4Addresses = await System.Net.Dns.GetHostAddressesAsync(hostname)
                .ContinueWith(t =>
                {
                    if (t.IsFaulted) return Array.Empty<System.Net.IPAddress>();
                    return t.Result.Where(ip => ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork).ToArray();
                });

            if (ipv4Addresses.Length > 0)
            {
                string ipv4 = ipv4Addresses[0].ToString();
                System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] Found IPv4 address: {ipv4}");
                return ipv4;
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] No IPv4 addresses found for {hostname}, using original hostname");
                return hostname;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] DNS resolution failed: {ex.Message}");
            return hostname;
        }
    }

    /// <summary>
    /// Saves the settings and prompts for restart if database type changed.
    /// </summary>
    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        IsLoading = true;
        StatusMessage = "Saving settings...";

        try
        {
            // Validate Supabase connection if PostgreSQL is selected
            if (SelectedDatabaseType == DatabaseType.PostgreSql)
            {
                if (string.IsNullOrWhiteSpace(SupabaseConnectionString))
                {
                    StatusMessage = "✗ Error: Connection string is required for Supabase.";
                    IsLoading = false;
                    return;
                }

                // Test connection before saving
                var testResult = await TestConnectionInternalAsync();
                if (!testResult)
                {
                    StatusMessage = "✗ Error: Cannot save. Connection test failed. Please verify your connection string.";
                    IsLoading = false;
                    return;
                }
            }

            // Check if database type changed BEFORE updating _originalDatabaseType
            bool databaseTypeChanged = SelectedDatabaseType != _originalDatabaseType;

            // Save to preferences (normalize connection string first)
            _preferencesService.DatabaseType = SelectedDatabaseType;
            _preferencesService.SupabaseConnectionString = NormalizePostgreConnectionString(SupabaseConnectionString);
            _preferencesService.SupabaseUrl = SupabaseUrl;
            await _preferencesService.SaveAsync();

            _originalDatabaseType = SelectedDatabaseType;
            HasUnsavedChanges = false;
            StatusMessage = "✓ Settings saved successfully.";

            if (databaseTypeChanged)
            {
                // Prompt user to restart
                bool shouldRestart = MessageBox.Show(
                    "Database type has changed. The application must restart for changes to take effect.\n\n" +
                    "Note: Switching databases will not migrate your data. You will need to import data into the new database.\n\n" +
                    "Restart now?",
                    "Restart Required",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question) == MessageBoxResult.Yes;

                if (shouldRestart)
                {
                    RestartApplication();
                }
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"✗ Error saving settings: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Internal connection test without UI updates.
    /// </summary>
    private async Task<bool> TestConnectionInternalAsync()
    {
        try
        {
            // Normalize connection string (convert URI to key-value format if needed)
            var normalizedConnectionString = NormalizePostgreConnectionString(SupabaseConnectionString);

            // Parse connection string and ensure SSL settings
            var connectionStringBuilder = new NpgsqlConnectionStringBuilder(normalizedConnectionString);

            // Resolve hostname to IPv4 to avoid IPv6 timeout issues
            if (!string.IsNullOrEmpty(connectionStringBuilder.Host))
            {
                string resolvedHost = await ResolveToIPv4Async(connectionStringBuilder.Host);
                if (resolvedHost != connectionStringBuilder.Host)
                {
                    connectionStringBuilder.Host = resolvedHost;
                }
            }

            // Ensure SSL is enabled
            if (!connectionStringBuilder.ContainsKey("SSL Mode") &&
                !connectionStringBuilder.ContainsKey("SSL Mode") &&
                !connectionStringBuilder.ContainsKey("SslMode"))
            {
                connectionStringBuilder.SslMode = SslMode.Require;
            }

            // Use direct Npgsql connection with proper async pattern
            using var connection = new NpgsqlConnection(connectionStringBuilder.ToString());
            await connection.OpenAsync();
            await connection.CloseAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Resets settings to their last saved values.
    /// </summary>
    [RelayCommand]
    private void ResetSettings()
    {
        SelectedDatabaseType = _preferencesService.DatabaseType;
        SupabaseConnectionString = _preferencesService.SupabaseConnectionString ?? string.Empty;
        SupabaseUrl = _preferencesService.SupabaseUrl ?? string.Empty;
        HasUnsavedChanges = false;
        StatusMessage = "Settings reset to last saved values.";
    }

    /// <summary>
    /// Restarts the application.
    /// </summary>
    private void RestartApplication()
    {
        // Get the path to the current executable
        string executablePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName
            ?? System.Reflection.Assembly.GetExecutingAssembly().Location;

        // Start a new process
        System.Diagnostics.Process.Start(executablePath);

        // Shutdown the current application
        Application.Current.Shutdown();
    }

    /// <summary>
    /// Opens a browser to Supabase documentation for connection strings.
    /// </summary>
    [RelayCommand]
    private void OpenHelpDocumentation()
    {
        try
        {
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "https://supabase.com/docs/guides/database/connecting-to-postgres",
                    UseShellExecute = true
                });
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not open browser: {ex.Message}";
        }
    }
}
