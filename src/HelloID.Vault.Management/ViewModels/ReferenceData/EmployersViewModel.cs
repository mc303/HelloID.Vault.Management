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

public partial class EmployersViewModel : ObservableObject
{
    private readonly IReferenceDataService _referenceDataService;
    private readonly IServiceProvider _serviceProvider;
    private readonly IUserPreferencesService _userPreferencesService;
    private readonly ISourceSystemRepository _sourceSystemRepository;
    private readonly IDialogService _dialogService;
    private List<Employer> _allItems = new();

    [ObservableProperty] private ObservableCollection<Employer> _employers = new();
    [ObservableProperty] private Employer? _selectedEmployer;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private int _totalCount;

    public EmployersViewModel(IReferenceDataService referenceDataService, IServiceProvider serviceProvider)
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
        try { IsBusy = true; _allItems = (await _referenceDataService.GetEmployersAsync()).ToList(); ApplyFilter(); }
        catch (Exception ex) { _dialogService.ShowError($"Error: {ex.Message}", "Error"); }
        finally { IsBusy = false; }
    }

    [RelayCommand] private async Task RefreshAsync() => await LoadDataAsync();
    [RelayCommand] private void AddItem() { var vm = new ReferenceDataEditViewModel(_referenceDataService, _sourceSystemRepository, "Employers"); var w = new ReferenceDataEditWindow(); w.SetViewModel(vm); w.Owner = Application.Current.MainWindow; if (w.ShowDialog() == true) _ = RefreshAsync(); }

    [RelayCommand]
    private async Task EditItemAsync()
    {
        if (SelectedEmployer == null) return;
        if (SelectedEmployer.Source == null)
        {
            _dialogService.ShowError("Employer has no source system.", "Error");
            return;
        }
        var item = await _referenceDataService.GetEmployerByIdAsync(SelectedEmployer.ExternalId, SelectedEmployer.Source);
        if (item == null) return;
        var vm = new ReferenceDataEditViewModel(_referenceDataService, _sourceSystemRepository, "Employers", item.ExternalId, item.Code, item.Name, item.Source);
        var w = new ReferenceDataEditWindow(); w.SetViewModel(vm); w.Owner = Application.Current.MainWindow;
        if (w.ShowDialog() == true) await RefreshAsync();
    }

    [RelayCommand]
    private async Task DeleteItemAsync()
    {
        if (SelectedEmployer == null) return;
        if (SelectedEmployer.Source == null)
        {
            _dialogService.ShowError("Employer has no source system.", "Error");
            return;
        }
        if (_dialogService.ShowConfirm($"Delete '{SelectedEmployer.ExternalId}'?", "Confirm"))
        {
            try { await _referenceDataService.DeleteEmployerAsync(SelectedEmployer.ExternalId, SelectedEmployer.Source); await RefreshAsync(); }
            catch (Exception ex) { _dialogService.ShowError($"Error: {ex.Message}", "Error"); }
        }
    }

    partial void OnSelectedEmployerChanged(Employer? value)
    {
        if (value != null)
        {
            _userPreferencesService.LastSelectedEmployerCode = value.Code;
        }
    }

    partial void OnSearchTextChanged(string value)
    {
        _userPreferencesService.LastEmployerSearchText = value;
        ApplyFilter();
    }

    private void LoadPersistedState()
    {
        var lastCode = _userPreferencesService.LastSelectedEmployerCode;
        if (!string.IsNullOrWhiteSpace(lastCode))
        {
            SelectedEmployer = Employers.FirstOrDefault(e => e.Code == lastCode);
        }

        if (!string.IsNullOrWhiteSpace(_userPreferencesService.LastEmployerSearchText))
        {
            SearchText = _userPreferencesService.LastEmployerSearchText;
        }
    }

    [RelayCommand]
    private void ResetSettings()
    {
        SelectedEmployer = null;
        SearchText = string.Empty;
        _userPreferencesService.LastSelectedEmployerCode = null;
        _userPreferencesService.LastEmployerSearchText = string.Empty;
    }

    private void ApplyFilter()
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            Employers = new ObservableCollection<Employer>(_allItems);
        }
        else
        {
            var searchTerm = SearchText.Trim();
            var stringProperties = typeof(Employer).GetProperties()
                .Where(p => p.PropertyType == typeof(string) && p.CanRead);

            Employers = new ObservableCollection<Employer>(_allItems.Where(i =>
                stringProperties.Any(prop =>
                {
                    var value = prop.GetValue(i)?.ToString();
                    return value?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) == true;
                })
            ));
        }
        TotalCount = Employers.Count;
    }

    /// <summary>
    /// Saves the current column order to preferences.
    /// Called from view when columns are reordered.
    /// </summary>
    public void SaveColumnOrder(List<string> columnNames)
    {
        _userPreferencesService.EmployersColumnOrder = columnNames;
    }

    /// <summary>
    /// Gets the saved column order from preferences.
    /// Called from view to restore column order.
    /// </summary>
    public List<string>? GetSavedColumnOrder()
    {
        var order = _userPreferencesService.EmployersColumnOrder;
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
        _userPreferencesService.EmployersColumnWidths = columnWidths;
    }

    /// <summary>
    /// Gets the saved column widths from preferences.
    /// Called from view to restore column widths.
    /// </summary>
    public Dictionary<string, double>? GetSavedColumnWidths()
    {
        var widths = _userPreferencesService.EmployersColumnWidths;
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
        _userPreferencesService.EmployersColumnOrder = null;
    }

    /// <summary>
    /// Resets the saved column widths to default (XAML widths).
    /// </summary>
    [RelayCommand]
    private void ResetColumnWidths()
    {
        _userPreferencesService.EmployersColumnWidths = null;
    }
}
