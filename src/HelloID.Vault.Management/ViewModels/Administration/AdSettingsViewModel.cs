using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HelloID.Vault.Core.Models.Ad;
using HelloID.Vault.Services.Interfaces;
using HelloID.Vault.Services.Security;

namespace HelloID.Vault.Management.ViewModels.Administration;

/// <summary>
/// ViewModel for the AD Correlation Settings view: LDAP connection,
/// correlation attribute, entitlement filter, and match field configuration.
/// Settings are persisted in user preferences (not the database).
/// </summary>
public partial class AdSettingsViewModel : ObservableObject
{
    private readonly IActiveDirectoryService _activeDirectoryService;
    private readonly IUserPreferencesService _userPreferencesService;
    private readonly IEncryptionService? _encryptionService;
    private bool _isInitializing;

    public AdSettingsViewModel(
        IActiveDirectoryService activeDirectoryService,
        IUserPreferencesService userPreferencesService,
        IEncryptionService? encryptionService = null)
    {
        _activeDirectoryService = activeDirectoryService ?? throw new ArgumentNullException(nameof(activeDirectoryService));
        _userPreferencesService = userPreferencesService ?? throw new ArgumentNullException(nameof(userPreferencesService));
        _encryptionService = encryptionService;
    }

    public async Task InitializeAsync()
    {
        if (_isInitializing) return;
        _isInitializing = true;
        try
        {
            await LoadConfigAsync();
        }
        finally
        {
            _isInitializing = false;
        }
    }

    // ---- Connection configuration ----

    [ObservableProperty]
    private string _ldapHost = string.Empty;

    [ObservableProperty]
    private bool _useLdaps = true;

    [ObservableProperty]
    private string _searchBase = string.Empty;

    [ObservableProperty]
    private string? _username;

    [ObservableProperty]
    private string? _password;

    [ObservableProperty]
    private bool _hasStoredPassword;

    // ---- Correlation configuration ----

    [ObservableProperty]
    private string _correlationAttribute = "employeeID";

    [ObservableProperty]
    private string _vaultField = "external_id";

    [ObservableProperty]
    private bool _activeContractOnly = true;

    [ObservableProperty]
    private int _futureContractDays = 30;

    [ObservableProperty]
    private int _acceptAllThreshold = 95;

    public ObservableCollection<AdMatchFieldConfig> MatchFields { get; } = new();

    public string[] VaultFieldOptions { get; } = { "external_id", "user_name", "display_name" };
    public string[] AdAttributeSuggestions { get; } = { "employeeID", "sAMAccountName", "userPrincipalName", "mail", "extensionAttribute1", "extensionAttribute2", "employeeNumber" };

    // ---- State ----

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    // ---- Commands ----

    [RelayCommand]
    private void SaveConfig()
    {
        try
        {
            IsBusy = true;
            StatusMessage = "Saving configuration...";

            var prefs = _userPreferencesService;
            prefs.AdLdapHost = LdapHost?.Trim() ?? string.Empty;
            prefs.AdUseLdaps = UseLdaps;
            prefs.AdSearchBase = SearchBase?.Trim() ?? string.Empty;
            prefs.AdUsername = string.IsNullOrWhiteSpace(Username) ? null : Username.Trim();
            prefs.AdCorrelationAttribute = string.IsNullOrWhiteSpace(CorrelationAttribute) ? "employeeID" : CorrelationAttribute.Trim();
            prefs.AdVaultField = string.IsNullOrWhiteSpace(VaultField) ? "external_id" : VaultField;
            prefs.AdActiveContractOnly = ActiveContractOnly;
            prefs.AdFutureContractDays = Math.Max(0, FutureContractDays);
            prefs.AdAcceptAllThreshold = Math.Clamp(AcceptAllThreshold, 1, 100);
            prefs.AdMatchFieldsJson = System.Text.Json.JsonSerializer.Serialize(MatchFields.ToList());

            // Only update the stored password when the user typed a new one
            if (!string.IsNullOrEmpty(Password))
            {
                prefs.AdEncryptedPassword = _encryptionService?.Encrypt(Password);
                HasStoredPassword = true;
            }

            StatusMessage = "Configuration saved.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Save failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task TestConnectionAsync()
    {
        try
        {
            IsBusy = true;
            StatusMessage = "Testing connection...";

            var config = BuildConfig();
            var plainPassword = !string.IsNullOrEmpty(Password)
                ? Password
                : TryDecryptStoredPassword(_userPreferencesService.AdEncryptedPassword, _encryptionService);

            var (success, message) = await _activeDirectoryService.TestConnectionAsync(config, plainPassword);
            StatusMessage = success ? $"Success: {message}" : $"Failed: {message}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Connection test failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void ClearCredentials()
    {
        try
        {
            var confirmed = System.Windows.MessageBox.Show(
                "This clears the stored AD username and password.\n\n" +
                "The app will then connect with your Windows identity. Continue?",
                "Clear Credentials",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);

            if (confirmed != System.Windows.MessageBoxResult.Yes)
            {
                StatusMessage = "Clear cancelled.";
                return;
            }

            var prefs = _userPreferencesService;
            prefs.AdUsername = null;
            prefs.AdEncryptedPassword = null;

            Username = null;
            Password = null;
            HasStoredPassword = false;

            StatusMessage = "Stored credentials cleared - connecting with your Windows identity.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Clear failed: {ex.Message}";
        }
    }

    // ---- Loading ----

    private async Task LoadConfigAsync()
    {
        var prefs = _userPreferencesService;

        _isInitializing = true;
        try
        {
            LdapHost = prefs.AdLdapHost ?? string.Empty;
            UseLdaps = prefs.AdUseLdaps;
            SearchBase = prefs.AdSearchBase ?? string.Empty;
            Username = prefs.AdUsername;
            CorrelationAttribute = prefs.AdCorrelationAttribute;
            VaultField = prefs.AdVaultField;
            ActiveContractOnly = prefs.AdActiveContractOnly;
            FutureContractDays = prefs.AdFutureContractDays;
            AcceptAllThreshold = prefs.AdAcceptAllThreshold;
            HasStoredPassword = !string.IsNullOrEmpty(prefs.AdEncryptedPassword);
        }
        finally
        {
            _isInitializing = false;
        }

        MatchFields.Clear();
        foreach (var field in ParseMatchFields(prefs.AdMatchFieldsJson))
        {
            MatchFields.Add(field);
        }
    }

    // ---- Helpers ----

    private AdCorrelationConfig BuildConfig()
    {
        return new AdCorrelationConfig
        {
            LdapHost = LdapHost?.Trim() ?? string.Empty,
            LdapPort = UseLdaps ? 636 : 389,
            UseLdaps = UseLdaps,
            SearchBase = SearchBase?.Trim() ?? string.Empty,
            Username = string.IsNullOrWhiteSpace(Username) ? null : Username.Trim(),
            EncryptedPassword = _userPreferencesService.AdEncryptedPassword,
            CorrelationAttribute = string.IsNullOrWhiteSpace(CorrelationAttribute) ? "employeeID" : CorrelationAttribute.Trim(),
            VaultField = string.IsNullOrWhiteSpace(VaultField) ? "external_id" : VaultField,
            ActiveContractOnly = ActiveContractOnly,
            FutureContractDays = Math.Max(0, FutureContractDays),
            MatchFieldsJson = System.Text.Json.JsonSerializer.Serialize(MatchFields.ToList())
        };
    }

    /// <summary>
    /// Decrypts the stored LDAP password so actions can run without re-entering it.
    /// </summary>
    public static string? TryDecryptStoredPassword(string? encryptedPassword, IEncryptionService? encryptionService = null)
    {
        if (encryptionService == null || string.IsNullOrEmpty(encryptedPassword))
        {
            return null;
        }

        try
        {
            return encryptionService.Decrypt(encryptedPassword);
        }
        catch
        {
            return null;
        }
    }

    private List<AdMatchFieldConfig> ParseMatchFields(string? matchFieldsJson)
    {
        if (!string.IsNullOrWhiteSpace(matchFieldsJson))
        {
            try
            {
                var fields = System.Text.Json.JsonSerializer.Deserialize<List<AdMatchFieldConfig>>(matchFieldsJson);
                if (fields != null && fields.Count > 0)
                {
                    return fields;
                }
            }
            catch (System.Text.Json.JsonException)
            {
                // Fall back to defaults
            }
        }
        return AdCorrelationConfig.DefaultMatchFields();
    }
}
