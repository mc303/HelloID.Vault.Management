using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HelloID.Vault.Core.Models.DTOs;
using HelloID.Vault.Data.Repositories.Interfaces;
using HelloID.Vault.Management.Views.ReferenceData;
using HelloID.Vault.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace HelloID.Vault.Management.ViewModels.ReferenceData;

/// <summary>
/// ViewModel for Source Systems reference data management.
/// Provides enhanced view of source systems with usage statistics.
/// </summary>
public partial class SourceSystemsViewModel : ObservableObject
{
    private readonly ISourceSystemRepository _sourceSystemRepository;
    private readonly IUserPreferencesService _userPreferencesService;
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    private ObservableCollection<SourceSystemDto> _sourceSystems = new();

    [ObservableProperty]
    private SourceSystemDto? _selectedSourceSystem;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private int _totalCount;

    [ObservableProperty]
    private int _unusedCount;

    public SourceSystemsViewModel(ISourceSystemRepository sourceSystemRepository, IServiceProvider serviceProvider)
    {
        _sourceSystemRepository = sourceSystemRepository ?? throw new ArgumentNullException(nameof(sourceSystemRepository));
        _userPreferencesService = serviceProvider.GetRequiredService<IUserPreferencesService>();
        _dialogService = serviceProvider.GetRequiredService<IDialogService>();
    }

    public async Task InitializeAsync()
    {
        await LoadDataAsync();
        LoadPersistedState();
    }

    [RelayCommand]
    private async Task LoadDataAsync()
    {
        if (IsBusy) return;

        try
        {
            IsBusy = true;

            var sourceSystems = await _sourceSystemRepository.GetAllAsync();
            _allItems = sourceSystems.ToList();

            // Apply search filter
            var filteredItems = _allItems;

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                filteredItems = _allItems
                    .Where(item =>
                        item.DisplayName.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                        item.IdentificationKey.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            SourceSystems.Clear();
            foreach (var item in filteredItems.OrderBy(x => x.DisplayName))
            {
                SourceSystems.Add(item);
            }

            TotalCount = _allItems.Count;
            UnusedCount = _allItems.Count(x => x.ReferenceCount == 0);
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"Error loading source systems: {ex.Message}", "Error");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RefreshDataAsync()
    {
        await LoadDataAsync();
    }

    [RelayCommand]
    private void Search()
    {
        // Apply search filter immediately (no async operation needed)
        var filteredItems = _allItems;

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            filteredItems = _allItems
                .Where(item =>
                    item.DisplayName.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                    item.IdentificationKey.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        SourceSystems.Clear();
        foreach (var item in filteredItems.OrderBy(x => x.DisplayName))
        {
            SourceSystems.Add(item);
        }
    }

    [RelayCommand]
    private void ClearSearch()
    {
        SearchText = string.Empty;
        Search();
    }

    [RelayCommand]
    private void ShowUsageStatistics()
    {
        try
        {
            var statistics = _sourceSystemRepository.GetUsageStatisticsAsync().Result;

            var message = string.Join(Environment.NewLine,
                statistics.Select(x => $"{x.Key}: {x.Value} references"));

            _dialogService.ShowInfo($"Source System Usage Statistics:{Environment.NewLine}{Environment.NewLine}{message}",
                "Usage Statistics");
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"Error loading usage statistics: {ex.Message}", "Error");
        }
    }

    [RelayCommand]
    private async Task ShowUnusedSourceSystems()
    {
        try
        {
            IsBusy = true;

            var unusedSystems = await _sourceSystemRepository.GetUnusedAsync();

            if (unusedSystems.Any())
            {
                var message = string.Join(Environment.NewLine,
                    unusedSystems.Select(x => $"{x.DisplayName} ({x.SystemId})"));

                _dialogService.ShowInfo($"Unused Source Systems ({unusedSystems.Count()}):{Environment.NewLine}{Environment.NewLine}{message}",
                    "Unused Source Systems");
            }
            else
            {
                _dialogService.ShowInfo("All source systems are in use.", "Unused Source Systems");
            }
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"Error loading unused source systems: {ex.Message}", "Error");
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnSearchTextChanged(string value)
    {
        // Save search text to preferences
        _userPreferencesService.LastSourceSystemSearchText = value;
        Search();
    }

    // Save selected source system when changed
    partial void OnSelectedSourceSystemChanged(SourceSystemDto? value)
    {
        _userPreferencesService.LastSelectedSourceSystemId = value?.SystemId;
    }

    /// <summary>
    /// Loads persisted state from preferences and applies it to the view.
    /// Called after source systems are loaded.
    /// </summary>
    public void LoadPersistedState()
    {
        // Get saved source system ID before applying filter (filter will rebuild SourceSystems collection)
        var savedSystemId = _userPreferencesService.LastSelectedSourceSystemId;

        // Restore search text (triggers Search via OnSearchTextChanged)
        var savedSearchText = _userPreferencesService.LastSourceSystemSearchText;
        if (!string.IsNullOrWhiteSpace(savedSearchText))
        {
            SearchText = savedSearchText;
        }

        // Restore selected source system AFTER filter is applied
        // We need to wait for the UI to update, so use Dispatcher
        Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
        {
            if (!string.IsNullOrWhiteSpace(savedSystemId))
            {
                SelectedSourceSystem = SourceSystems.FirstOrDefault(s => s.SystemId == savedSystemId);
            }
        }), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    /// <summary>
    /// Resets all settings. Clears search text and selection.
    /// </summary>
    [RelayCommand]
    private void ResetSettings()
    {
        // Clear search text
        SearchText = string.Empty;

        // Clear selection
        SelectedSourceSystem = null;

        // Clear saved preferences
        _userPreferencesService.LastSelectedSourceSystemId = null;
        _userPreferencesService.LastSourceSystemSearchText = null;
    }

    [RelayCommand]
    private void AddSourceSystem()
    {
        try
        {
            var viewModel = new SourceSystemEditViewModel(_sourceSystemRepository);
            var window = new SourceSystemEditWindow(viewModel);
            window.Owner = Application.Current.MainWindow;

            if (window.ShowDialog() == true)
            {
                _ = LoadDataAsync();
            }
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"Error opening add source system dialog: {ex.Message}", "Error");
        }
    }

    [RelayCommand]
    private void EditSourceSystem()
    {
        try
        {
            if (SelectedSourceSystem == null)
            {
                _dialogService.ShowWarning("Please select a source system to edit.", "No Selection");
                return;
            }

            var viewModel = new SourceSystemEditViewModel(_sourceSystemRepository, SelectedSourceSystem);
            var window = new SourceSystemEditWindow(viewModel);
            window.Owner = Application.Current.MainWindow;

            if (window.ShowDialog() == true)
            {
                _ = LoadDataAsync();
            }
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"Error opening edit source system dialog: {ex.Message}", "Error");
        }
    }

    private List<SourceSystemDto> _allItems = new();

    /// <summary>
    /// Saves the current column order to preferences.
    /// Called from view when columns are reordered.
    /// </summary>
    public void SaveColumnOrder(List<string> columnNames)
    {
        _userPreferencesService.SourceSystemsColumnOrder = columnNames;
    }

    /// <summary>
    /// Gets the saved column order from preferences.
    /// Called from view to restore column order.
    /// </summary>
    public List<string>? GetSavedColumnOrder()
    {
        var order = _userPreferencesService.SourceSystemsColumnOrder;
        if (order == null)
        {
        }
        else
        {
        }
        return order;
    }

    /// <summary>
    /// Saves the current column widths to preferences.
    /// Called from view when column widths are changed.
    /// </summary>
    public void SaveColumnWidths(Dictionary<string, double> columnWidths)
    {
        _userPreferencesService.SourceSystemsColumnWidths = columnWidths;
    }

    /// <summary>
    /// Gets the saved column widths from preferences.
    /// Called from view to restore column widths.
    /// </summary>
    public Dictionary<string, double>? GetSavedColumnWidths()
    {
        var widths = _userPreferencesService.SourceSystemsColumnWidths;
        if (widths == null)
        {
        }
        else
        {
        }
        return widths;
    }

    /// <summary>
    /// Resets the saved column order to default (XAML order).
    /// </summary>
    [RelayCommand]
    private void ResetColumnOrder()
    {
        _userPreferencesService.SourceSystemsColumnOrder = null;
    }

    /// <summary>
    /// Resets the saved column widths to default (XAML widths).
    /// </summary>
    [RelayCommand]
    private void ResetColumnWidths()
    {
        _userPreferencesService.SourceSystemsColumnWidths = null;
    }
}