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

public partial class CostBearersViewModel : ObservableObject
{
    private readonly IReferenceDataService _referenceDataService;
    private readonly IServiceProvider _serviceProvider;
    private readonly ISourceSystemRepository _sourceSystemRepository;
    private readonly IUserPreferencesService _userPreferencesService;
    private readonly IDialogService _dialogService;
    private List<CostBearer> _allItems = new();

    [ObservableProperty] private ObservableCollection<CostBearer> _costBearers = new();
    [ObservableProperty] private CostBearer? _selectedCostBearer;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private int _totalCount;

    public CostBearersViewModel(IReferenceDataService referenceDataService, IServiceProvider serviceProvider)
    {
        _referenceDataService = referenceDataService;
        _serviceProvider = serviceProvider;
        _userPreferencesService = serviceProvider.GetRequiredService<IUserPreferencesService>();
        _dialogService = serviceProvider.GetRequiredService<IDialogService>();
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
        try { IsBusy = true; _allItems = (await _referenceDataService.GetCostBearersAsync()).ToList(); ApplyFilter(); }
        catch (Exception ex) { _dialogService.ShowError($"Error: {ex.Message}", "Error"); }
        finally { IsBusy = false; }
    }

    [RelayCommand] private async Task RefreshAsync() => await LoadDataAsync();
    [RelayCommand] private void AddItem() { var vm = new ReferenceDataEditViewModel(_referenceDataService, _sourceSystemRepository, "CostBearers"); var w = new ReferenceDataEditWindow(); w.SetViewModel(vm); w.Owner = Application.Current.MainWindow; if (w.ShowDialog() == true) _ = RefreshAsync(); }

    [RelayCommand]
    private async Task EditItemAsync()
    {
        if (SelectedCostBearer == null) return;
        if (SelectedCostBearer.Source == null)
        {
            _dialogService.ShowError("Cost Bearer has no source system.", "Error");
            return;
        }
        var item = await _referenceDataService.GetCostBearerByIdAsync(SelectedCostBearer.ExternalId, SelectedCostBearer.Source);
        if (item == null) return;
        var vm = new ReferenceDataEditViewModel(_referenceDataService, _sourceSystemRepository, "CostBearers", item.ExternalId, item.Code, item.Name, item.Source);
        var w = new ReferenceDataEditWindow(); w.SetViewModel(vm); w.Owner = Application.Current.MainWindow;
        if (w.ShowDialog() == true) await RefreshAsync();
    }

    [RelayCommand]
    private async Task DeleteItemAsync()
    {
        if (SelectedCostBearer == null) return;
        if (SelectedCostBearer.Source == null)
        {
            _dialogService.ShowError("Cost Bearer has no source system.", "Error");
            return;
        }
        if (_dialogService.ShowConfirm($"Delete '{SelectedCostBearer.ExternalId}'?", "Confirm"))
        {
            try { await _referenceDataService.DeleteCostBearerAsync(SelectedCostBearer.ExternalId, SelectedCostBearer.Source); await RefreshAsync(); }
            catch (Exception ex) { _dialogService.ShowError($"Error: {ex.Message}", "Error"); }
        }
    }

    partial void OnSearchTextChanged(string value)
    {
        // Save search text to preferences
        _userPreferencesService.LastCostBearerSearchText = value;
        ApplyFilter();
    }

    // Save selected cost bearer when changed
    partial void OnSelectedCostBearerChanged(CostBearer? value)
    {
        _userPreferencesService.LastSelectedCostBearerCode = value?.Code;
    }

    /// <summary>
    /// Loads persisted state from preferences and applies it to the view.
    /// </summary>
    public void LoadPersistedState()
    {
        var savedCode = _userPreferencesService.LastSelectedCostBearerCode;
        var savedSearchText = _userPreferencesService.LastCostBearerSearchText;
        if (!string.IsNullOrWhiteSpace(savedSearchText))
        {
            SearchText = savedSearchText;
        }
        Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
        {
            if (!string.IsNullOrWhiteSpace(savedCode))
            {
                SelectedCostBearer = CostBearers.FirstOrDefault(c => c.Code == savedCode);
            }
        }), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    /// <summary>
    /// Resets all settings. Clears search text and selection.
    /// </summary>
    [RelayCommand]
    private void ResetSettings()
    {
        SearchText = string.Empty;
        SelectedCostBearer = null;
        _userPreferencesService.LastSelectedCostBearerCode = null;
        _userPreferencesService.LastCostBearerSearchText = null;
    }

    private void ApplyFilter()
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            CostBearers = new ObservableCollection<CostBearer>(_allItems);
        }
        else
        {
            var searchTerm = SearchText.Trim();
            var stringProperties = typeof(CostBearer).GetProperties()
                .Where(p => p.PropertyType == typeof(string) && p.CanRead);

            CostBearers = new ObservableCollection<CostBearer>(_allItems.Where(i =>
                stringProperties.Any(prop =>
                {
                    var value = prop.GetValue(i)?.ToString();
                    return value?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) == true;
                })
            ));
        }
        TotalCount = CostBearers.Count;
    }

    /// <summary>
    /// Saves the current column order to preferences.
    /// Called from view when columns are reordered.
    /// </summary>
    public void SaveColumnOrder(List<string> columnNames)
    {
        _userPreferencesService.CostBearersColumnOrder = columnNames;
    }

    /// <summary>
    /// Gets the saved column order from preferences.
    /// Called from view to restore column order.
    /// </summary>
    public List<string>? GetSavedColumnOrder()
    {
        var order = _userPreferencesService.CostBearersColumnOrder;
        return order;
    }

    /// <summary>
    /// Saves the current column widths to preferences.
    /// Called from view when column widths are changed.
    /// </summary>
    public void SaveColumnWidths(Dictionary<string, double> columnWidths)
    {
        _userPreferencesService.CostBearersColumnWidths = columnWidths;
    }

    /// <summary>
    /// Gets the saved column widths from preferences.
    /// Called from view to restore column widths.
    /// </summary>
    public Dictionary<string, double>? GetSavedColumnWidths()
    {
        var widths = _userPreferencesService.CostBearersColumnWidths;
        return widths;
    }

    /// <summary>
    /// Resets the saved column order to default (XAML order).
    /// </summary>
    [RelayCommand]
    private void ResetColumnOrder()
    {
        _userPreferencesService.CostBearersColumnOrder = null;
    }

    /// <summary>
    /// Resets the saved column widths to default (XAML widths).
    /// </summary>
    [RelayCommand]
    private void ResetColumnWidths()
    {
        _userPreferencesService.CostBearersColumnWidths = null;
    }
}
