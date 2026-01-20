using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HelloID.Vault.Core.Models.Entities;
using HelloID.Vault.Management.Views.ReferenceData;
using HelloID.Vault.Services.Interfaces;
using HelloID.Vault.Data.Repositories.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace HelloID.Vault.Management.ViewModels.ReferenceData;

/// <summary>
/// ViewModel for Departments reference data management.
/// Uses specialized DepartmentEditWindow due to unique schema.
/// </summary>
public partial class DepartmentsViewModel : ObservableObject
{
    private readonly IReferenceDataService _referenceDataService;
    private readonly ISourceSystemRepository _sourceSystemRepository;
    private readonly IServiceProvider _serviceProvider;
    private readonly IUserPreferencesService _userPreferencesService;
    private List<Department> _allItems = new();

    [ObservableProperty]
    private ObservableCollection<Department> _departments = new();

    [ObservableProperty]
    private Department? _selectedDepartment;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private int _totalCount;

    public DepartmentsViewModel(IReferenceDataService referenceDataService, IServiceProvider serviceProvider)
    {
        _referenceDataService = referenceDataService ?? throw new ArgumentNullException(nameof(referenceDataService));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _userPreferencesService = serviceProvider.GetRequiredService<IUserPreferencesService>();
        _sourceSystemRepository = serviceProvider.GetService(typeof(ISourceSystemRepository)) as ISourceSystemRepository
            ?? throw new InvalidOperationException("ISourceSystemRepository not registered in service provider");
    }

    public async Task InitializeAsync()
    {
        // Clear any stale cached data in case database was modified externally
        _referenceDataService.ClearCache();
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
            _allItems = (await _referenceDataService.GetDepartmentsAsync()).ToList();
            ApplyFilter();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading departments: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RefreshAsync() => await LoadDataAsync();

    [RelayCommand]
    private void AddItem()
    {
        var viewModel = new DepartmentEditViewModel(_referenceDataService, _sourceSystemRepository);
        var window = new DepartmentEditWindow();
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
        if (SelectedDepartment == null) return;

        var department = await _referenceDataService.GetDepartmentByIdAsync(SelectedDepartment.ExternalId, SelectedDepartment.Source);
        if (department == null)
        {
            MessageBox.Show("Department not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var viewModel = new DepartmentEditViewModel(_referenceDataService, _sourceSystemRepository, department);
        var window = new DepartmentEditWindow();
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
        if (SelectedDepartment == null) return;

        var result = MessageBox.Show(
            $"Are you sure you want to delete department '{SelectedDepartment.DisplayName}'?",
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            try
            {
                await _referenceDataService.DeleteDepartmentAsync(SelectedDepartment.ExternalId, SelectedDepartment.Source);
                await RefreshAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error deleting department: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    partial void OnSearchTextChanged(string value)
    {
        // Save search text to preferences
        _userPreferencesService.LastDepartmentSearchText = value;
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            Departments = new ObservableCollection<Department>(_allItems);
        }
        else
        {
            var searchTerm = SearchText.Trim();
            var stringProperties = typeof(Department).GetProperties()
                .Where(p => p.PropertyType == typeof(string) && p.CanRead);

            Departments = new ObservableCollection<Department>(_allItems.Where(i =>
                stringProperties.Any(prop =>
                {
                    var value = prop.GetValue(i)?.ToString();
                    return value?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) == true;
                })
            ));
        }
        TotalCount = Departments.Count;
    }

    // Save selected department when changed
    partial void OnSelectedDepartmentChanged(Department? value)
    {
        _userPreferencesService.LastSelectedDepartmentCode = value?.Code;
    }

    /// <summary>
    /// Loads persisted state from preferences and applies it to the view.
    /// Called after departments are loaded.
    /// </summary>
    public void LoadPersistedState()
    {
        // Get saved department code before applying filter (filter will rebuild Departments collection)
        var savedDepartmentCode = _userPreferencesService.LastSelectedDepartmentCode;

        // Restore search text (triggers ApplyFilter)
        var savedSearchText = _userPreferencesService.LastDepartmentSearchText;
        if (!string.IsNullOrWhiteSpace(savedSearchText))
        {
            SearchText = savedSearchText;
        }

        // Restore selected department AFTER filter is applied
        // We need to wait for the UI to update, so use Dispatcher
        Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
        {
            if (!string.IsNullOrWhiteSpace(savedDepartmentCode))
            {
                SelectedDepartment = Departments.FirstOrDefault(d => d.Code == savedDepartmentCode);
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
        SelectedDepartment = null;

        // Clear saved preferences
        _userPreferencesService.LastSelectedDepartmentCode = null;
        _userPreferencesService.LastDepartmentSearchText = null;
    }

    /// <summary>
    /// Saves the current column order to preferences.
    /// Called from view when columns are reordered.
    /// </summary>
    public void SaveColumnOrder(List<string> columnNames)
    {
        System.Diagnostics.Debug.WriteLine($"[DepartmentsViewModel] SaveColumnOrder() - Saving {columnNames.Count} columns: [{string.Join(", ", columnNames)}]");
        _userPreferencesService.DepartmentsColumnOrder = columnNames;
    }

    /// <summary>
    /// Gets the saved column order from preferences.
    /// Called from view to restore column order.
    /// </summary>
    public List<string>? GetSavedColumnOrder()
    {
        var order = _userPreferencesService.DepartmentsColumnOrder;
        if (order == null)
        {
            System.Diagnostics.Debug.WriteLine("[DepartmentsViewModel] GetSavedColumnOrder() - Returning null (no saved order)");
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"[DepartmentsViewModel] GetSavedColumnOrder() - Returning {order.Count} columns: [{string.Join(", ", order)}]");
        }
        return order;
    }

    /// <summary>
    /// Saves the current column widths to preferences.
    /// Called from view when column widths are changed.
    /// </summary>
    public void SaveColumnWidths(Dictionary<string, double> columnWidths)
    {
        System.Diagnostics.Debug.WriteLine($"[DepartmentsViewModel] SaveColumnWidths() - Saving {columnWidths.Count} column widths");
        _userPreferencesService.DepartmentsColumnWidths = columnWidths;
    }

    /// <summary>
    /// Gets the saved column widths from preferences.
    /// Called from view to restore column widths.
    /// </summary>
    public Dictionary<string, double>? GetSavedColumnWidths()
    {
        var widths = _userPreferencesService.DepartmentsColumnWidths;
        if (widths == null)
        {
            System.Diagnostics.Debug.WriteLine("[DepartmentsViewModel] GetSavedColumnWidths() - Returning null (no saved widths)");
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"[DepartmentsViewModel] GetSavedColumnWidths() - Returning {widths.Count} column widths");
        }
        return widths;
    }

    /// <summary>
    /// Resets the saved column order to default (XAML order).
    /// </summary>
    [RelayCommand]
    private void ResetColumnOrder()
    {
        System.Diagnostics.Debug.WriteLine("[DepartmentsViewModel] ResetColumnOrder() - Clearing saved column order");
        _userPreferencesService.DepartmentsColumnOrder = null;
    }

    /// <summary>
    /// Resets the saved column widths to default (XAML widths).
    /// </summary>
    [RelayCommand]
    private void ResetColumnWidths()
    {
        System.Diagnostics.Debug.WriteLine("[DepartmentsViewModel] ResetColumnWidths() - Clearing saved column widths");
        _userPreferencesService.DepartmentsColumnWidths = null;
    }
}
