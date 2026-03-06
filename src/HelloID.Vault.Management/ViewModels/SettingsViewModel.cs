using System.ComponentModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HelloID.Vault.Data.Connection;
using HelloID.Vault.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using Npgsql;
using System.Diagnostics;
using HelloID.Vault.Services;

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

    // Supabase/PostgreSQL settings
    [ObservableProperty]
    private string _supabaseConnectionString = string.Empty;

    [ObservableProperty]
    private string _supabaseUrl = string.Empty;

    // Turso settings
    [ObservableProperty]
    private string _tursoDatabaseUrl = string.Empty;

    private string _tursoAuthToken = string.Empty;

    public string TursoAuthToken
    {
        get => _tursoAuthToken;
        set
        {
            if (SetProperty(ref _tursoAuthToken, value))
            {
                HasUnsavedChanges = true;
            }
        }
    }

    [ObservableProperty]
    private string _tursoOrganizationSlug = string.Empty;

    // Turso Platform API settings (for database creation)
    private string _tursoPlatformApiToken = string.Empty;

    public string TursoPlatformApiToken
    {
        get => _tursoPlatformApiToken;
        set
        {
            if (SetProperty(ref _tursoPlatformApiToken, value))
            {
                HasUnsavedChanges = true;
                OnPropertyChanged(nameof(HasPlatformToken));
            }
        }
    }

    [ObservableProperty]
    private string _tursoDatabaseName = string.Empty;

    // Visibility states
    [ObservableProperty]
    private bool _isSupabaseSelected;

    [ObservableProperty]
    private bool _isTursoSelected;

    [ObservableProperty]
    private bool _showDatabaseWarning;

    // UI state
    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _hasUnsavedChanges;

    private DatabaseType _originalDatabaseType;

    [ObservableProperty]
    private string _sqliteDatabasePath = string.Empty;

    [ObservableProperty]
    private string _preferencesFilePath = string.Empty;

    [ObservableProperty]
    private bool _isSqliteSelected;

    // Password visibility toggles
    [ObservableProperty]
    private bool _showSupabasePassword;

    [ObservableProperty]
    private bool _showTursoAuthToken;

    [ObservableProperty]
    private bool _showTursoPlatformToken;

    public SettingsViewModel(IUserPreferencesService preferencesService, IDialogService dialogService)
    {
        _preferencesService = preferencesService ?? throw new ArgumentNullException(nameof(preferencesService));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));

        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HelloID.Vault.Management");
        var defaultDbPath = Path.Combine(appDataPath, "db", "vault.db");

        var preferencesAppDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "HelloID.Vault.Management");
        _preferencesFilePath = Path.Combine(preferencesAppDataPath, "user-preferences.json");

        // Load current settings
        _selectedDatabaseType = _preferencesService.DatabaseType;
        _originalDatabaseType = _selectedDatabaseType;
        _supabaseConnectionString = _preferencesService.SupabaseConnectionString ?? string.Empty;
        _supabaseUrl = _preferencesService.SupabaseUrl ?? string.Empty;
        _tursoDatabaseUrl = _preferencesService.TursoDatabaseUrl ?? string.Empty;
        _tursoAuthToken = _preferencesService.TursoAuthToken ?? string.Empty;
        _tursoOrganizationSlug = _preferencesService.TursoOrganizationSlug ?? string.Empty;
        _tursoPlatformApiToken = _preferencesService.TursoPlatformApiToken ?? string.Empty;
        _tursoDatabaseName = _preferencesService.TursoDatabaseName ?? string.Empty;

        var customDbPath = _preferencesService.DatabasePath;
        _sqliteDatabasePath = !string.IsNullOrWhiteSpace(customDbPath)
            ? Path.IsPathRooted(customDbPath)
                ? customDbPath
                : Path.Combine(appDataPath, customDbPath)
            : defaultDbPath;

        UpdateVisibility();
    }

    /// <summary>
    /// Updates the visibility of database-related controls based on selected database type.
    /// </summary>
    partial void OnSelectedDatabaseTypeChanged(DatabaseType value)
    {
        UpdateVisibility();
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

    /// <summary>
    /// Tracks unsaved changes when Turso database URL changes.
    /// </summary>
    partial void OnTursoDatabaseUrlChanged(string value)
    {
        HasUnsavedChanges = true;
    }

    /// <summary>
    /// Tracks unsaved changes when Turso organization slug changes.
    /// </summary>
    partial void OnTursoOrganizationSlugChanged(string value)
    {
        HasUnsavedChanges = true;
    }

    /// <summary>
    /// Updates HasPlatformToken when database name changes.
    /// </summary>
    partial void OnTursoDatabaseNameChanged(string value)
    {
        OnPropertyChanged(nameof(HasPlatformToken));
    }

    /// <summary>
    /// Tracks unsaved changes when SQLite database path changes.
    /// </summary>
    partial void OnSqliteDatabasePathChanged(string value)
    {
        HasUnsavedChanges = true;
    }

    private void UpdateVisibility()
    {
        IsSupabaseSelected = SelectedDatabaseType == DatabaseType.PostgreSql;
        IsTursoSelected = SelectedDatabaseType == DatabaseType.Turso;
        IsSqliteSelected = SelectedDatabaseType == DatabaseType.Sqlite;
        ShowDatabaseWarning = IsSupabaseSelected || IsTursoSelected;
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
                Debug.WriteLine("[SettingsViewModel] Connection string was normalized from URI to key-value format");
            }

            // Log connection attempt (without exposing password)
            var maskedConnectionString = MaskConnectionString(normalizedConnectionString);
            Debug.WriteLine($"[SettingsViewModel] Testing connection with: {maskedConnectionString}");

            // Parse and validate connection string, ensure SSL settings are correct
            var connectionStringBuilder = new NpgsqlConnectionStringBuilder(normalizedConnectionString);

            // Log connection details
            Debug.WriteLine($"[SettingsViewModel] Connection details:");
            Debug.WriteLine($"  - Original Host: {connectionStringBuilder.Host}");
            Debug.WriteLine($"  - Port: {connectionStringBuilder.Port}");
            Debug.WriteLine($"  - Database: {connectionStringBuilder.Database}");
            Debug.WriteLine($"  - Username: {connectionStringBuilder.Username}");
            Debug.WriteLine($"  - Timeout: {connectionStringBuilder.Timeout} seconds");
            Debug.WriteLine($"  - SSL Mode: {connectionStringBuilder.SslMode}");

            // Resolve hostname to IPv4 to avoid IPv6 timeout issues
            if (!string.IsNullOrEmpty(connectionStringBuilder.Host))
            {
                string resolvedHost = await ResolveToIPv4Async(connectionStringBuilder.Host);
                if (resolvedHost != connectionStringBuilder.Host)
                {
                    Debug.WriteLine($"[SettingsViewModel] Resolved {connectionStringBuilder.Host} -> {resolvedHost} (IPv4)");
                    connectionStringBuilder.Host = resolvedHost;
                }
            }

            // Ensure SSL is enabled for Supabase
            if (!connectionStringBuilder.ContainsKey("SSL Mode") &&
                !connectionStringBuilder.ContainsKey("SSL Mode") &&
                !connectionStringBuilder.ContainsKey("SslMode"))
            {
                connectionStringBuilder.SslMode = SslMode.Require;
                Debug.WriteLine("[SettingsViewModel] Added SSL Mode=Require to connection string");
            }

            // Set reasonable timeout (15 seconds)
            if (connectionStringBuilder.Timeout == 0)
            {
                connectionStringBuilder.Timeout = 15;
                Debug.WriteLine("[SettingsViewModel] Set connection timeout to 15 seconds");
            }

            Debug.WriteLine($"[SettingsViewModel] Final connection string: {MaskConnectionString(connectionStringBuilder.ToString())}");

            // Use direct Npgsql connection with proper async pattern
            using var connection = new NpgsqlConnection(connectionStringBuilder.ToString());

            // Try to open the connection asynchronously
            Debug.WriteLine("[SettingsViewModel] Attempting to open connection...");
            await connection.OpenAsync();
            Debug.WriteLine($"[SettingsViewModel] Connection opened successfully.");
            Debug.WriteLine($"[SettingsViewModel] Server version: {connection.PostgreSqlVersion}");
            Debug.WriteLine($"[SettingsViewModel] Database: {connection.Database}");
            Debug.WriteLine($"[SettingsViewModel] DataSource: {connection.DataSource}");

            // Successfully connected
            await connection.CloseAsync();

            StatusMessage = "✓ Connection successful! Supabase is reachable.";
        }
        catch (NpgsqlException npgsqlEx)
        {
            // Handle PostgreSQL-specific exceptions with detailed error info
            Debug.WriteLine($"[SettingsViewModel] === NpgsqlException Details ===");
            Debug.WriteLine($"[SettingsViewModel] Message: {npgsqlEx.Message}");
            Debug.WriteLine($"[SettingsViewModel] SQL State: {npgsqlEx.SqlState}");
            Debug.WriteLine($"[SettingsViewModel] Error Code: {npgsqlEx.ErrorCode}");
            Debug.WriteLine($"[SettingsViewModel] InnerException: {npgsqlEx.InnerException?.Message}");

            // Log inner exception details if available
            if (npgsqlEx.InnerException != null)
            {
                Debug.WriteLine($"[SettingsViewModel] InnerException Type: {npgsqlEx.InnerException.GetType().Name}");
                Debug.WriteLine($"[SettingsViewModel] InnerException StackTrace: {npgsqlEx.InnerException.StackTrace}");
            }

            Debug.WriteLine($"[SettingsViewModel] StackTrace: {npgsqlEx.StackTrace}");
            Debug.WriteLine($"[SettingsViewModel] =================================");

            StatusMessage = $"✗ PostgreSQL connection failed: {npgsqlEx.Message}";
        }
        catch (System.InvalidOperationException invalidOpEx)
        {
            // Handle invalid connection string format
            Debug.WriteLine($"[SettingsViewModel] === InvalidOperationException ===");
            Debug.WriteLine($"[SettingsViewModel] Message: {invalidOpEx.Message}");
            Debug.WriteLine($"[SettingsViewModel] InnerException: {invalidOpEx.InnerException?.Message}");
            Debug.WriteLine($"[SettingsViewModel] StackTrace: {invalidOpEx.StackTrace}");
            Debug.WriteLine($"[SettingsViewModel] =================================");
            StatusMessage = $"✗ Invalid connection string format: {invalidOpEx.Message}";
        }
        catch (System.TimeoutException timeoutEx)
        {
            // Handle timeout specifically
            Debug.WriteLine($"[SettingsViewModel] === TimeoutException ===");
            Debug.WriteLine($"[SettingsViewModel] Message: {timeoutEx.Message}");
            Debug.WriteLine($"[SettingsViewModel] The connection attempt timed out. This could be due to:");
            Debug.WriteLine($"[SettingsViewModel] - Network connectivity issues");
            Debug.WriteLine($"[SettingsViewModel] - Firewall blocking the connection");
            Debug.WriteLine($"[SettingsViewModel] - Incorrect host or port");
            Debug.WriteLine($"[SettingsViewModel] - Supabase service unavailable");
            Debug.WriteLine($"[SettingsViewModel] =================================");
            StatusMessage = $"✗ Connection timeout: Server did not respond within timeout period.";
        }
        catch (System.Net.Sockets.SocketException sockEx)
        {
            // Handle socket errors (network issues)
            Debug.WriteLine($"[SettingsViewModel] === SocketException ===");
            Debug.WriteLine($"[SettingsViewModel] Message: {sockEx.Message}");
            Debug.WriteLine($"[SettingsViewModel] Socket ErrorCode: {sockEx.SocketErrorCode}");
            Debug.WriteLine($"[SettingsViewModel] This usually indicates a network connectivity issue.");
            Debug.WriteLine($"[SettingsViewModel] =================================");
            StatusMessage = $"✗ Network error: {sockEx.Message}";
        }
        catch (Exception ex)
        {
            // Handle any other exceptions
            Debug.WriteLine($"[SettingsViewModel] === Exception Details ===");
            Debug.WriteLine($"[SettingsViewModel] Type: {ex.GetType().FullName}");
            Debug.WriteLine($"[SettingsViewModel] Message: {ex.Message}");
            Debug.WriteLine($"[SettingsViewModel] InnerException: {ex.InnerException?.Message}");
            Debug.WriteLine($"[SettingsViewModel] StackTrace: {ex.StackTrace}");
            Debug.WriteLine($"[SettingsViewModel] =================================");
            StatusMessage = $"✗ Connection failed: {ex.GetType().Name} - {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Tests the Turso connection with the provided credentials.
    /// </summary>
    [RelayCommand]
    private async Task TestTursoConnectionAsync()
    {
        if (SelectedDatabaseType != DatabaseType.Turso)
        {
            StatusMessage = "Connection testing is only applicable for Turso.";
            return;
        }

        if (string.IsNullOrWhiteSpace(TursoDatabaseUrl))
        {
            StatusMessage = "Please enter a database URL first.";
            return;
        }

        if (string.IsNullOrWhiteSpace(TursoAuthToken))
        {
            StatusMessage = "Please enter an auth token first.";
            return;
        }

        IsLoading = true;
        StatusMessage = "Testing Turso connection...";

        try
        {
            Debug.WriteLine($"[SettingsViewModel] Testing Turso connection to: {TursoDatabaseUrl}");

            // Create a temporary client to test the connection
            using var client = new TursoClient(TursoDatabaseUrl, TursoAuthToken);
            var success = await client.TestConnectionAsync();

            if (success)
            {
                StatusMessage = "✓ Connection successful! Turso database is reachable.";
                Debug.WriteLine("[SettingsViewModel] Turso connection test successful");
            }
            else
            {
                StatusMessage = "✗ Connection failed. Please check your credentials.";
                Debug.WriteLine("[SettingsViewModel] Turso connection test failed");
            }
        }
        catch (TursoAuthException authEx)
        {
            Debug.WriteLine($"[SettingsViewModel] Turso auth error: {authEx.Message}");
            StatusMessage = $"✗ Authentication failed: {(authEx.IsTokenExpired ? "Token has expired" : "Invalid token")}";
        }
        catch (TursoNetworkException netEx)
        {
            Debug.WriteLine($"[SettingsViewModel] Turso network error: {netEx.Message}");
            StatusMessage = $"✗ Network error: {(netEx.IsOffline ? "No internet connection" : netEx.Message)}";
        }
        catch (TursoConnectionException connEx)
        {
            Debug.WriteLine($"[SettingsViewModel] Turso connection error: {connEx.Message}");
            StatusMessage = $"✗ Connection failed: {connEx.Message}";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SettingsViewModel] Turso test error: {ex.GetType().Name} - {ex.Message}");
            StatusMessage = $"✗ Connection test failed: {ex.Message}";
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

                Debug.WriteLine($"[SettingsViewModel] Converted URI to key-value format: {uri.Host}:{builder.Port}/{builder.Database}");
                return builder.ConnectionString;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SettingsViewModel] Failed to parse URI connection string: {ex.Message}");
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
                Debug.WriteLine($"[SettingsViewModel] {hostname} is already an IP address ({ipAddress.AddressFamily})");
                return hostname;
            }

            Debug.WriteLine($"[SettingsViewModel] Resolving {hostname} to IPv4...");

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
                Debug.WriteLine($"[SettingsViewModel] Found IPv4 address: {ipv4}");
                return ipv4;
            }
            else
            {
                Debug.WriteLine($"[SettingsViewModel] No IPv4 addresses found for {hostname}, using original hostname");
                return hostname;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SettingsViewModel] DNS resolution failed: {ex.Message}");
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

            // Validate Turso connection if Turso is selected
            if (SelectedDatabaseType == DatabaseType.Turso)
            {
                if (string.IsNullOrWhiteSpace(TursoDatabaseUrl))
                {
                    StatusMessage = "✗ Error: Database URL is required for Turso.";
                    IsLoading = false;
                    return;
                }

                if (string.IsNullOrWhiteSpace(TursoAuthToken))
                {
                    StatusMessage = "✗ Error: Auth token is required for Turso.";
                    IsLoading = false;
                    return;
                }

                // Test connection before saving
                var testResult = await TestTursoConnectionInternalAsync();
                if (!testResult)
                {
                    StatusMessage = "✗ Error: Cannot save. Connection test failed. Please verify your credentials.";
                    IsLoading = false;
                    return;
                }
            }

            // Check if database type changed BEFORE updating _originalDatabaseType
            bool databaseTypeChanged = SelectedDatabaseType != _originalDatabaseType;

            // Compute the database path to save (relative or absolute)
            string? dbPathToSave = null;
            if (!string.IsNullOrWhiteSpace(SqliteDatabasePath))
            {
                var appDataPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "HelloID.Vault.Management");
                var defaultDbPath = Path.Combine(appDataPath, "db", "vault.db");

                if (string.Equals(SqliteDatabasePath, defaultDbPath, StringComparison.OrdinalIgnoreCase))
                {
                    dbPathToSave = null;
                }
                else if (SqliteDatabasePath.StartsWith(appDataPath, StringComparison.OrdinalIgnoreCase))
                {
                    dbPathToSave = SqliteDatabasePath.Substring(appDataPath.Length).TrimStart(Path.DirectorySeparatorChar);
                }
                else
                {
                    dbPathToSave = SqliteDatabasePath;
                }
            }

            // Save to preferences
            _preferencesService.DatabaseType = SelectedDatabaseType;
            _preferencesService.DatabasePath = dbPathToSave;
            _preferencesService.SupabaseConnectionString = NormalizePostgreConnectionString(SupabaseConnectionString);
            _preferencesService.SupabaseUrl = SupabaseUrl;
            _preferencesService.TursoDatabaseUrl = TursoDatabaseUrl;
            _preferencesService.TursoAuthToken = TursoAuthToken;
            _preferencesService.TursoOrganizationSlug = TursoOrganizationSlug;
            _preferencesService.TursoPlatformApiToken = TursoPlatformApiToken;
            _preferencesService.TursoDatabaseName = TursoDatabaseName;
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
    /// Internal connection test for Supabase without UI updates.
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
    /// Internal connection test for Turso without UI updates.
    /// </summary>
    private async Task<bool> TestTursoConnectionInternalAsync()
    {
        try
        {
            using var client = new TursoClient(TursoDatabaseUrl, TursoAuthToken);
            return await client.TestConnectionAsync();
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
        TursoDatabaseUrl = _preferencesService.TursoDatabaseUrl ?? string.Empty;
        TursoAuthToken = _preferencesService.TursoAuthToken ?? string.Empty;
        TursoOrganizationSlug = _preferencesService.TursoOrganizationSlug ?? string.Empty;

        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HelloID.Vault.Management");
        var defaultDbPath = Path.Combine(appDataPath, "db", "vault.db");
        var customDbPath = _preferencesService.DatabasePath;
        SqliteDatabasePath = !string.IsNullOrWhiteSpace(customDbPath)
            ? Path.IsPathRooted(customDbPath)
                ? customDbPath
                : Path.Combine(appDataPath, customDbPath)
            : defaultDbPath;

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

    /// <summary>
    /// Opens a file dialog to browse for SQLite database file location.
    /// </summary>
    [RelayCommand]
    private void BrowseDatabasePath()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Select SQLite Database Location",
            Filter = "SQLite Database (*.db)|*.db|All Files (*.*)|*.*",
            DefaultExt = ".db",
            FileName = "vault.db",
            OverwritePrompt = false
        };

        if (!string.IsNullOrEmpty(SqliteDatabasePath))
        {
            var directory = Path.GetDirectoryName(SqliteDatabasePath);
            if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
            {
                dialog.InitialDirectory = directory;
            }
            dialog.FileName = Path.GetFileName(SqliteDatabasePath);
        }

        if (dialog.ShowDialog() == true)
        {
            SqliteDatabasePath = dialog.FileName;
        }
    }

    /// <summary>
    /// Opens the folder containing the SQLite database in Windows Explorer.
    /// </summary>
    [RelayCommand]
    private void OpenDatabaseFolder()
    {
        try
        {
            var directory = Path.GetDirectoryName(SqliteDatabasePath);
            if (!string.IsNullOrEmpty(directory))
            {
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                System.Diagnostics.Process.Start(
                    new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = directory,
                        UseShellExecute = true,
                        Verb = "open"
                    });
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not open folder: {ex.Message}";
        }
    }

    /// <summary>
    /// Opens the folder containing the preferences file in Windows Explorer.
    /// </summary>
    [RelayCommand]
    private void OpenPreferencesFolder()
    {
        try
        {
            var directory = Path.GetDirectoryName(PreferencesFilePath);
            if (!string.IsNullOrEmpty(directory))
            {
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                System.Diagnostics.Process.Start(
                    new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = directory,
                        UseShellExecute = true,
                        Verb = "open"
                    });
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not open folder: {ex.Message}";
        }
    }

    /// <summary>
    /// Toggles the visibility of the Supabase connection password field.
    /// </summary>
    [RelayCommand]
    private void ToggleSupabasePasswordVisibility()
    {
        ShowSupabasePassword = !ShowSupabasePassword;
    }

    /// <summary>
    /// Toggles the visibility of the Turso auth token field.
    /// </summary>
    [RelayCommand]
    private void ToggleTursoAuthTokenVisibility()
    {
        ShowTursoAuthToken = !ShowTursoAuthToken;
    }

    /// <summary>
    /// Toggles the visibility of the Turso platform API token field.
    /// </summary>
    [RelayCommand]
    private void ToggleTursoPlatformTokenVisibility()
    {
        ShowTursoPlatformToken = !ShowTursoPlatformToken;
    }

    /// <summary>
    /// Gets a value indicating whether a platform API token is configured.
    /// Used to enable/disable the "Create New Database" button.
    /// </summary>
    public bool HasPlatformToken => !string.IsNullOrWhiteSpace(TursoPlatformApiToken) &&
                                     !string.IsNullOrWhiteSpace(TursoDatabaseName);

    /// <summary>
    /// Creates a new Turso database using the Platform API.
    /// If the database already exists, prompts the user to reinitialize the schema.
    /// </summary>
    [RelayCommand]
    private async Task CreateTursoDatabaseAsync()
    {
        if (string.IsNullOrWhiteSpace(TursoPlatformApiToken))
        {
            StatusMessage = "Please enter a Platform API token first.";
            return;
        }

        if (string.IsNullOrWhiteSpace(TursoDatabaseName))
        {
            StatusMessage = "Please enter a database name first.";
            return;
        }

        IsLoading = true;
        StatusMessage = "Checking if database exists...";

        try
        {
            var platformService = new TursoPlatformService();
            platformService.SetAuthToken(TursoPlatformApiToken);

            var databaseName = TursoDatabaseName;
            var organization = TursoOrganizationSlug;

            // Check if database already exists
            var databaseExists = await platformService.DatabaseExistsAsync(databaseName, organization);

            if (databaseExists)
            {
                Debug.WriteLine($"[SettingsViewModel] Database '{databaseName}' already exists");

                // Prompt user to reinitialize schema
                var result = _dialogService.ShowConfirm(
                    $"Database '{databaseName}' already exists.\n\n" +
                    "Do you want to reinitialize the schema? This will replace all data in the database.",
                    "Database Exists");

                if (result != true)
                {
                    StatusMessage = "Operation cancelled. Database was not modified.";
                    IsLoading = false;
                    return;
                }

                // User confirmed - get URL and token for existing database
                StatusMessage = "Getting database credentials...";
                var existingUrl = await platformService.GetDatabaseUrlAsync(databaseName, organization);
                var existingToken = await platformService.CreateDatabaseTokenAsync(databaseName, organization);

                if (string.IsNullOrEmpty(existingUrl) || string.IsNullOrEmpty(existingToken))
                {
                    StatusMessage = "Failed to get database credentials.";
                    IsLoading = false;
                    return;
                }

                // Update the URL and token fields
                TursoDatabaseUrl = existingUrl;
                TursoAuthToken = existingToken;

                StatusMessage = "Database credentials updated. Schema will be initialized on next import.";
                HasUnsavedChanges = true;
            }
            else
            {
                // Database doesn't exist - create it
                StatusMessage = $"Creating database '{databaseName}'...";

                var dbResult = await platformService.CreateDatabaseAsync(databaseName, "default", organization);

                if (dbResult == null || string.IsNullOrEmpty(dbResult.Hostname))
                {
                    StatusMessage = "Failed to create database.";
                    IsLoading = false;
                    return;
                }

                Debug.WriteLine($"[SettingsViewModel] Database created: {dbResult.Url}");

                StatusMessage = "Creating database token...";

                // Create a token for the new database
                var token = await platformService.CreateDatabaseTokenAsync(databaseName, organization);

                if (string.IsNullOrEmpty(token))
                {
                    StatusMessage = "Database created, but failed to create token.";
                    IsLoading = false;
                    return;
                }

                // Wait for database to be ready (Turso needs a few seconds)
                StatusMessage = "Waiting for database to be ready...";
                await Task.Delay(3000);

                // Test connection with retries
                bool connectionReady = false;
                for (int attempt = 1; attempt <= 5; attempt++)
                {
                    Debug.WriteLine($"[SettingsViewModel] Testing connection, attempt {attempt}/5");
                    try
                    {
                        using var client = new TursoClient(dbResult.Url, token);
                        connectionReady = await client.TestConnectionAsync();
                        if (connectionReady)
                        {
                            Debug.WriteLine($"[SettingsViewModel] Connection successful on attempt {attempt}");
                            break;
                        }
                    }
                    catch (Exception testEx)
                    {
                        Debug.WriteLine($"[SettingsViewModel] Connection test {attempt} failed: {testEx.Message}");
                    }

                    if (attempt < 5)
                    {
                        StatusMessage = $"Waiting for database... (attempt {attempt}/5)";
                        await Task.Delay(2000);
                    }
                }

                if (!connectionReady)
                {
                    StatusMessage = "Database created but not yet ready. Please wait a moment and try 'Test Connection'.";
                    // Still set the values so user can retry
                    TursoDatabaseUrl = dbResult.Url;
                    TursoAuthToken = token;
                    HasUnsavedChanges = true;
                    return;
                }

                // Update the URL and token fields
                TursoDatabaseUrl = dbResult.Url;
                TursoAuthToken = token;

                StatusMessage = $"Database created and ready: {dbResult.Url}";
                HasUnsavedChanges = true;

                Debug.WriteLine($"[SettingsViewModel] Database creation complete");
            }
        }
        catch (TursoPlatformException platEx)
        {
            Debug.WriteLine($"[SettingsViewModel] Platform API error: {platEx.Message}");
            StatusMessage = $"Platform API error: {platEx.Message}";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SettingsViewModel] Create database error: {ex.Message}");
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Tests the connection to an existing Turso database.
    /// </summary>
    [RelayCommand]
    private async Task ConnectExistingTursoDatabaseAsync()
    {
        if (string.IsNullOrWhiteSpace(TursoDatabaseUrl))
        {
            StatusMessage = "Please enter a database URL first.";
            return;
        }

        if (string.IsNullOrWhiteSpace(TursoAuthToken))
        {
            StatusMessage = "Please enter an auth token first.";
            return;
        }

        IsLoading = true;
        StatusMessage = "Testing connection...";

        try
        {
            using var client = new TursoClient(TursoDatabaseUrl, TursoAuthToken);
            var success = await client.TestConnectionAsync();

            if (success)
            {
                // Check if schema is initialized
                var schemaInitialized = await client.IsSchemaInitializedAsync();

                if (!schemaInitialized)
                {
                    StatusMessage = "Connection successful. Database is empty - schema will be initialized on first import.";
                }
                else
                {
                    StatusMessage = "Connection successful.";
                }

                HasUnsavedChanges = true;
            }
            else
            {
                StatusMessage = "Connection failed. Please check your credentials.";
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SettingsViewModel] Connection test error: {ex.Message}");
            StatusMessage = $"Connection failed: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
