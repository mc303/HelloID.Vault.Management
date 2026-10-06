using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HelloID.Vault.Core.Models.Ad;
using HelloID.Vault.Data.Repositories.Interfaces;
using HelloID.Vault.Services.Interfaces;
using HelloID.Vault.Services.Security;

namespace HelloID.Vault.Management.ViewModels.Administration;

/// <summary>
/// ViewModel for the AD Correlation Results view: run correlation and
/// review per-person match status.
/// </summary>
public partial class AdResultsViewModel : ObservableObject
{
    private readonly IAdCorrelationRepository _repository;
    private readonly IAdCorrelationService _correlationService;
    private readonly IUserPreferencesService _userPreferencesService;
    private readonly IEncryptionService? _encryptionService;
    private bool _isInitializing;

    public AdResultsViewModel(
        IAdCorrelationRepository repository,
        IAdCorrelationService correlationService,
        IUserPreferencesService userPreferencesService,
        IEncryptionService? encryptionService = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _correlationService = correlationService ?? throw new ArgumentNullException(nameof(correlationService));
        _userPreferencesService = userPreferencesService ?? throw new ArgumentNullException(nameof(userPreferencesService));
        _encryptionService = encryptionService;
    }

    public async Task InitializeAsync()
    {
        if (_isInitializing) return;
        _isInitializing = true;
        try
        {
            await _repository.EnsureTablesAsync();
            await LoadResultsAsync();
        }
        finally
        {
            _isInitializing = false;
        }
    }

    // ---- State ----

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private string _progressText = string.Empty;

    [ObservableProperty]
    private AdCorrelationSummary? _summary;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private int _totalCount;

    private CancellationTokenSource? _searchCts;

    public ObservableCollection<AdCorrelationResult> Results { get; } = new();

    private List<AdCorrelationResult> allResults = new();

    [ObservableProperty]
    private string _resultsFilter = "All";

    public string[] ResultsFilterOptions { get; } = { "All", "Matched", "NotFound", "Ambiguous", "ManuallyMatched", "Needs write" };

    private static bool NeedsWrite(AdCorrelationResult r) =>
        r.ValueStatus is "Missing" or "Different" or "Unreadable";

    // ---- Commands ----

    [RelayCommand]
    private async Task RefreshAsync()
    {
        try
        {
            IsBusy = true;
            await LoadResultsAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Refresh failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ClearResultsAsync()
    {
        try
        {
            var confirmed = System.Windows.MessageBox.Show(
                $"This will delete ALL correlation results ({allResults.Count} rows).\n\n" +
                $"You will need to run the correlation again. Continue?",
                "Clear Correlation Results",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);

            if (confirmed != System.Windows.MessageBoxResult.Yes)
            {
                StatusMessage = "Clear cancelled.";
                return;
            }

            IsBusy = true;
            var deleted = await _repository.ClearResultsAsync();
            Summary = null;
            await LoadResultsAsync();
            StatusMessage = $"Cleared {deleted} correlation results.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Clear failed: {ex.Message}";
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

            var (config, password) = LoadConfigWithPassword();
            Summary = await _correlationService.RunCorrelationAsync(config, password, progress);

            await LoadResultsAsync();
            StatusMessage = "Correlation completed.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Correlation failed: {ex.Message}";
            ShowError($"Run Correlation failed:\n\n{ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task WriteCorrelationAttributeAsync()
    {
        try
        {
            var (config, _) = LoadConfigWithPassword();

            // Count matched accounts for the confirmation dialog, with value-status breakdown
            var allResults = await _repository.GetResultsAsync();
            var matchedResults = allResults.Where(r => r.StatusText is "Matched" or "ManuallyMatched").ToList();
            var matchedCount = matchedResults.Count;

            if (matchedCount == 0)
            {
                StatusMessage = "No matched persons - run the correlation first.";
                return;
            }

            var missing = matchedResults.Count(r => r.ValueStatus == "Missing");
            var different = matchedResults.Count(r => r.ValueStatus == "Different");
            var unreadable = matchedResults.Count(r => r.ValueStatus == "Unreadable");
            var correct = matchedResults.Count(r => r.ValueStatus == "Correct");

            var confirmed = System.Windows.MessageBox.Show(
                $"This will modify Active Directory:\n\n" +
                $"Set '{config.CorrelationAttribute}' on matched AD accounts using each person's '{config.VaultField}'.\n\n" +
                $"{matchedCount} matched account(s):\n" +
                $"  - {missing} missing the value (IAM cannot correlate)\n" +
                $"  - {different} with a different value (IAM correlates wrong)\n" +
                $"  - {unreadable} unreadable value (will be written to be safe)\n" +
                $"  - {correct} already correct (skipped)\n\n" +
                $"Continue?",
                "Write Correlation Attribute to AD",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);

            if (confirmed != System.Windows.MessageBoxResult.Yes)
            {
                StatusMessage = "Write cancelled.";
                return;
            }

            IsBusy = true;
            ProgressText = "Starting...";
            var progress = new Progress<string>(msg => ProgressText = msg);

            var (config2, password) = LoadConfigWithPassword();
            var summary = await _correlationService.WriteCorrelationAttributeAsync(config2, password, progress);

            foreach (var error in summary.Errors)
            {
                System.Diagnostics.Debug.WriteLine($"[AdWrite] {error}");
            }

            StatusMessage = $"{summary.TotalMatched} processed: {summary.Written} written, " +
                            $"{summary.AlreadyCorrect} already correct, {summary.UnreadableWritten} unreadable-written, " +
                            $"{summary.SkippedNoValue} skipped, {summary.Failed} failed. " +
                            $"Audit: %LOCALAPPDATA%\\HelloID.Vault.Management\\logs\\ad-write-audit.log";
        }
        catch (UnauthorizedAccessException ex)
        {
            StatusMessage = $"AD was NOT modified - the audit log could not be written: {ex.Message}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Write failed: {ex.Message}";
            ShowError($"Write Correlation Attribute failed:\n\n{ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ---- Loading ----

    partial void OnSearchTextChanged(string value)
    {
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;
        _ = Task.Delay(300, token).ContinueWith(t =>
        {
            if (!t.IsCanceled) System.Windows.Application.Current?.Dispatcher.Invoke(ApplyFilters);
        }, TaskScheduler.FromCurrentSynchronizationContext());
    }


    /// <summary>
    /// Shows an error dialog safely from any thread (dispatches to the UI thread).
    /// </summary>
    private static void ShowError(string message)
    {
        var app = System.Windows.Application.Current;
        if (app?.Dispatcher == null || app.Dispatcher.CheckAccess())
        {
            System.Windows.MessageBox.Show(message, "AD Correlation",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            return;
        }
        app.Dispatcher.Invoke(() =>
            System.Windows.MessageBox.Show(message, "AD Correlation",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error));
    }

    partial void OnResultsFilterChanged(string value) => ApplyFilters();

    private void ApplyFilters()
    {
        var statusFilter = ResultsFilter == "All" ? null : ResultsFilter;
        var search = SearchText?.Trim();

        IEnumerable<AdCorrelationResult> query = allResults;

        if (statusFilter != null)
        {
            query = statusFilter == "Needs write"
                ? query.Where(NeedsWrite)
                : query.Where(r => r.StatusText == statusFilter);
        }

        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(r =>
                Contains(r.PersonDisplayName, search) ||
                Contains(r.ExternalId, search) ||
                Contains(r.AdDisplayName, search) ||
                Contains(r.AdSamAccountName, search) ||
                Contains(r.AdUserPrincipalName, search) ||
                Contains(r.CorrelationValue, search) ||
                Contains(r.StatusText, search));
        }

        Results.Clear();
        foreach (var result in query)
        {
            Results.Add(result);
        }
        TotalCount = Results.Count;
    }

    private static bool Contains(string? value, string search) =>
        value?.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;

    private async Task LoadResultsAsync()
    {
        allResults = await _repository.GetResultsAsync();
        ApplyFilters();
    }

    /// <summary>
    /// Loads the persisted settings and decrypts the stored LDAP password so
    /// actions run without re-entering credentials.
    /// </summary>
    private (AdCorrelationConfig Config, string? Password) LoadConfigWithPassword()
    {
        var prefs = _userPreferencesService;
        var config = new AdCorrelationConfig
        {
            LdapHost = prefs.AdLdapHost ?? string.Empty,
            LdapPort = prefs.AdUseLdaps ? 636 : 389,
            UseLdaps = prefs.AdUseLdaps,
            SearchBase = prefs.AdSearchBase ?? string.Empty,
            Username = prefs.AdUsername,
            EncryptedPassword = prefs.AdEncryptedPassword,
            CorrelationAttribute = prefs.AdCorrelationAttribute,
            VaultField = prefs.AdVaultField,
            ActiveContractOnly = prefs.AdActiveContractOnly,
            FutureContractDays = prefs.AdFutureContractDays,
            MatchFieldsJson = prefs.AdMatchFieldsJson,
            MinScore = prefs.AdMinScore,
            MaxCandidates = prefs.AdMaxCandidates,
            AcceptAllThreshold = prefs.AdAcceptAllThreshold
        };
        var password = AdSettingsViewModel.TryDecryptStoredPassword(prefs.AdEncryptedPassword, _encryptionService);
        return (config, password);
    }
}

/// <summary>
/// ViewModel for the AD Correlation Recommendations view: generate scored
/// match suggestions for uncorrelated persons and accept/reject them.
/// </summary>
public partial class AdRecommendationsViewModel : ObservableObject
{
    private readonly IAdCorrelationRepository _repository;
    private readonly IAdCorrelationService _correlationService;
    private readonly IUserPreferencesService _userPreferencesService;
    private readonly IEncryptionService? _encryptionService;
    private bool _isInitializing;

    public AdRecommendationsViewModel(
        IAdCorrelationRepository repository,
        IAdCorrelationService correlationService,
        IUserPreferencesService userPreferencesService,
        IEncryptionService? encryptionService = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _correlationService = correlationService ?? throw new ArgumentNullException(nameof(correlationService));
        _userPreferencesService = userPreferencesService ?? throw new ArgumentNullException(nameof(userPreferencesService));
        _encryptionService = encryptionService;
    }

    public async Task InitializeAsync()
    {
        if (_isInitializing) return;
        _isInitializing = true;
        try
        {
            await _repository.EnsureTablesAsync();
            var config = BuildConfigFromPreferences(_userPreferencesService);
            ScoreHighlightThreshold = config.AcceptAllThreshold;
            OnPropertyChanged(nameof(AcceptAllLabel));
            await LoadRecommendationsAsync();
        }
        finally
        {
            _isInitializing = false;
        }
    }

    // ---- State ----

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private string _progressText = string.Empty;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private int _totalCount;

    private CancellationTokenSource? _searchCts;

    public ObservableCollection<AdMatchRecommendation> Recommendations { get; } = new();

    private List<AdMatchRecommendation> allRecommendations = new();

    [ObservableProperty]
    private int _scoreHighlightThreshold = 95;

    /// <summary>Button label showing the configured bulk-accept threshold.</summary>
    public string AcceptAllLabel => $"Accept All >= {ScoreHighlightThreshold}%";

    [ObservableProperty]
    private string _recommendationsFilter = "Proposed";

    public string[] RecommendationsFilterOptions { get; } = { "Proposed", "Accepted", "Rejected", "All" };

    // ---- Commands ----

    [RelayCommand]
    private async Task RefreshAsync()
    {
        try
        {
            IsBusy = true;
            await LoadRecommendationsAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Refresh failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ClearRecommendationsAsync()
    {
        try
        {
            var confirmed = System.Windows.MessageBox.Show(
                $"This will delete ALL recommendations ({allRecommendations.Count} rows), " +
                $"including Accepted and Rejected history.\n\n" +
                $"Previously decided pairs can be suggested again. Continue?",
                "Clear Recommendations",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);

            if (confirmed != System.Windows.MessageBoxResult.Yes)
            {
                StatusMessage = "Clear cancelled.";
                return;
            }

            IsBusy = true;
            var deleted = await _repository.ClearRecommendationsAsync();
            await LoadRecommendationsAsync();
            StatusMessage = $"Cleared {deleted} recommendations.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Clear failed: {ex.Message}";
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

            var prefs = _userPreferencesService;
            var config = BuildConfigFromPreferences(prefs);
            var password = AdSettingsViewModel.TryDecryptStoredPassword(prefs.AdEncryptedPassword, _encryptionService);
            var count = await _correlationService.GenerateRecommendationsAsync(config, password, progress);

            await LoadRecommendationsAsync();
            StatusMessage = $"Generated {count} recommendations.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Recommendation generation failed: {ex.Message}";
            ShowError($"Generate Recommendations failed:\n\n{ex.Message}");
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

    [RelayCommand]
    private async Task AcceptAllAsync()
    {
        try
        {
            // Determine how many qualify per threshold tier for the confirmation
            var proposed = await _repository.GetRecommendationsAsync("Proposed");
            var topPerPerson = proposed
                .GroupBy(r => r.PersonId)
                .Select(g => g.OrderByDescending(r => r.ScorePercent).First())
                .ToList();

            if (topPerPerson.Count == 0)
            {
                StatusMessage = "No proposed recommendations - generate first.";
                return;
            }

            var threshold = ScoreHighlightThreshold;
            var qualifying = topPerPerson.Count(r => r.ScorePercent >= threshold);

            var choice = System.Windows.MessageBox.Show(
                $"Bulk-accept every person's TOP recommendation at or above {threshold}%.\n\n" +
                $"{topPerPerson.Count} persons with proposals, {qualifying} at >= {threshold}%.\n\n" +
                $"Accept these {qualifying} matches?",
                "Bulk Accept Recommendations",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);

            if (choice != System.Windows.MessageBoxResult.Yes)
            {
                StatusMessage = "Bulk accept cancelled.";
                return;
            }

            IsBusy = true;
            ProgressText = "Starting...";
            var progress = new Progress<string>(msg => ProgressText = msg);

            var (accepted, skipped) = await _correlationService.BulkAcceptRecommendationsAsync(threshold, progress);

            await LoadRecommendationsAsync();
            StatusMessage = $"Bulk accept ({threshold}%+): {accepted} matched, {skipped} failed.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Bulk accept failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ---- Loading ----

    partial void OnSearchTextChanged(string value)
    {
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;
        _ = Task.Delay(300, token).ContinueWith(t =>
        {
            if (!t.IsCanceled) System.Windows.Application.Current?.Dispatcher.Invoke(ApplyFilters);
        }, TaskScheduler.FromCurrentSynchronizationContext());
    }


    /// <summary>
    /// Shows an error dialog safely from any thread (dispatches to the UI thread).
    /// </summary>
    private static void ShowError(string message)
    {
        var app = System.Windows.Application.Current;
        if (app?.Dispatcher == null || app.Dispatcher.CheckAccess())
        {
            System.Windows.MessageBox.Show(message, "AD Correlation",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            return;
        }
        app.Dispatcher.Invoke(() =>
            System.Windows.MessageBox.Show(message, "AD Correlation",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error));
    }


    private static AdCorrelationConfig BuildConfigFromPreferences(IUserPreferencesService prefs)
    {
        return new AdCorrelationConfig
        {
            LdapHost = prefs.AdLdapHost ?? string.Empty,
            LdapPort = prefs.AdUseLdaps ? 636 : 389,
            UseLdaps = prefs.AdUseLdaps,
            SearchBase = prefs.AdSearchBase ?? string.Empty,
            Username = prefs.AdUsername,
            EncryptedPassword = prefs.AdEncryptedPassword,
            CorrelationAttribute = prefs.AdCorrelationAttribute,
            VaultField = prefs.AdVaultField,
            ActiveContractOnly = prefs.AdActiveContractOnly,
            FutureContractDays = prefs.AdFutureContractDays,
            MatchFieldsJson = prefs.AdMatchFieldsJson,
            MinScore = prefs.AdMinScore,
            MaxCandidates = prefs.AdMaxCandidates,
            AcceptAllThreshold = prefs.AdAcceptAllThreshold
        };
    }

    partial void OnRecommendationsFilterChanged(string value) => ApplyFilters();

    private void ApplyFilters()
    {
        var statusFilter = RecommendationsFilter == "All" ? null : RecommendationsFilter;
        var search = SearchText?.Trim();

        IEnumerable<AdMatchRecommendation> query = allRecommendations;

        if (statusFilter != null)
        {
            query = query.Where(r => r.StatusText == statusFilter);
        }

        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(r =>
                Contains(r.PersonDisplayName, search) ||
                Contains(r.PersonExternalId, search) ||
                Contains(r.AdDisplayName, search) ||
                Contains(r.AdSamAccountName, search) ||
                Contains(r.AdUserPrincipalName, search));
        }

        Recommendations.Clear();
        foreach (var recommendation in query.OrderByDescending(r => r.ScorePercent))
        {
            Recommendations.Add(recommendation);
        }
        TotalCount = Recommendations.Count;
    }

    private static bool Contains(string? value, string search) =>
        value?.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;

    private async Task LoadRecommendationsAsync()
    {
        allRecommendations = await _repository.GetRecommendationsAsync();
        ApplyFilters();
    }
}
