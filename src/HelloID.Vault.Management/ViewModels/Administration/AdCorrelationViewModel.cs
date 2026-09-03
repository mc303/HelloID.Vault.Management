using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HelloID.Vault.Core.Models.Ad;
using HelloID.Vault.Data.Repositories.Interfaces;
using HelloID.Vault.Services.Interfaces;
using HelloID.Vault.Services.Security;

namespace HelloID.Vault.Management.ViewModels.Administration;

/// <summary>
/// ViewModel for the AD Correlation administration view:
/// configure LDAP connection + correlation attribute, run correlation,
/// review recommendations and accept/reject them.
/// </summary>
public partial class AdCorrelationViewModel : ObservableObject
{
    private readonly IAdCorrelationRepository _repository;
    private readonly IActiveDirectoryService _activeDirectoryService;
    private readonly IAdCorrelationService _correlationService;
    private readonly IEncryptionService? _encryptionService;
    private bool _isInitializing;

    public AdCorrelationViewModel(
        IAdCorrelationRepository repository,
        IActiveDirectoryService activeDirectoryService,
        IAdCorrelationService correlationService,
        IEncryptionService? encryptionService = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _activeDirectoryService = activeDirectoryService ?? throw new ArgumentNullException(nameof(activeDirectoryService));
        _correlationService = correlationService ?? throw new ArgumentNullException(nameof(correlationService));
        _encryptionService = encryptionService;
    }

    public async Task InitializeAsync()
    {
        if (_isInitializing) return;
        _isInitializing = true;
        try
        {
            await _repository.EnsureTablesAsync();
            await LoadConfigAsync();
            await LoadResultsAsync();
            await LoadRecommendationsAsync();
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
    private int _ldapPort = 636;

    [ObservableProperty]
    private bool _useLdaps = true;

    [ObservableProperty]
    private string _searchBase = string.Empty;

    [ObservableProperty]
    private string _authType = "Negotiate";

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
    private int _minScore = 50;

    [ObservableProperty]
    private int _maxCandidates = 3;

    public ObservableCollection<AdMatchFieldConfig> MatchFields { get; } = new();

    public string[] AuthTypeOptions { get; } = { "Negotiate", "Simple", "Anonymous" };
    public string[] VaultFieldOptions { get; } = { "external_id", "user_name", "display_name" };
    public string[] AdAttributeSuggestions { get; } = { "employeeID", "sAMAccountName", "userPrincipalName", "mail", "extensionAttribute1", "extensionAttribute2", "employeeNumber" };

    // ---- State ----

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private string _progressText = string.Empty;

    [ObservableProperty]
    private AdCorrelationSummary? _summary;

    public ObservableCollection<AdCorrelationResult> Results { get; } = new();

    public ObservableCollection<AdMatchRecommendation> Recommendations { get; } = new();

    [ObservableProperty]
    private string _resultsFilter = "All";

    [ObservableProperty]
    private string _recommendationsFilter = "Proposed";

    public string[] ResultsFilterOptions { get; } = { "All", "Matched", "NotFound", "Ambiguous", "ManuallyMatched" };
    public string[] RecommendationsFilterOptions { get; } = { "Proposed", "Accepted", "Rejected", "All" };

    // ---- Commands ----

    [RelayCommand]
    private async Task SaveConfigAsync()
    {
        var config = BuildConfig(persistPassword: false);
        try
        {
            IsBusy = true;
            StatusMessage = "Saving configuration...";
            await _repository.SaveConfigAsync(config);
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
            var config = BuildConfig(persistPassword: false);
            var (success, message) = await _activeDirectoryService.TestConnectionAsync(config, Password);
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
    private async Task RunCorrelationAsync()
    {
        try
        {
            IsBusy = true;
            ProgressText = "Starting...";
            var progress = new Progress<string>(msg => ProgressText = msg);

            var config = BuildConfig(persistPassword: false);
            Summary = await _correlationService.RunCorrelationAsync(config, Password, progress);

            await LoadResultsAsync();
            StatusMessage = "Correlation completed.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Correlation failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task GenerateRecommendationsAsync()
    {
        try
        {
            IsBusy = true;
            ProgressText = "Starting...";
            var progress = new Progress<string>(msg => ProgressText = msg);

            var config = BuildConfig(persistPassword: false);
            var count = await _correlationService.GenerateRecommendationsAsync(config, Password, progress);

            await LoadRecommendationsAsync();
            StatusMessage = $"Generated {count} recommendations.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Recommendation generation failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task AcceptRecommendationAsync(AdMatchRecommendation? recommendation)
    {
        if (recommendation == null) return;

        try
        {
            IsBusy = true;
            await _correlationService.AcceptRecommendationAsync(recommendation.PersonId, recommendation.AdObjectGuid);
            await LoadResultsAsync();
            await LoadRecommendationsAsync();
            StatusMessage = $"Matched {recommendation.PersonDisplayName} to {recommendation.AdDisplayName}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Accept failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RejectRecommendationAsync(AdMatchRecommendation? recommendation)
    {
        if (recommendation == null) return;

        try
        {
            IsBusy = true;
            await _correlationService.RejectRecommendationAsync(recommendation.PersonId, recommendation.AdObjectGuid);
            await LoadRecommendationsAsync();
            StatusMessage = "Recommendation rejected.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Reject failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ---- Loading ----

    private async Task LoadConfigAsync()
    {
        var config = await _repository.GetConfigAsync();

        LdapHost = config.LdapHost;
        LdapPort = config.LdapPort;
        UseLdaps = config.UseLdaps;
        SearchBase = config.SearchBase;
        AuthType = config.AuthType;
        Username = config.Username;
        CorrelationAttribute = config.CorrelationAttribute;
        VaultField = config.VaultField;
        MinScore = config.MinScore;
        MaxCandidates = config.MaxCandidates;
        HasStoredPassword = !string.IsNullOrEmpty(config.EncryptedPassword);

        MatchFields.Clear();
        var fields = ParseMatchFields(config);
        foreach (var field in fields)
        {
            MatchFields.Add(field);
        }
    }

    private async Task LoadResultsAsync()
    {
        var filter = ResultsFilter == "All" ? null : ResultsFilter;
        var results = await _repository.GetResultsAsync(filter);
        Results.Clear();
        foreach (var result in results)
        {
            Results.Add(result);
        }
    }

    private async Task LoadRecommendationsAsync()
    {
        var filter = RecommendationsFilter == "All" ? null : RecommendationsFilter;
        var recommendations = await _repository.GetRecommendationsAsync(filter);
        Recommendations.Clear();
        foreach (var recommendation in recommendations)
        {
            Recommendations.Add(recommendation);
        }
    }

    partial void OnResultsFilterChanged(string value) => _ = LoadResultsAsync();
    partial void OnRecommendationsFilterChanged(string value) => _ = LoadRecommendationsAsync();

    // ---- Helpers ----

    private AdCorrelationConfig BuildConfig(bool persistPassword)
    {
        var config = new AdCorrelationConfig
        {
            LdapHost = LdapHost?.Trim() ?? string.Empty,
            LdapPort = LdapPort,
            UseLdaps = UseLdaps,
            SearchBase = SearchBase?.Trim() ?? string.Empty,
            AuthType = AuthType,
            Username = string.IsNullOrWhiteSpace(Username) ? null : Username.Trim(),
            CorrelationAttribute = string.IsNullOrWhiteSpace(CorrelationAttribute) ? "employeeID" : CorrelationAttribute.Trim(),
            VaultField = string.IsNullOrWhiteSpace(VaultField) ? "external_id" : VaultField,
            MinScore = MinScore,
            MaxCandidates = MaxCandidates,
            MatchFieldsJson = System.Text.Json.JsonSerializer.Serialize(MatchFields.ToList())
        };

        // Only update the stored password when the user typed a new one;
        // otherwise carry over the existing encrypted value so it is not lost on save
        if (!string.IsNullOrEmpty(Password))
        {
            config.EncryptedPassword = _encryptionService?.Encrypt(Password);
            HasStoredPassword = true;
        }
        else
        {
            var existing = _repository.GetConfigAsync().GetAwaiter().GetResult();
            config.EncryptedPassword = existing.EncryptedPassword;
        }

        return config;
    }

    private static List<AdMatchFieldConfig> ParseMatchFields(AdCorrelationConfig config)
    {
        if (!string.IsNullOrWhiteSpace(config.MatchFieldsJson))
        {
            try
            {
                var fields = System.Text.Json.JsonSerializer.Deserialize<List<AdMatchFieldConfig>>(config.MatchFieldsJson);
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
