using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HelloID.Vault.Core.Models.Entities;
using HelloID.Vault.Data.Repositories.Interfaces;
using HelloID.Vault.Management.Views.ReferenceData;
using HelloID.Vault.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace HelloID.Vault.Management.ViewModels.ReferenceData;

/// <summary>
/// ViewModel for Locations reference data management.
/// </summary>
public partial class LocationsViewModel : ObservableObject
{
    private readonly IReferenceDataService _referenceDataService;
    private readonly IServiceProvider _serviceProvider;
    private readonly ISourceSystemRepository _sourceSystemRepository;
    private readonly IUserPreferencesService _userPreferencesService;
    private List<Location> _allLocations = new();

    [ObservableProperty]
    private ObservableCollection<Location> _locations = new();

    [ObservableProperty]
    private Location? _selectedLocation;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private int _totalCount;

    public LocationsViewModel(
        IReferenceDataService referenceDataService,
        IServiceProvider serviceProvider)
    {
        _referenceDataService = referenceDataService ?? throw new ArgumentNullException(nameof(referenceDataService));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _userPreferencesService = serviceProvider.GetRequiredService<IUserPreferencesService>();
        _sourceSystemRepository = serviceProvider.GetService(typeof(ISourceSystemRepository)) as ISourceSystemRepository ?? throw new InvalidOperationException("ISourceSystemRepository not registered");
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
            _allLocations = (await _referenceDataService.GetLocationsAsync()).ToList();
            ApplyFilter();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading locations: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadDataAsync();
    }

    [RelayCommand]
    private void AddItem()
    {
        var viewModel = new ReferenceDataEditViewModel(_referenceDataService, _sourceSystemRepository, "Locations");
        var window = new ReferenceDataEditWindow();
        window.SetViewModel(viewModel);
        window.Owner = Application.Current.MainWindow;

        if (window.ShowDialog() == true)
        {
            _ = RefreshAsync();
        }
    }

    [RelayCommand]
    private async Task EditItemAsync()
    {
        if (SelectedLocation == null) return;

        var location = await _referenceDataService.GetLocationByIdAsync(SelectedLocation.ExternalId, SelectedLocation.Source);
        if (location == null)
        {
            MessageBox.Show("Location not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var viewModel = new ReferenceDataEditViewModel(
            _referenceDataService,
            _sourceSystemRepository,
            "Locations",
            location.ExternalId,
            location.Code,
            location.Name,
            location.Source);

        var window = new ReferenceDataEditWindow();
        window.SetViewModel(viewModel);
        window.Owner = Application.Current.MainWindow;

        if (window.ShowDialog() == true)
        {
            await RefreshAsync();
        }
    }

    [RelayCommand]
    private async Task DeleteItemAsync()
    {
        if (SelectedLocation == null) return;

        var result = MessageBox.Show(
            $"Are you sure you want to delete location '{SelectedLocation.ExternalId}'?",
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            try
            {
                await _referenceDataService.DeleteLocationAsync(SelectedLocation.ExternalId, SelectedLocation.Source);
                await RefreshAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error deleting location: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    partial void OnSearchTextChanged(string value)
    {
        // Save search text to preferences
        _userPreferencesService.LastLocationSearchText = value;
        ApplyFilter();
    }

    // Save selected location when changed
    partial void OnSelectedLocationChanged(Location? value)
    {
        _userPreferencesService.LastSelectedLocationCode = value?.Code;
    }

    /// <summary>
    /// Loads persisted state from preferences and applies it to the view.
    /// Called after locations are loaded.
    /// </summary>
    public void LoadPersistedState()
    {
        // Get saved location code before applying filter (filter will rebuild Locations collection)
        var savedLocationCode = _userPreferencesService.LastSelectedLocationCode;

        // Restore search text (triggers ApplyFilter)
        var savedSearchText = _userPreferencesService.LastLocationSearchText;
        if (!string.IsNullOrWhiteSpace(savedSearchText))
        {
            SearchText = savedSearchText;
        }

        // Restore selected location AFTER filter is applied
        // We need to wait for the UI to update, so use Dispatcher
        Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
        {
            if (!string.IsNullOrWhiteSpace(savedLocationCode))
            {
                SelectedLocation = Locations.FirstOrDefault(l => l.Code == savedLocationCode);
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
        SelectedLocation = null;

        // Clear saved preferences
        _userPreferencesService.LastSelectedLocationCode = null;
        _userPreferencesService.LastLocationSearchText = null;
    }

    private void ApplyFilter()
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            Locations = new ObservableCollection<Location>(_allLocations);
        }
        else
        {
            var searchTerm = SearchText.Trim();

            // Get all string properties for comprehensive search
            var stringProperties = typeof(Location).GetProperties()
                .Where(p => p.PropertyType == typeof(string) && p.CanRead)
                .ToList();

            var filtered = _allLocations.Where(l =>
                stringProperties.Any(prop =>
                {
                    var value = prop.GetValue(l)?.ToString();
                    return value?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) == true;
                })
            ).ToList();

            Locations = new ObservableCollection<Location>(filtered);
        }

        TotalCount = Locations.Count;
    }

    /// <summary>
    /// Saves the current column order to preferences.
    /// Called from view when columns are reordered.
    /// </summary>
    public void SaveColumnOrder(List<string> columnNames)
    {
        System.Diagnostics.Debug.WriteLine($"[LocationsViewModel] SaveColumnOrder() - Saving {columnNames.Count} columns: [{string.Join(", ", columnNames)}]");
        _userPreferencesService.LocationsColumnOrder = columnNames;
    }

    /// <summary>
    /// Gets the saved column order from preferences.
    /// Called from view to restore column order.
    /// </summary>
    public List<string>? GetSavedColumnOrder()
    {
        var order = _userPreferencesService.LocationsColumnOrder;
        if (order == null)
        {
            System.Diagnostics.Debug.WriteLine("[LocationsViewModel] GetSavedColumnOrder() - Returning null (no saved order)");
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"[LocationsViewModel] GetSavedColumnOrder() - Returning {order.Count} columns: [{string.Join(", ", order)}]");
        }
        return order;
    }

    /// <summary>
    /// Saves the current column widths to preferences.
    /// Called from view when column widths are changed.
    /// </summary>
    public void SaveColumnWidths(Dictionary<string, double> columnWidths)
    {
        System.Diagnostics.Debug.WriteLine($"[LocationsViewModel] SaveColumnWidths() - Saving {columnWidths.Count} column widths");
        _userPreferencesService.LocationsColumnWidths = columnWidths;
    }

    /// <summary>
    /// Gets the saved column widths from preferences.
    /// Called from view to restore column widths.
    /// </summary>
    public Dictionary<string, double>? GetSavedColumnWidths()
    {
        var widths = _userPreferencesService.LocationsColumnWidths;
        if (widths == null)
        {
            System.Diagnostics.Debug.WriteLine("[LocationsViewModel] GetSavedColumnWidths() - Returning null (no saved widths)");
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"[LocationsViewModel] GetSavedColumnWidths() - Returning {widths.Count} column widths");
        }
        return widths;
    }

    /// <summary>
    /// Resets the saved column order to default (XAML order).
    /// </summary>
    [RelayCommand]
    private void ResetColumnOrder()
    {
        System.Diagnostics.Debug.WriteLine("[LocationsViewModel] ResetColumnOrder() - Clearing saved column order");
        _userPreferencesService.LocationsColumnOrder = null;
    }

    /// <summary>
    /// Resets the saved column widths to default (XAML widths).
    /// </summary>
    [RelayCommand]
    private void ResetColumnWidths()
    {
        System.Diagnostics.Debug.WriteLine("[LocationsViewModel] ResetColumnWidths() - Clearing saved column widths");
        _userPreferencesService.LocationsColumnWidths = null;
    }
}
