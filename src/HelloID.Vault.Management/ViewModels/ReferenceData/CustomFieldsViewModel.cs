using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HelloID.Vault.Core.Models.Entities;
using HelloID.Vault.Management.Views.ReferenceData;
using HelloID.Vault.Data.Repositories.Interfaces;
using HelloID.Vault.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace HelloID.Vault.Management.ViewModels.ReferenceData;

/// <summary>
/// ViewModel for Custom Fields administration.
/// </summary>
public partial class CustomFieldsViewModel : ObservableObject
{
    private readonly ICustomFieldRepository _customFieldRepository;
    private readonly IServiceProvider _serviceProvider;
    private readonly IUserPreferencesService _userPreferencesService;
    private readonly IDialogService _dialogService;
    private List<CustomFieldSchema> _allCustomFields = new();

    [ObservableProperty]
    private ObservableCollection<CustomFieldSchema> _customFields = new();

    [ObservableProperty]
    private CustomFieldSchema? _selectedCustomField;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _showAll = true;

    [ObservableProperty]
    private bool _showPersons;

    [ObservableProperty]
    private bool _showContracts;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private int _totalCount;

    [ObservableProperty]
    private int _personFieldsCount;

    [ObservableProperty]
    private int _contractFieldsCount;

    public CustomFieldsViewModel(
        ICustomFieldRepository customFieldRepository,
        IServiceProvider serviceProvider)
    {
        _customFieldRepository = customFieldRepository ?? throw new ArgumentNullException(nameof(customFieldRepository));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
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

            // Load schemas for both tables
            var personSchemas = await _customFieldRepository.GetSchemasAsync("persons");
            var contractSchemas = await _customFieldRepository.GetSchemasAsync("contracts");

            _allCustomFields = personSchemas.Concat(contractSchemas)
                .OrderBy(s => s.TableName)
                .ThenBy(s => s.SortOrder)
                .ToList();

            PersonFieldsCount = personSchemas.Count();
            ContractFieldsCount = contractSchemas.Count();

            ApplyFilter();
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"Error loading custom fields: {ex.Message}", "Error");
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
    private void ShowAllFields()
    {
        ShowAll = true;
        ShowPersons = false;
        ShowContracts = false;
    }

    [RelayCommand]
    private void ShowPersonFields()
    {
        ShowAll = false;
        ShowPersons = true;
        ShowContracts = false;
    }

    [RelayCommand]
    private void ShowContractFields()
    {
        ShowAll = false;
        ShowPersons = false;
        ShowContracts = true;
    }

    [RelayCommand]
    private void AddItem()
    {
        var viewModel = _serviceProvider.GetRequiredService<CustomFieldEditViewModel>();
        var window = new CustomFieldEditWindow();
        window.SetViewModel(viewModel);
        window.Owner = Application.Current.MainWindow;

        if (window.ShowDialog() == true)
        {
            _ = RefreshAsync();
        }
    }

    [RelayCommand]
    private void EditItem()
    {
        if (SelectedCustomField == null) return;

        var viewModel = _serviceProvider.GetRequiredService<CustomFieldEditViewModel>();
        viewModel.LoadExistingField(SelectedCustomField);

        var window = new CustomFieldEditWindow();
        window.SetViewModel(viewModel);
        window.Owner = Application.Current.MainWindow;

        if (window.ShowDialog() == true)
        {
            _ = RefreshAsync();
        }
    }

    [RelayCommand]
    private async Task DeleteItemAsync()
    {
        if (SelectedCustomField == null) return;

        var result = _dialogService.ShowConfirm(
            $"Are you sure you want to delete custom field '{SelectedCustomField.DisplayName}' ({SelectedCustomField.FieldKey})?\n\nThis will not delete existing field values, but the field will no longer appear in edit forms.",
            "Confirm Delete");

        if (result)
        {
            try
            {
                await _customFieldRepository.DeleteSchemaAsync(SelectedCustomField.FieldKey);
                await RefreshAsync();
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"Error deleting custom field: {ex.Message}", "Error");
            }
        }
    }

    partial void OnSearchTextChanged(string value)
    {
        // Save search text to preferences
        _userPreferencesService.LastCustomFieldSearchText = value;
        ApplyFilter();
    }

    // Save selected custom field when changed
    partial void OnSelectedCustomFieldChanged(CustomFieldSchema? value)
    {
        _userPreferencesService.LastSelectedCustomFieldKey = value?.FieldKey;
    }

    /// <summary>
    /// Loads persisted state from preferences and applies it to the view.
    /// Called after custom fields are loaded.
    /// </summary>
    public void LoadPersistedState()
    {
        // Get saved custom field key before applying filter (filter will rebuild CustomFields collection)
        var savedFieldKey = _userPreferencesService.LastSelectedCustomFieldKey;

        // Restore search text (triggers ApplyFilter via OnSearchTextChanged)
        var savedSearchText = _userPreferencesService.LastCustomFieldSearchText;
        if (!string.IsNullOrWhiteSpace(savedSearchText))
        {
            SearchText = savedSearchText;
        }

        // Restore selected custom field AFTER filter is applied
        // We need to wait for the UI to update, so use Dispatcher
        Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
        {
            if (!string.IsNullOrWhiteSpace(savedFieldKey))
            {
                SelectedCustomField = CustomFields.FirstOrDefault(f => f.FieldKey == savedFieldKey);
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
        SelectedCustomField = null;

        // Clear saved preferences
        _userPreferencesService.LastSelectedCustomFieldKey = null;
        _userPreferencesService.LastCustomFieldSearchText = null;
    }

    partial void OnShowAllChanged(bool value) => ApplyFilter();

    partial void OnShowPersonsChanged(bool value) => ApplyFilter();

    partial void OnShowContractsChanged(bool value) => ApplyFilter();

    private void ApplyFilter()
    {
        var filtered = _allCustomFields.AsEnumerable();

        // Apply table filter
        if (ShowPersons)
        {
            filtered = filtered.Where(f => f.TableName.Equals("persons", StringComparison.OrdinalIgnoreCase));
        }
        else if (ShowContracts)
        {
            filtered = filtered.Where(f => f.TableName.Equals("contracts", StringComparison.OrdinalIgnoreCase));
        }

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var searchTerm = SearchText.Trim();
            var stringProperties = typeof(CustomFieldSchema).GetProperties()
                .Where(p => p.PropertyType == typeof(string) && p.CanRead)
                .ToList();

            filtered = filtered.Where(f =>
                stringProperties.Any(prop =>
                {
                    var value = prop.GetValue(f)?.ToString();
                    return value?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) == true;
                })
            );
        }

        CustomFields = new ObservableCollection<CustomFieldSchema>(filtered);
        TotalCount = CustomFields.Count;
    }

    /// <summary>
    /// Saves the current column order to preferences.
    /// Called from view when columns are reordered.
    /// </summary>
    public void SaveColumnOrder(List<string> columnNames)
    {
        _userPreferencesService.CustomFieldsColumnOrder = columnNames;
    }

    /// <summary>
    /// Gets the saved column order from preferences.
    /// Called from view to restore column order.
    /// </summary>
    public List<string>? GetSavedColumnOrder()
    {
        var order = _userPreferencesService.CustomFieldsColumnOrder;
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
        _userPreferencesService.CustomFieldsColumnWidths = columnWidths;
    }

    /// <summary>
    /// Gets the saved column widths from preferences.
    /// Called from view to restore column widths.
    /// </summary>
    public Dictionary<string, double>? GetSavedColumnWidths()
    {
        var widths = _userPreferencesService.CustomFieldsColumnWidths;
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
        _userPreferencesService.CustomFieldsColumnOrder = null;
    }

    /// <summary>
    /// Resets the saved column widths to default (XAML widths).
    /// </summary>
    [RelayCommand]
    private void ResetColumnWidths()
    {
        _userPreferencesService.CustomFieldsColumnWidths = null;
    }
}
