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

public partial class TeamsViewModel : ObservableObject
{
    private readonly IReferenceDataService _referenceDataService;
    private readonly IServiceProvider _serviceProvider;
    private readonly IUserPreferencesService _userPreferencesService;
    private readonly ISourceSystemRepository _sourceSystemRepository;
    private readonly IDialogService _dialogService;
    private List<Team> _allItems = new();

    [ObservableProperty] private ObservableCollection<Team> _teams = new();
    [ObservableProperty] private Team? _selectedTeam;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private int _totalCount;

    public TeamsViewModel(IReferenceDataService referenceDataService, IServiceProvider serviceProvider)
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
        try { IsBusy = true; _allItems = (await _referenceDataService.GetTeamsAsync()).ToList(); ApplyFilter(); }
        catch (Exception ex) { _dialogService.ShowError($"Error: {ex.Message}", "Error"); }
        finally { IsBusy = false; }
    }

    [RelayCommand] private async Task RefreshAsync() => await LoadDataAsync();
    [RelayCommand] private void AddItem() { var vm = new ReferenceDataEditViewModel(_referenceDataService, _sourceSystemRepository, "Teams"); var w = new ReferenceDataEditWindow(); w.SetViewModel(vm); w.Owner = Application.Current.MainWindow; if (w.ShowDialog() == true) _ = RefreshAsync(); }

    [RelayCommand]
    private async Task EditItemAsync()
    {
        if (SelectedTeam == null) return;
        if (SelectedTeam.Source == null)
        {
            _dialogService.ShowError("Team has no source system.", "Error");
            return;
        }
        var item = await _referenceDataService.GetTeamByIdAsync(SelectedTeam.ExternalId, SelectedTeam.Source);
        if (item == null) return;
        var vm = new ReferenceDataEditViewModel(_referenceDataService, _sourceSystemRepository, "Teams", item.ExternalId, item.Code, item.Name, item.Source);
        var w = new ReferenceDataEditWindow(); w.SetViewModel(vm); w.Owner = Application.Current.MainWindow;
        if (w.ShowDialog() == true) await RefreshAsync();
    }

    [RelayCommand]
    private async Task DeleteItemAsync()
    {
        if (SelectedTeam == null) return;
        if (SelectedTeam.Source == null)
        {
            _dialogService.ShowError("Team has no source system.", "Error");
            return;
        }
        if (_dialogService.ShowConfirm($"Delete '{SelectedTeam.ExternalId}'?", "Confirm"))
        {
            try { await _referenceDataService.DeleteTeamAsync(SelectedTeam.ExternalId, SelectedTeam.Source); await RefreshAsync(); }
            catch (Exception ex) { _dialogService.ShowError($"Error: {ex.Message}", "Error"); }
        }
    }

    partial void OnSelectedTeamChanged(Team? value)
    {
        if (value != null)
        {
            _userPreferencesService.LastSelectedTeamCode = value.Code;
        }
    }

    partial void OnSearchTextChanged(string value)
    {
        _userPreferencesService.LastTeamSearchText = value;
        ApplyFilter();
    }

    private void LoadPersistedState()
    {
        var lastCode = _userPreferencesService.LastSelectedTeamCode;
        if (!string.IsNullOrWhiteSpace(lastCode))
        {
            SelectedTeam = Teams.FirstOrDefault(t => t.Code == lastCode);
        }

        if (!string.IsNullOrWhiteSpace(_userPreferencesService.LastTeamSearchText))
        {
            SearchText = _userPreferencesService.LastTeamSearchText;
        }
    }

    [RelayCommand]
    private void ResetSettings()
    {
        SelectedTeam = null;
        SearchText = string.Empty;
        _userPreferencesService.LastSelectedTeamCode = null;
        _userPreferencesService.LastTeamSearchText = string.Empty;
    }

    private void ApplyFilter()
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            Teams = new ObservableCollection<Team>(_allItems);
        }
        else
        {
            var searchTerm = SearchText.Trim();
            var stringProperties = typeof(Team).GetProperties()
                .Where(p => p.PropertyType == typeof(string) && p.CanRead);

            Teams = new ObservableCollection<Team>(_allItems.Where(i =>
                stringProperties.Any(prop =>
                {
                    var value = prop.GetValue(i)?.ToString();
                    return value?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) == true;
                })
            ));
        }
        TotalCount = Teams.Count;
    }

    /// <summary>
    /// Saves the current column order to preferences.
    /// Called from view when columns are reordered.
    /// </summary>
    public void SaveColumnOrder(List<string> columnNames)
    {
        _userPreferencesService.TeamsColumnOrder = columnNames;
    }

    /// <summary>
    /// Gets the saved column order from preferences.
    /// Called from view to restore column order.
    /// </summary>
    public List<string>? GetSavedColumnOrder()
    {
        var order = _userPreferencesService.TeamsColumnOrder;
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
        _userPreferencesService.TeamsColumnWidths = columnWidths;
    }

    /// <summary>
    /// Gets the saved column widths from preferences.
    /// Called from view to restore column widths.
    /// </summary>
    public Dictionary<string, double>? GetSavedColumnWidths()
    {
        var widths = _userPreferencesService.TeamsColumnWidths;
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
        _userPreferencesService.TeamsColumnOrder = null;
    }

    /// <summary>
    /// Resets the saved column widths to default (XAML widths).
    /// </summary>
    [RelayCommand]
    private void ResetColumnWidths()
    {
        _userPreferencesService.TeamsColumnWidths = null;
    }
}
