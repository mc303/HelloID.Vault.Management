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

public partial class DivisionsViewModel : ObservableObject
{
    private readonly IReferenceDataService _referenceDataService;
    private readonly IServiceProvider _serviceProvider;
    private readonly IUserPreferencesService _userPreferencesService;
    private readonly ISourceSystemRepository _sourceSystemRepository;
    private List<Division> _allItems = new();

    [ObservableProperty] private ObservableCollection<Division> _divisions = new();
    [ObservableProperty] private Division? _selectedDivision;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private int _totalCount;

    public DivisionsViewModel(IReferenceDataService referenceDataService, IServiceProvider serviceProvider)
    {
        _referenceDataService = referenceDataService;
        _serviceProvider = serviceProvider;
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
        try { IsBusy = true; _allItems = (await _referenceDataService.GetDivisionsAsync()).ToList(); ApplyFilter(); }
        catch (Exception ex) { MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
        finally { IsBusy = false; }
    }

    [RelayCommand] private async Task RefreshAsync() => await LoadDataAsync();
    [RelayCommand] private void AddItem() { var vm = new ReferenceDataEditViewModel(_referenceDataService, _sourceSystemRepository, "Divisions"); var w = new ReferenceDataEditWindow(); w.SetViewModel(vm); w.Owner = Application.Current.MainWindow; if (w.ShowDialog() == true) _ = RefreshAsync(); }

    [RelayCommand]
    private async Task EditItemAsync()
    {
        if (SelectedDivision == null) return;
        var item = await _referenceDataService.GetDivisionByIdAsync(SelectedDivision.ExternalId, SelectedDivision.Source);
        if (item == null) return;
        var vm = new ReferenceDataEditViewModel(_referenceDataService, _sourceSystemRepository, "Divisions", item.ExternalId, item.Code, item.Name, item.Source);
        var w = new ReferenceDataEditWindow(); w.SetViewModel(vm); w.Owner = Application.Current.MainWindow;
        if (w.ShowDialog() == true) await RefreshAsync();
    }

    [RelayCommand]
    private async Task DeleteItemAsync()
    {
        if (SelectedDivision == null) return;
        if (MessageBox.Show($"Delete '{SelectedDivision.ExternalId}'?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
        {
            try { await _referenceDataService.DeleteDivisionAsync(SelectedDivision.ExternalId, SelectedDivision.Source); await RefreshAsync(); }
            catch (Exception ex) { MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
        }
    }

    partial void OnSelectedDivisionChanged(Division? value)
    {
        if (value != null)
        {
            _userPreferencesService.LastSelectedDivisionCode = value.Code;
        }
    }

    partial void OnSearchTextChanged(string value)
    {
        _userPreferencesService.LastDivisionSearchText = value;
        ApplyFilter();
    }

    private void LoadPersistedState()
    {
        var lastCode = _userPreferencesService.LastSelectedDivisionCode;
        if (!string.IsNullOrWhiteSpace(lastCode))
        {
            SelectedDivision = Divisions.FirstOrDefault(d => d.Code == lastCode);
        }

        if (!string.IsNullOrWhiteSpace(_userPreferencesService.LastDivisionSearchText))
        {
            SearchText = _userPreferencesService.LastDivisionSearchText;
        }
    }

    [RelayCommand]
    private void ResetSettings()
    {
        SelectedDivision = null;
        SearchText = string.Empty;
        _userPreferencesService.LastSelectedDivisionCode = null;
        _userPreferencesService.LastDivisionSearchText = string.Empty;
    }

    private void ApplyFilter()
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            Divisions = new ObservableCollection<Division>(_allItems);
        }
        else
        {
            var searchTerm = SearchText.Trim();
            var stringProperties = typeof(Division).GetProperties()
                .Where(p => p.PropertyType == typeof(string) && p.CanRead);

            Divisions = new ObservableCollection<Division>(_allItems.Where(i =>
                stringProperties.Any(prop =>
                {
                    var value = prop.GetValue(i)?.ToString();
                    return value?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) == true;
                })
            ));
        }
        TotalCount = Divisions.Count;
    }

    /// <summary>
    /// Saves the current column order to preferences.
    /// Called from view when columns are reordered.
    /// </summary>
    public void SaveColumnOrder(List<string> columnNames)
    {
        System.Diagnostics.Debug.WriteLine($"[DivisionsViewModel] SaveColumnOrder() - Saving {columnNames.Count} columns: [{string.Join(", ", columnNames)}]");
        _userPreferencesService.DivisionsColumnOrder = columnNames;
    }

    /// <summary>
    /// Gets the saved column order from preferences.
    /// Called from view to restore column order.
    /// </summary>
    public List<string>? GetSavedColumnOrder()
    {
        var order = _userPreferencesService.DivisionsColumnOrder;
        if (order == null)
        {
            System.Diagnostics.Debug.WriteLine("[DivisionsViewModel] GetSavedColumnOrder() - Returning null (no saved order)");
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"[DivisionsViewModel] GetSavedColumnOrder() - Returning {order.Count} columns: [{string.Join(", ", order)}]");
        }
        return order;
    }

    /// <summary>
    /// Saves the current column widths to preferences.
    /// Called from view when column widths are changed.
    /// </summary>
    public void SaveColumnWidths(Dictionary<string, double> columnWidths)
    {
        System.Diagnostics.Debug.WriteLine($"[DivisionsViewModel] SaveColumnWidths() - Saving {columnWidths.Count} column widths");
        _userPreferencesService.DivisionsColumnWidths = columnWidths;
    }

    /// <summary>
    /// Gets the saved column widths from preferences.
    /// Called from view to restore column widths.
    /// </summary>
    public Dictionary<string, double>? GetSavedColumnWidths()
    {
        var widths = _userPreferencesService.DivisionsColumnWidths;
        if (widths == null)
        {
            System.Diagnostics.Debug.WriteLine("[DivisionsViewModel] GetSavedColumnWidths() - Returning null (no saved widths)");
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"[DivisionsViewModel] GetSavedColumnWidths() - Returning {widths.Count} column widths");
        }
        return widths;
    }

    /// <summary>
    /// Resets the saved column order to default (XAML order).
    /// </summary>
    [RelayCommand]
    private void ResetColumnOrder()
    {
        System.Diagnostics.Debug.WriteLine("[DivisionsViewModel] ResetColumnOrder() - Clearing saved column order");
        _userPreferencesService.DivisionsColumnOrder = null;
    }

    /// <summary>
    /// Resets the saved column widths to default (XAML widths).
    /// </summary>
    [RelayCommand]
    private void ResetColumnWidths()
    {
        System.Diagnostics.Debug.WriteLine("[DivisionsViewModel] ResetColumnWidths() - Clearing saved column widths");
        _userPreferencesService.DivisionsColumnWidths = null;
    }
}
