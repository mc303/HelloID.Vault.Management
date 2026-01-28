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

public partial class CostCentersViewModel : ObservableObject
{
    private readonly IReferenceDataService _referenceDataService;
    private readonly IServiceProvider _serviceProvider;
    private readonly IUserPreferencesService _userPreferencesService;
    private readonly ISourceSystemRepository _sourceSystemRepository;
    private readonly IDialogService _dialogService;
    private List<CostCenter> _allItems = new();

    [ObservableProperty] private ObservableCollection<CostCenter> _costCenters = new();
    [ObservableProperty] private CostCenter? _selectedCostCenter;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private int _totalCount;

    public CostCentersViewModel(IReferenceDataService referenceDataService, IServiceProvider serviceProvider)
    {
        _referenceDataService = referenceDataService ?? throw new ArgumentNullException(nameof(referenceDataService));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
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
        try { IsBusy = true; _allItems = (await _referenceDataService.GetCostCentersAsync()).ToList(); ApplyFilter(); }
        catch (Exception ex) { _dialogService.ShowError($"Error loading cost centers: {ex.Message}", "Error"); }
        finally { IsBusy = false; }
    }

    [RelayCommand] private async Task RefreshAsync() => await LoadDataAsync();

    [RelayCommand]
    private void AddItem()
    {
        var viewModel = new ReferenceDataEditViewModel(_referenceDataService, _sourceSystemRepository, "CostCenters");
        var window = new ReferenceDataEditWindow();
        window.SetViewModel(viewModel);
        window.Owner = Application.Current.MainWindow;
        if (window.ShowDialog() == true) _ = RefreshAsync();
    }

    [RelayCommand]
    private async Task EditItemAsync()
    {
        if (SelectedCostCenter == null) return;
        if (SelectedCostCenter.Source == null)
        {
            _dialogService.ShowError("Cost Center has no source system.", "Error");
            return;
        }
        var item = await _referenceDataService.GetCostCenterByIdAsync(SelectedCostCenter.ExternalId, SelectedCostCenter.Source);
        if (item == null) { _dialogService.ShowError("Cost Center not found.", "Error"); return; }
        var viewModel = new ReferenceDataEditViewModel(_referenceDataService, _sourceSystemRepository, "CostCenters", item.ExternalId, item.Code, item.Name, item.Source);
        var window = new ReferenceDataEditWindow();
        window.SetViewModel(viewModel);
        window.Owner = Application.Current.MainWindow;
        if (window.ShowDialog() == true) await RefreshAsync();
    }

    [RelayCommand]
    private async Task DeleteItemAsync()
    {
        if (SelectedCostCenter == null) return;
        if (SelectedCostCenter.Source == null)
        {
            _dialogService.ShowError("Cost Center has no source system.", "Error");
            return;
        }
        if (_dialogService.ShowConfirm($"Delete cost center '{SelectedCostCenter.ExternalId}'?", "Confirm"))
        {
            try { await _referenceDataService.DeleteCostCenterAsync(SelectedCostCenter.ExternalId, SelectedCostCenter.Source); await RefreshAsync(); }
            catch (Exception ex) { _dialogService.ShowError($"Error: {ex.Message}", "Error"); }
        }
    }

    partial void OnSelectedCostCenterChanged(CostCenter? value)
    {
        if (value != null)
        {
            _userPreferencesService.LastSelectedCostCenterCode = value.Code;
        }
    }

    partial void OnSearchTextChanged(string value)
    {
        _userPreferencesService.LastCostCenterSearchText = value;
        ApplyFilter();
    }

    private void LoadPersistedState()
    {
        var lastCode = _userPreferencesService.LastSelectedCostCenterCode;
        if (!string.IsNullOrWhiteSpace(lastCode))
        {
            SelectedCostCenter = CostCenters.FirstOrDefault(c => c.Code == lastCode);
        }

        if (!string.IsNullOrWhiteSpace(_userPreferencesService.LastCostCenterSearchText))
        {
            SearchText = _userPreferencesService.LastCostCenterSearchText;
        }
    }

    [RelayCommand]
    private void ResetSettings()
    {
        SelectedCostCenter = null;
        SearchText = string.Empty;
        _userPreferencesService.LastSelectedCostCenterCode = null;
        _userPreferencesService.LastCostCenterSearchText = string.Empty;
    }

    private void ApplyFilter()
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            CostCenters = new ObservableCollection<CostCenter>(_allItems);
        }
        else
        {
            var searchTerm = SearchText.Trim();
            var stringProperties = typeof(CostCenter).GetProperties()
                .Where(p => p.PropertyType == typeof(string) && p.CanRead);

            CostCenters = new ObservableCollection<CostCenter>(_allItems.Where(i =>
                stringProperties.Any(prop =>
                {
                    var value = prop.GetValue(i)?.ToString();
                    return value?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) == true;
                })
            ));
        }
        TotalCount = CostCenters.Count;
    }

    /// <summary>
    /// Saves the current column order to preferences.
    /// Called from view when columns are reordered.
    /// </summary>
    public void SaveColumnOrder(List<string> columnNames)
    {
        _userPreferencesService.CostCentersColumnOrder = columnNames;
    }

    /// <summary>
    /// Gets the saved column order from preferences.
    /// Called from view to restore column order.
    /// </summary>
    public List<string>? GetSavedColumnOrder()
    {
        var order = _userPreferencesService.CostCentersColumnOrder;
        return order;
    }

    /// <summary>
    /// Saves the current column widths to preferences.
    /// Called from view when column widths are changed.
    /// </summary>
    public void SaveColumnWidths(Dictionary<string, double> columnWidths)
    {
        _userPreferencesService.CostCentersColumnWidths = columnWidths;
    }

    /// <summary>
    /// Gets the saved column widths from preferences.
    /// Called from view to restore column widths.
    /// </summary>
    public Dictionary<string, double>? GetSavedColumnWidths()
    {
        var widths = _userPreferencesService.CostCentersColumnWidths;
        return widths;
    }

    /// <summary>
    /// Resets the saved column order to default (XAML order).
    /// </summary>
    [RelayCommand]
    private void ResetColumnOrder()
    {
        _userPreferencesService.CostCentersColumnOrder = null;
    }

    /// <summary>
    /// Resets the saved column widths to default (XAML widths).
    /// </summary>
    [RelayCommand]
    private void ResetColumnWidths()
    {
        _userPreferencesService.CostCentersColumnWidths = null;
    }
}
