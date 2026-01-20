using System.Collections.ObjectModel;
using System.Timers;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HelloID.Vault.Core.Models.DTOs;
using HelloID.Vault.Core.Models.Filters;
using HelloID.Vault.Services.Interfaces;
using HelloID.Vault.Data.Repositories.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace HelloID.Vault.Management.ViewModels.Persons;

/// <summary>
/// Master-detail coordinator ViewModel for Persons module.
/// Manages the person list with virtualized scrolling, search, and selected person detail.
/// </summary>
public partial class PersonsViewModel : ObservableObject
{
    private readonly IPersonService _personService;
    private readonly ICustomFieldRepository _customFieldRepository;
    private readonly IUserPreferencesService _userPreferencesService;
    private readonly System.Timers.Timer _searchDebounceTimer;
    private const int BatchSize = 200;
    private const int SearchDebounceMs = 300;
    private int _currentOffset = 0;
    private bool _hasMoreData = true;

    [ObservableProperty]
    private ObservableCollection<PersonListDto> _persons = new();

    [ObservableProperty]
    private PersonListDto? _selectedPerson;

    [ObservableProperty]
    private PersonDetailViewModel? _detailViewModel;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    private bool _showAll = true;
    private bool _showPast;
    private bool _showActive;
    private bool _showFuture;
    private bool _isRestoringSelection;
    private bool _isInitializing;
    private bool _willRestoreSelection;

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
                _ = LoadPersonsAsync();
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
                _ = LoadPersonsAsync();
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
                _ = LoadPersonsAsync();
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
                _ = LoadPersonsAsync();
            }
        }
    }

    [ObservableProperty]
    private int _totalCount;

    private readonly IServiceProvider _serviceProvider;

    public PersonsViewModel(IPersonService personService, ICustomFieldRepository customFieldRepository, IUserPreferencesService userPreferencesService, IServiceProvider serviceProvider)
    {
        _personService = personService ?? throw new ArgumentNullException(nameof(personService));
        _customFieldRepository = customFieldRepository ?? throw new ArgumentNullException(nameof(customFieldRepository));
        _userPreferencesService = userPreferencesService ?? throw new ArgumentNullException(nameof(userPreferencesService));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

        // Initialize search debounce timer
        _searchDebounceTimer = new System.Timers.Timer(SearchDebounceMs);
        _searchDebounceTimer.AutoReset = false;
        _searchDebounceTimer.Elapsed += OnSearchDebounceTimerElapsed;
    }

    /// <summary>
    /// Loads the initial data when the view is activated.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_isInitializing)
        {
            System.Diagnostics.Debug.WriteLine($"[PersonsViewModel] InitializeAsync already in progress, skipping");
            return;
        }

        System.Diagnostics.Debug.WriteLine($"[PersonsViewModel] ===== InitializeAsync START =====");
        System.Diagnostics.Debug.WriteLine($"[PersonsViewModel] Saved search text: '{_userPreferencesService.LastPersonSearchText}'");
        System.Diagnostics.Debug.WriteLine($"[PersonsViewModel] Saved person ID: '{_userPreferencesService.LastSelectedPersonId}'");
        System.Diagnostics.Debug.WriteLine($"[PersonsViewModel] Current SearchText: '{SearchText}'");
        System.Diagnostics.Debug.WriteLine($"[PersonsViewModel] Current SelectedPerson: {SelectedPerson?.DisplayName ?? "null"}");

        _isInitializing = true;

        try
        {
            // Restore the saved search text
            var savedSearchText = _userPreferencesService.LastPersonSearchText;
            if (!string.IsNullOrEmpty(savedSearchText))
            {
                System.Diagnostics.Debug.WriteLine($"[PersonsViewModel] Restoring search text: '{savedSearchText}'");
                SearchText = savedSearchText;
            }

            await LoadPersonsAsync();

            System.Diagnostics.Debug.WriteLine($"[PersonsViewModel] LoadPersonsAsync completed. Final SelectedPerson: {SelectedPerson?.DisplayName ?? "null"}");
        }
        finally
        {
            _isInitializing = false;
            System.Diagnostics.Debug.WriteLine($"[PersonsViewModel] ===== InitializeAsync END =====");
        }
    }

    /// <summary>
    /// Loads persons based on current filter settings.
    /// Clears existing data and loads first batch.
    /// </summary>
    [RelayCommand]
    private async Task LoadPersonsAsync()
    {
        System.Diagnostics.Debug.WriteLine($"[PersonsViewModel] ----- LoadPersonsAsync START -----");
        System.Diagnostics.Debug.WriteLine($"[PersonsViewModel] IsBusy: {IsBusy}, _isRestoringSelection: {_isRestoringSelection}");

        if (IsBusy)
        {
            System.Diagnostics.Debug.WriteLine($"[PersonsViewModel] Already busy, returning");
            return;
        }

        // Determine which person ID we need to find BEFORE clearing the collection
        string? targetPersonId = null;

        // Save the current selection before clearing (unless we're restoring)
        if (!_isRestoringSelection && SelectedPerson != null)
        {
            targetPersonId = SelectedPerson.PersonId;
            System.Diagnostics.Debug.WriteLine($"[PersonsViewModel] Target from current selection: '{targetPersonId}'");
        }

        // Also check user preferences if no current selection
        if (string.IsNullOrEmpty(targetPersonId) && !_isRestoringSelection)
        {
            targetPersonId = _userPreferencesService.LastSelectedPersonId;
            System.Diagnostics.Debug.WriteLine($"[PersonsViewModel] Target from preferences: '{targetPersonId}'");
        }

        System.Diagnostics.Debug.WriteLine($"[PersonsViewModel] Final targetPersonId: '{targetPersonId}'");

        // Set flag to prevent intermediate null selection from corrupting preferences
        _willRestoreSelection = !string.IsNullOrEmpty(targetPersonId);
        System.Diagnostics.Debug.WriteLine($"[PersonsViewModel] _willRestoreSelection: {_willRestoreSelection}");

        try
        {
            IsBusy = true;

            // Reset state
            Persons.Clear();
            _currentOffset = 0;
            _hasMoreData = true;

            // If we have a target person to restore, load as many batches as needed to find them
            if (!string.IsNullOrEmpty(targetPersonId))
            {
                System.Diagnostics.Debug.WriteLine($"[PersonsViewModel] Loading batches to find person '{targetPersonId}'");
                PersonListDto? targetPerson = null;
                int batchCount = 0;

                // Keep loading batches until we find the person or run out of results
                while (_hasMoreData && targetPerson == null)
                {
                    batchCount++;
                    System.Diagnostics.Debug.WriteLine($"[PersonsViewModel] Loading batch {batchCount}");
                    await LoadNextBatchAsync();
                    targetPerson = Persons.FirstOrDefault(p => p.PersonId == targetPersonId);
                    System.Diagnostics.Debug.WriteLine($"[PersonsViewModel] Batch {batchCount} loaded. Items in collection: {Persons.Count}. Target found: {targetPerson != null}");
                }

                System.Diagnostics.Debug.WriteLine($"[PersonsViewModel] Loaded {batchCount} batches. _hasMoreData: {_hasMoreData}");

                // Select the person if found
                if (targetPerson != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[PersonsViewModel] FOUND target person: {targetPerson.DisplayName}. Setting SelectedPerson.");
                    _isRestoringSelection = true;
                    try
                    {
                        SelectedPerson = targetPerson;
                        System.Diagnostics.Debug.WriteLine($"[PersonsViewModel] SelectedPerson set to: {SelectedPerson?.DisplayName ?? "null"}");
                    }
                    finally
                    {
                        _isRestoringSelection = false;
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[PersonsViewModel] Target person NOT FOUND in loaded results");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[PersonsViewModel] No target person, loading first batch only");
                // No target person, just load first batch
                await LoadNextBatchAsync();
            }
        }
        catch (Exception ex)
        {
            // TODO: Add error handling/logging
            System.Diagnostics.Debug.WriteLine($"Error loading persons: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
            _willRestoreSelection = false;
            System.Diagnostics.Debug.WriteLine($"[PersonsViewModel] ----- LoadPersonsAsync END -----");
        }
    }

    /// <summary>
    /// Loads the next batch of records incrementally.
    /// </summary>
    public async Task LoadMoreAsync()
    {
        if (IsBusy || !_hasMoreData) return;

        await LoadNextBatchAsync();
    }

    /// <summary>
    /// Internal method to load the next batch of data.
    /// </summary>
    private async Task LoadNextBatchAsync()
    {
        try
        {
            var filter = new PersonFilter
            {
                SearchTerm = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText,
                PersonStatus = ShowAll ? null : (ShowPast ? "Past" : (ShowActive ? "Active" : (ShowFuture ? "Future" : null)))
            };

            System.Diagnostics.Debug.WriteLine($"[PersonsViewModel] LoadNextBatchAsync: SearchText='{SearchText}', SearchTerm='{filter.SearchTerm}', PersonStatus='{filter.PersonStatus}'");

            // Calculate page number from offset
            int pageNumber = (_currentOffset / BatchSize) + 1;

            System.Diagnostics.Debug.WriteLine($"[PersonsViewModel] Calling GetPagedAsync with page={pageNumber}, pageSize={BatchSize}");

            var (items, totalCount) = await _personService.GetPagedAsync(filter, pageNumber, BatchSize);

            System.Diagnostics.Debug.WriteLine($"[PersonsViewModel] GetPagedAsync returned {items.Count()} items, totalCount={totalCount}");

            // Update total count
            TotalCount = totalCount;

            // Add new items to collection and count them
            int itemsAdded = 0;
            foreach (var item in items)
            {
                Persons.Add(item);
                itemsAdded++;
            }

            System.Diagnostics.Debug.WriteLine($"[PersonsViewModel] Added {itemsAdded} items to collection, new total={Persons.Count}");

            // Update offset and check if more data available
            _currentOffset += itemsAdded;
            _hasMoreData = _currentOffset < totalCount;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading batch: {ex.Message}");
        }
    }

    /// <summary>
    /// Performs a search based on the current search text.
    /// Resets to beginning when searching.
    /// </summary>
    [RelayCommand]
    private async Task SearchAsync()
    {
        await LoadPersonsAsync();
    }

    /// <summary>
    /// Refreshes the current view by reloading data.
    /// </summary>
    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadPersonsAsync();
    }

    /// <summary>
    /// Called when the selected person changes.
    /// Loads the detail view for the selected person.
    /// </summary>
    partial void OnSelectedPersonChanged(PersonListDto? value)
    {
        System.Diagnostics.Debug.WriteLine($"[PersonsViewModel] *** OnSelectedPersonChanged ***");
        System.Diagnostics.Debug.WriteLine($"[PersonsViewModel] New value: {value?.DisplayName ?? "null"}");
        System.Diagnostics.Debug.WriteLine($"[PersonsViewModel] _isRestoringSelection: {_isRestoringSelection}, _willRestoreSelection: {_willRestoreSelection}");

        // Save the selection to user preferences (unless we're restoring or will restore)
        if (!_isRestoringSelection && !_willRestoreSelection)
        {
            System.Diagnostics.Debug.WriteLine($"[PersonsViewModel] Saving selection to preferences: '{value?.PersonId}'");
            _userPreferencesService.LastSelectedPersonId = value?.PersonId;
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"[PersonsViewModel] Skipping save (restoring: {_isRestoringSelection}, will restore: {_willRestoreSelection})");
        }

        if (value == null)
        {
            DetailViewModel = null;
            return;
        }

        // Load detail view for selected person
        var userPreferencesService = _serviceProvider.GetRequiredService<IUserPreferencesService>();
        DetailViewModel = new PersonDetailViewModel(_personService, _customFieldRepository, _serviceProvider, userPreferencesService, value.PersonId);

        // Subscribe to person changes to refresh the list
        DetailViewModel.PersonChangedEvent += async () =>
        {
            await LoadPersonsAsync();
        };

        _ = DetailViewModel.LoadAsync();
    }

    /// <summary>
    /// Called when search text changes.
    /// Debounces the search to avoid excessive queries while typing.
    /// </summary>
    partial void OnSearchTextChanged(string value)
    {
        System.Diagnostics.Debug.WriteLine($"[PersonsViewModel] OnSearchTextChanged called with value: '{value}'");

        // Save search text to preferences
        _userPreferencesService.LastPersonSearchText = value;

        // Skip debounce timer during initialization to prevent duplicate LoadPersonsAsync calls
        if (!_isInitializing)
        {
            // Reset and restart the debounce timer
            _searchDebounceTimer.Stop();
            _searchDebounceTimer.Start();

            System.Diagnostics.Debug.WriteLine($"[PersonsViewModel] Debounce timer started");
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"[PersonsViewModel] Initializing, skipping debounce timer");
        }
    }

    /// <summary>
    /// Called when the search debounce timer elapses.
    /// Triggers the actual search on the UI thread.
    /// </summary>
    private void OnSearchDebounceTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"[PersonsViewModel] OnSearchDebounceTimerElapsed called");

        // Dispatch to UI thread since timer runs on background thread
        Application.Current?.Dispatcher.InvokeAsync(async () =>
        {
            System.Diagnostics.Debug.WriteLine($"[PersonsViewModel] Dispatcher.InvokeAsync executing, calling LoadPersonsAsync");
            await LoadPersonsAsync();
        });
    }

    // Button commands for status filtering
    [RelayCommand]
    private void ShowAllPersons()
    {
        ShowAll = true;
    }

    [RelayCommand]
    private void ShowPastPersons()
    {
        ShowPast = true;
    }

    [RelayCommand]
    private void ShowActivePersons()
    {
        ShowActive = true;
    }

    [RelayCommand]
    private void ShowFuturePersons()
    {
        ShowFuture = true;
    }
}
