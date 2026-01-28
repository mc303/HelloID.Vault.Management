using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HelloID.Vault.Core.Models;
using HelloID.Vault.Core.Models.DTOs;
using HelloID.Vault.Services;
using HelloID.Vault.Services.Interfaces;
using HelloID.Vault.Management.Services;
using Microsoft.Extensions.DependencyInjection;
using HelloID.Vault.Management.Views.Contracts;
using HelloID.Vault.Management.Views.Persons;
using HelloID.Vault.Management.ViewModels.Persons;

namespace HelloID.Vault.Management.ViewModels.Contracts;

/// <summary>
/// ViewModel for Contracts module.
/// Uses in-memory filtering for fast search across all contracts.
/// Implements IDisposable to properly clean up the debounce timer.
/// </summary>
public partial class ContractsViewModel : ObservableObject, IDisposable
{
    // Cached reflection result for string properties in ContractDetailDto
    private static readonly Lazy<System.Reflection.PropertyInfo[]> _stringProperties = new(() =>
        typeof(ContractDetailDto).GetProperties()
            .Where(p => p.PropertyType == typeof(string) && p.CanRead)
            .ToArray());

    private readonly IContractService _contractService;
    private readonly IServiceProvider _serviceProvider;
    private readonly IUserPreferencesService _userPreferencesService;
    private readonly IDialogService _dialogService;
    private readonly IColumnLayoutManager _columnLayoutManager;
    private readonly DispatcherTimer _filterDebounceTimer;

    // Event raised when data is loaded and ready for column creation
    public event EventHandler? DataLoaded;

    // Master list - loaded once
    private List<ContractDetailDto> _allContracts = new();

    // Active advanced filters
    private List<FilterCriteriaDto> _activeFilters = new();

    // Keep reference to search window
    private AdvancedContractSearchWindow? _searchWindow;
    private bool _isInitializing;

    [ObservableProperty]
    private ObservableCollection<ContractDetailDto> _contracts = new();

    [ObservableProperty]
    private ContractDetailDto? _selectedContract;

    // Global search
    [ObservableProperty]
    private string _searchText = string.Empty;

    // Status filtering properties
    private bool _showAll = true;
    private bool _showPast;
    private bool _showActive;
    private bool _showFuture;

    [ObservableProperty]
    private int _activeFilterCount;

    // UI state
    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _loadingMessage = string.Empty;

    [ObservableProperty]
    private int _loadingProgress;

    [ObservableProperty]
    private int _totalCount;

    [ObservableProperty]
    private int _filteredCount;

    // Column visibility management - delegated to ColumnLayoutManager
    public ContractsColumnVisibility ColumnVisibility => _columnLayoutManager.ColumnVisibility;

    // DataGrid reference for column operations - delegated to ColumnLayoutManager
    public DataGrid? ContractsDataGrid
    {
        get => _columnLayoutManager.TargetDataGrid;
        set => _columnLayoutManager.TargetDataGrid = value;
    }

    public ContractsViewModel(IContractService contractService, IServiceProvider serviceProvider)
    {
        _contractService = contractService ?? throw new ArgumentNullException(nameof(contractService));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _userPreferencesService = serviceProvider.GetRequiredService<IUserPreferencesService>();
        _dialogService = serviceProvider.GetRequiredService<IDialogService>();
        _columnLayoutManager = serviceProvider.GetRequiredService<IColumnLayoutManager>();

        // Initialize filter debounce timer (200ms delay)
        _filterDebounceTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(200)
        };
        _filterDebounceTimer.Tick += OnFilterDebounceTimerElapsed;

        // NOTE: Column visibility is initialized in InitializeAsync(), not here.
        // This prevents saving default values before preferences are loaded.
    }

    // Status filter properties with mutual exclusivity
    public bool ShowAll
    {
        get => _showAll;
        set
        {
            if (SetProperty(ref _showAll, value) && value)
            {
                _showPast = false;
                _showActive = false;
                _showFuture = false;
                OnPropertyChanged(nameof(ShowPast));
                OnPropertyChanged(nameof(ShowActive));
                OnPropertyChanged(nameof(ShowFuture));
                ApplyFilters();

                // Save status filter preference
                _userPreferencesService.LastContractStatusFilter = "All";
            }
        }
    }

    public bool ShowPast
    {
        get => _showPast;
        set
        {
            if (SetProperty(ref _showPast, value) && value)
            {
                _showAll = false;
                _showActive = false;
                _showFuture = false;
                OnPropertyChanged(nameof(ShowAll));
                OnPropertyChanged(nameof(ShowActive));
                OnPropertyChanged(nameof(ShowFuture));
                ApplyFilters();

                // Save status filter preference
                _userPreferencesService.LastContractStatusFilter = "Past";
            }
        }
    }

    public bool ShowActive
    {
        get => _showActive;
        set
        {
            if (SetProperty(ref _showActive, value) && value)
            {
                _showAll = false;
                _showPast = false;
                _showFuture = false;
                OnPropertyChanged(nameof(ShowAll));
                OnPropertyChanged(nameof(ShowPast));
                OnPropertyChanged(nameof(ShowFuture));
                ApplyFilters();

                // Save status filter preference
                _userPreferencesService.LastContractStatusFilter = "Active";
            }
        }
    }

    public bool ShowFuture
    {
        get => _showFuture;
        set
        {
            if (SetProperty(ref _showFuture, value) && value)
            {
                _showAll = false;
                _showPast = false;
                _showActive = false;
                OnPropertyChanged(nameof(ShowAll));
                OnPropertyChanged(nameof(ShowPast));
                OnPropertyChanged(nameof(ShowActive));
                ApplyFilters();

                // Save status filter preference
                _userPreferencesService.LastContractStatusFilter = "Future";
            }
        }
    }

    private void OnFilterDebounceTimerElapsed(object? sender, EventArgs e)
    {
        // DispatcherTimer already runs on UI thread, no need for Dispatcher.Invoke
        _filterDebounceTimer.Stop();
        ApplyFilters();
    }

    /// <summary>
    /// Loads all contracts into memory on initialization.
    /// Ensures cache is populated on first use.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_isInitializing)
        {
            return;
        }

        _isInitializing = true;

        try
        {
            // Load saved column visibility preferences FIRST
            InitializeColumnVisibility();

            // Check if cache needs initialization
            var cacheMetadata = await _contractService.GetCacheMetadataAsync();

            if (cacheMetadata.RowCount == 0)
            {
                await _contractService.RebuildCacheAsync();
            }

            await LoadAllContractsAsync();

            // Auto-hide empty columns after import (only on first load after import)
            if (!_userPreferencesService.ContractsColumnVisibilityInitialized)
            {
                UpdateEmptyColumnVisibility();
                _userPreferencesService.ContractsColumnVisibilityInitialized = true;
            }
        }
        catch (Exception ex)
        {
            _dialogService.ShowWarning($"Error rebuilding contract cache: {ex.Message}", "Cache Error");
        }
        finally
        {
            _isInitializing = false;
        }
    }

    /// <summary>
    /// Loads all contracts from the database with progress reporting.
    /// Uses cached table for fast performance (~100ms).
    /// </summary>
    [RelayCommand]
    private async Task LoadAllContractsAsync()
    {
        if (IsLoading) return;

        try
        {
            IsLoading = true;
            IsBusy = true;
            LoadingProgress = 0;
            LoadingMessage = "Loading contracts from cache...";

            LoadingProgress = 10;

            // Load from cache (fast, ~100ms)
            var allData = await _contractService.GetAllFromCacheAsync();

            LoadingProgress = 50;
            LoadingMessage = "Processing data...";

            _allContracts = allData.ToList();
            TotalCount = _allContracts.Count;

            LoadingProgress = 75;
            LoadingMessage = "Applying filters...";

            ApplyFilters();

            LoadingProgress = 90;
            LoadingMessage = "Restoring state...";

            // Restore persisted state (search, filters, selection)
            LoadPersistedState();

            LoadingProgress = 100;
            LoadingMessage = "Complete!";

            // Signal view that data is ready for column creation
            DataLoaded?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"Error loading contracts: {ex.Message}", "Error");
        }
        finally
        {
            IsLoading = false;
            IsBusy = false;
        }
    }

    /// <summary>
    /// Applies filters (global search, status filters, or advanced filters) to the master list and updates the display.
    /// </summary>
    private void ApplyFilters()
    {
        IEnumerable<ContractDetailDto> filtered = _allContracts;

        // Apply status filtering first
        if (!ShowAll)
        {
            var selectedStatus = ShowPast ? "Past" : (ShowActive ? "Active" : (ShowFuture ? "Future" : null));
            if (selectedStatus != null)
            {
                filtered = filtered.Where(c => c.ContractStatus == selectedStatus);
            }
        }

        // Apply advanced filters if any exist
        if (_activeFilters.Any())
        {
            foreach (var filter in _activeFilters)
            {
                filtered = ApplyFilterCriteria(filtered, filter);
            }
        }
        // Otherwise apply global search (in addition to status filter)
        else if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var searchTerm = SearchText.Trim();

            // Use cached string properties for better performance
            var stringProperties = _stringProperties.Value;

            filtered = filtered.Where(c =>
                stringProperties.Any(prop =>
                {
                    var value = prop.GetValue(c)?.ToString();
                    return value?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) == true;
                })
            );
        }

        // Update display collection efficiently
        var results = filtered.ToList();
        FilteredCount = results.Count;

        Contracts.Clear();
        foreach (var item in results)
            Contracts.Add(item);
    }

    /// <summary>
    /// Applies a single filter criterion to the data using reflection.
    /// </summary>
    private IEnumerable<ContractDetailDto> ApplyFilterCriteria(IEnumerable<ContractDetailDto> data, FilterCriteriaDto filter)
    {
        var property = typeof(ContractDetailDto).GetProperty(filter.FieldName);
        if (property == null) return data;

        return filter.Operator switch
        {
            "Contains" => data.Where(c =>
            {
                var value = property.GetValue(c)?.ToString();
                return value?.Contains(filter.Value, StringComparison.OrdinalIgnoreCase) == true;
            }),

            "Equals" => data.Where(c =>
            {
                var value = property.GetValue(c)?.ToString();
                return value?.Equals(filter.Value, StringComparison.OrdinalIgnoreCase) == true;
            }),

            "Starts With" => data.Where(c =>
            {
                var value = property.GetValue(c)?.ToString();
                return value?.StartsWith(filter.Value, StringComparison.OrdinalIgnoreCase) == true;
            }),

            "Ends With" => data.Where(c =>
            {
                var value = property.GetValue(c)?.ToString();
                return value?.EndsWith(filter.Value, StringComparison.OrdinalIgnoreCase) == true;
            }),

            "Greater Than" => data.Where(c =>
            {
                var value = property.GetValue(c);
                if (value == null) return false;

                // Try numeric comparison
                if (double.TryParse(value.ToString(), out var numValue) &&
                    double.TryParse(filter.Value, out var filterNumValue))
                {
                    return numValue > filterNumValue;
                }

                // Try date comparison
                if (DateTime.TryParse(value.ToString(), out var dateValue) &&
                    DateTime.TryParse(filter.Value, out var filterDateValue))
                {
                    return dateValue > filterDateValue;
                }

                return false;
            }),

            "Less Than" => data.Where(c =>
            {
                var value = property.GetValue(c);
                if (value == null) return false;

                // Try numeric comparison
                if (double.TryParse(value.ToString(), out var numValue) &&
                    double.TryParse(filter.Value, out var filterNumValue))
                {
                    return numValue < filterNumValue;
                }

                // Try date comparison
                if (DateTime.TryParse(value.ToString(), out var dateValue) &&
                    DateTime.TryParse(filter.Value, out var filterDateValue))
                {
                    return dateValue < filterDateValue;
                }

                return false;
            }),

            "Is Empty" => data.Where(c =>
            {
                var value = property.GetValue(c);
                return value == null || string.IsNullOrWhiteSpace(value.ToString());
            }),

            "Is Not Empty" => data.Where(c =>
            {
                var value = property.GetValue(c);
                return value != null && !string.IsNullOrWhiteSpace(value.ToString());
            }),

            _ => data
        };
    }

    /// <summary>
    /// Executes search based on current filters.
    /// </summary>
    [RelayCommand]
    private void Search()
    {
        ApplyFilters();
    }

    /// <summary>
    /// Opens the Advanced Search dialog.
    /// </summary>
    [RelayCommand]
    private void ShowAdvancedSearch()
    {
        // If window exists and is still open, just activate it
        if (_searchWindow != null && _searchWindow.IsLoaded)
        {
            _searchWindow.Activate();
            return;
        }

        // Create new window
        _searchWindow = new AdvancedContractSearchWindow();
        _searchWindow.Owner = Application.Current.MainWindow;

        // Populate with existing filters if any
        if (_activeFilters.Any())
        {
            _searchWindow.ViewModel.Filters.Clear();
            foreach (var filter in _activeFilters)
            {
                _searchWindow.ViewModel.Filters.Add(new FilterCriteriaDto
                {
                    FieldName = filter.FieldName,
                    FieldDisplayName = filter.FieldDisplayName,
                    Operator = filter.Operator,
                    Value = filter.Value
                });
            }
        }

        // Subscribe to the FiltersApplied event
        _searchWindow.ViewModel.FiltersApplied += (sender, validFilters) =>
        {
            _activeFilters = validFilters.Select(f => new FilterCriteriaDto
            {
                FieldName = f.FieldName,
                FieldDisplayName = f.FieldDisplayName,
                Operator = f.Operator,
                Value = f.Value
            }).ToList();

            ActiveFilterCount = _activeFilters.Count;

            // Clear global search when using advanced filters
            SearchText = string.Empty;

            ApplyFilters();

            // Save advanced filters to preferences
            SaveAdvancedFilters();
        };

        // Subscribe to the FiltersCleared event
        _searchWindow.ViewModel.FiltersCleared += (sender, e) =>
        {
            _activeFilters.Clear();
            ActiveFilterCount = 0;
            SearchText = string.Empty;
            ApplyFilters();

            // Clear saved advanced filters
            _userPreferencesService.LastContractAdvancedFilters = null;
        };

        // Handle window closing to clear reference
        _searchWindow.Closed += (sender, e) =>
        {
            _searchWindow = null;
        };

        // Show dialog (non-modal behavior - stays open)
        _searchWindow.Show();
    }

    /// <summary>
    /// Clears all advanced filters and shows all data.
    /// </summary>
    [RelayCommand]
    private void ClearAdvancedFilters()
    {
        // Close the search window if it's open
        if (_searchWindow != null && _searchWindow.IsLoaded)
        {
            _searchWindow.Close();
            _searchWindow = null;
        }

        // Clear filters
        _activeFilters.Clear();
        ActiveFilterCount = 0;
        SearchText = string.Empty;

        // Refresh view to show all data
        ApplyFilters();
    }

    /// <summary>
    /// Refreshes data by reloading from database.
    /// </summary>
    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (IsLoading) return;

        try
        {
            IsLoading = true;
            IsBusy = true;
            LoadingProgress = 0;
            LoadingMessage = "Loading contracts from cache...";

            LoadingProgress = 10;

            // Load from cache (fast, ~100ms)
            var allData = await _contractService.GetAllFromCacheAsync();

            LoadingProgress = 50;
            LoadingMessage = "Processing data...";

            _allContracts = allData.ToList();
            TotalCount = _allContracts.Count;

            LoadingProgress = 75;
            LoadingMessage = "Applying filters...";

            ApplyFilters();

            LoadingProgress = 90;
            LoadingMessage = "Restoring state...";

            // Restore persisted state (search, filters, selection)
            LoadPersistedState();

            LoadingProgress = 100;
            LoadingMessage = "Complete!";

            // Signal view that data is ready for column creation
            DataLoaded?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"Error refreshing contracts: {ex.Message}", "Error");
        }
        finally
        {
            IsLoading = false;
            IsBusy = false;
        }
    }

    /// <summary>
    /// Opens contract edit window to add a new contract.
    /// </summary>
    [RelayCommand]
    private void AddContract()
    {
        _dialogService.ShowInfo(
            "To add a contract, please navigate to a person's detail view.",
            "Add Contract");
    }

    /// <summary>
    /// Opens the contract edit window to edit the selected contract.
    /// </summary>
    [RelayCommand]
    private async Task EditContractAsync()
    {
        if (SelectedContract == null) return;

        try
        {
            var contract = await _contractService.GetByIdAsync(SelectedContract.ContractId);
            if (contract == null)
            {
                _dialogService.ShowError("Contract not found.", "Error");
                return;
            }

            var editViewModel = ActivatorUtilities.CreateInstance<ContractEditViewModel>(
                _serviceProvider,
                SelectedContract.PersonId,
                contract);
            var editWindow = new ContractEditWindow();
            editWindow.SetViewModel(editViewModel);
            editWindow.Owner = Application.Current.MainWindow;

            if (editWindow.ShowDialog() == true)
            {
                await RefreshAsync();
            }
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"Error opening contract window: {ex.Message}", "Error");
        }
    }

    /// <summary>
    /// Views the selected contract in read-only mode.
    /// </summary>
    [RelayCommand]
    private async Task ViewContractAsync()
    {
        if (SelectedContract == null) return;

        try
        {
            var contractJson = await _contractService.GetContractJsonByIdAsync(SelectedContract.ContractId);
            if (contractJson == null)
            {
                _dialogService.ShowError("Contract not found.", "Error");
                return;
            }

            var viewViewModel = new ContractViewViewModel(contractJson);
            var viewWindow = new ContractViewWindow();
            viewWindow.SetViewModel(viewViewModel);
            viewWindow.Owner = Application.Current.MainWindow;
            viewWindow.ShowDialog();
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"Error opening contract window: {ex.Message}", "Error");
        }
    }

    /// <summary>
    /// Deletes the selected contract after confirmation.
    /// </summary>
    [RelayCommand]
    private async Task DeleteContractAsync()
    {
        if (SelectedContract == null) return;

        var result = _dialogService.ShowConfirm(
            $"Are you sure you want to delete contract {SelectedContract.ContractId}?",
            "Delete Contract");

        if (result)
        {
            try
            {
                await _contractService.DeleteAsync(SelectedContract.ContractId);
                await RefreshAsync();
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(
                    $"Error deleting contract: {ex.Message}",
                    "Error");
            }
        }
    }

    // Auto-trigger filtering when search text changes (debounced)
    partial void OnSearchTextChanged(string value)
    {
        _filterDebounceTimer.Stop();
        _filterDebounceTimer.Start();

        // Save search text to preferences
        _userPreferencesService.LastContractSearchText = value;
    }

    // Save selected contract when changed
    partial void OnSelectedContractChanged(ContractDetailDto? value)
    {
        _userPreferencesService.LastSelectedContractId = value?.ContractId;
    }

    // Button commands for status filtering
    [RelayCommand]
    private void ShowAllContracts()
    {
        ShowAll = true;
    }

    [RelayCommand]
    private void ShowPastContracts()
    {
        ShowPast = true;
    }

    [RelayCommand]
    private void ShowActiveContracts()
    {
        ShowActive = true;
    }

    [RelayCommand]
    private void ShowFutureContracts()
    {
        ShowFuture = true;
    }

    /// <summary>
    /// Initializes column visibility from preferences or defaults.
    /// Delegates to ColumnLayoutManager.
    /// </summary>
    private void InitializeColumnVisibility()
    {
        _columnLayoutManager.InitializeColumnVisibility();
    }

    /// <summary>
    /// Opens the column picker dialog.
    /// </summary>
    [RelayCommand]
    private async Task ShowColumnPickerAsync()
    {
        Debug.WriteLine($"[ContractsViewModel] ShowColumnPickerAsync called");
        Debug.WriteLine($"[ContractsViewModel] this type: {GetType().Name}");

        var dialog = new ColumnPickerDialog
        {
            Owner = Application.Current.MainWindow,
            DataContext = this,
            TargetDataGrid = ContractsDataGrid
        };

        Debug.WriteLine($"[ContractsViewModel] Dialog DataContext type: {dialog.DataContext?.GetType().Name}");

        if (dialog.ShowDialog() == true)
        {
            // Save to preferences - await to ensure file is written before continuing
            _userPreferencesService.ContractsColumnVisibility = ColumnVisibility;
            await _userPreferencesService.SaveAsync();
        }
    }

    /// <summary>
    /// Detects and hides columns that have no data after import.
    /// Called after data import to automatically hide empty columns.
    /// Delegates to ColumnLayoutManager.
    /// </summary>
    public void UpdateEmptyColumnVisibility()
    {
        _columnLayoutManager.UpdateEmptyColumnVisibility(_allContracts);
    }

    /// <summary>
    /// Hides columns that have no data (command version for UI binding).
    /// Delegates to ColumnLayoutManager.
    /// </summary>
    [RelayCommand]
    private void HideEmptyColumns()
    {
        _columnLayoutManager.HideEmptyColumns(_allContracts);
    }

    /// <summary>
    /// Shows all columns.
    /// Delegates to ColumnLayoutManager.
    /// </summary>
    [RelayCommand]
    public async Task ShowAllColumnsAsync()
    {
        Debug.WriteLine("[ContractsViewModel] ========== ShowAllColumnsAsync START ==========");
        Debug.WriteLine($"[ContractsViewModel] Thread: {Thread.CurrentThread.ManagedThreadId}");
        Debug.WriteLine($"[ContractsViewModel] _columnLayoutManager is null: {_columnLayoutManager == null}");

        await _columnLayoutManager.ShowAllColumnsAsync();

        Debug.WriteLine("[ContractsViewModel] ========== ShowAllColumnsAsync END ==========");
    }

    /// <summary>
    /// Loads persisted state from preferences and applies it to the view.
    /// Called after contracts are loaded.
    /// </summary>
    public void LoadPersistedState()
    {
        // Get saved contract ID before applying filters (filters will rebuild Contracts collection)
        var savedContractId = _userPreferencesService.LastSelectedContractId;

        // Restore search text (triggers debounced ApplyFilters)
        var savedSearchText = _userPreferencesService.LastContractSearchText;
        if (!string.IsNullOrWhiteSpace(savedSearchText))
        {
            SearchText = savedSearchText;
        }

        // Restore status filter (triggers ApplyFilters immediately)
        var savedStatusFilter = _userPreferencesService.LastContractStatusFilter ?? "All";
        RestoreStatusFilter(savedStatusFilter);

        // Restore advanced filters (already applied in RestoreAdvancedFilters)
        RestoreAdvancedFilters();

        // Restore selected contract AFTER all filters are applied
        // We need to wait for the UI to update, so use Dispatcher
        Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
        {
            if (savedContractId.HasValue)
            {
                SelectedContract = Contracts.FirstOrDefault(c => c.ContractId == savedContractId.Value);
            }
        }), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    /// <summary>
    /// Restores the status filter state.
    /// </summary>
    private void RestoreStatusFilter(string statusFilter)
    {
        switch (statusFilter)
        {
            case "Past":
                ShowPast = true;
                break;
            case "Active":
                ShowActive = true;
                break;
            case "Future":
                ShowFuture = true;
                break;
            default:
                ShowAll = true;
                break;
        }
    }

    /// <summary>
    /// Restores advanced filters from preferences.
    /// </summary>
    private void RestoreAdvancedFilters()
    {
        var savedFilters = _userPreferencesService.LastContractAdvancedFilters;
        if (savedFilters != null && savedFilters.Any())
        {
            _activeFilters = savedFilters.Select(f => new FilterCriteriaDto
            {
                FieldName = f.FieldName,
                FieldDisplayName = f.FieldDisplayName,
                Operator = f.Operator,
                Value = f.Value
            }).ToList();

            ActiveFilterCount = _activeFilters.Count;
        }
    }

    /// <summary>
    /// Saves advanced filters to preferences.
    /// </summary>
    private void SaveAdvancedFilters()
    {
        if (_activeFilters.Any())
        {
            var filters = _activeFilters.Select(f => new ContractFilterDto
            {
                FieldName = f.FieldName,
                FieldDisplayName = f.FieldDisplayName,
                Operator = f.Operator,
                Value = f.Value
            }).ToList();

            _userPreferencesService.LastContractAdvancedFilters = filters;
        }
        else
        {
            _userPreferencesService.LastContractAdvancedFilters = null;
        }
    }

    /// <summary>
    /// Saves the current column order to preferences.
    /// Called from view when columns are reordered.
    /// Delegates to ColumnLayoutManager.
    /// </summary>
    public void SaveColumnOrder(List<string> columnNames)
    {
        _columnLayoutManager.SaveColumnOrder(columnNames);
    }

    /// <summary>
    /// Gets the saved column order from preferences.
    /// Called from view to restore column order.
    /// Delegates to ColumnLayoutManager.
    /// </summary>
    public List<string>? GetSavedColumnOrder()
    {
        return _columnLayoutManager.GetSavedColumnOrder();
    }

    /// <summary>
    /// Resets the saved column order to default (XAML order).
    /// Called from ColumnPickerDialog.
    /// Delegates to ColumnLayoutManager.
    /// </summary>
    public void ResetColumnOrder()
    {
        _columnLayoutManager.ResetColumnOrder();
    }

    /// <summary>
    /// Saves the current column widths to preferences.
    /// Called from view when column widths are changed.
    /// Delegates to ColumnLayoutManager.
    /// </summary>
    public void SaveColumnWidths(Dictionary<string, double> columnWidths)
    {
        _columnLayoutManager.SaveColumnWidths(columnWidths);
    }

    /// <summary>
    /// Gets the saved column widths from preferences.
    /// Called from view to restore column widths.
    /// Delegates to ColumnLayoutManager.
    /// </summary>
    public Dictionary<string, double>? GetSavedColumnWidths()
    {
        return _columnLayoutManager.GetSavedColumnWidths();
    }

    /// <summary>
    /// Resets the saved column widths to default (XAML widths).
    /// Delegates to ColumnLayoutManager.
    /// </summary>
    public void ResetColumnWidths()
    {
        _columnLayoutManager.ResetColumnWidths();
    }

    /// <summary>
    /// Resets all settings including column order.
    /// Clears search text, status filter, advanced filters, selection, and column order.
    /// </summary>
    [RelayCommand]
    private void ResetSettings()
    {
        // Clear search text
        SearchText = string.Empty;

        // Clear status filter - reset to "All"
        _showPast = false;
        _showActive = false;
        _showFuture = false;
        _showAll = true;
        OnPropertyChanged(nameof(ShowAll));
        OnPropertyChanged(nameof(ShowPast));
        OnPropertyChanged(nameof(ShowActive));
        OnPropertyChanged(nameof(ShowFuture));
        ApplyFilters();

        // Clear advanced filters
        _activeFilters.Clear();
        ActiveFilterCount = 0;

        // Clear selection
        SelectedContract = null;

        // Clear saved preferences
        _userPreferencesService.LastSelectedContractId = null;
        _userPreferencesService.LastContractSearchText = null;
        _userPreferencesService.LastContractStatusFilter = null;
        _userPreferencesService.LastContractAdvancedFilters = null;

        // Reset column order and widths to default
        ResetColumnOrder();
        ResetColumnWidths();

        // Physically reset column DisplayIndex to match default order
        if (ContractsDataGrid != null)
        {
            for (int i = 0; i < ContractsDataGrid.Columns.Count; i++)
            {
                ContractsDataGrid.Columns[i].DisplayIndex = i;
            }
        }
    }

    /// <summary>
    /// Disposes of resources including the debounce timer.
    /// </summary>
    public void Dispose()
    {
        _filterDebounceTimer?.Stop();
    }
}
