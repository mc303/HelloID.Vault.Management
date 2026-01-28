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

public partial class TitlesViewModel : ObservableObject
{
    private readonly IReferenceDataService _referenceDataService;
    private readonly IServiceProvider _serviceProvider;
    private readonly ISourceSystemRepository _sourceSystemRepository;
    private readonly IUserPreferencesService _userPreferencesService;
    private readonly IDialogService _dialogService;
    private List<Title> _allTitles = new();

    [ObservableProperty]
    private ObservableCollection<Title> _titles = new();

    [ObservableProperty]
    private Title? _selectedTitle;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private int _totalCount;

    public TitlesViewModel(IReferenceDataService referenceDataService, IServiceProvider serviceProvider)
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
        try
        {
            IsBusy = true;
            _allTitles = (await _referenceDataService.GetTitlesAsync()).ToList();
            ApplyFilter();
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"Error loading titles: {ex.Message}", "Error");
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
        var viewModel = new ReferenceDataEditViewModel(_referenceDataService, _sourceSystemRepository, "Titles");
        var window = new ReferenceDataEditWindow();
        window.SetViewModel(viewModel);
        window.Owner = Application.Current.MainWindow;
        if (window.ShowDialog() == true) _ = RefreshAsync();
    }

    [RelayCommand]
    private async Task EditItemAsync()
    {
        if (SelectedTitle == null) return;
        if (SelectedTitle.Source == null)
        {
            _dialogService.ShowError("Title has no source system.", "Error");
            return;
        }
        var title = await _referenceDataService.GetTitleByIdAsync(SelectedTitle.ExternalId, SelectedTitle.Source);
        if (title == null)
        {
            _dialogService.ShowError("Title not found.", "Error");
            return;
        }
        var viewModel = new ReferenceDataEditViewModel(_referenceDataService, _sourceSystemRepository, "Titles", title.ExternalId, title.Code, title.Name, title.Source);
        var window = new ReferenceDataEditWindow();
        window.SetViewModel(viewModel);
        window.Owner = Application.Current.MainWindow;
        if (window.ShowDialog() == true) await RefreshAsync();
    }

    [RelayCommand]
    private async Task DeleteItemAsync()
    {
        if (SelectedTitle == null) return;
        if (SelectedTitle.Source == null)
        {
            _dialogService.ShowError("Title has no source system.", "Error");
            return;
        }
        var result = _dialogService.ShowConfirm($"Are you sure you want to delete title '{SelectedTitle.ExternalId}'?", "Confirm Delete");
        if (result)
        {
            try
            {
                await _referenceDataService.DeleteTitleAsync(SelectedTitle.ExternalId, SelectedTitle.Source);
                await RefreshAsync();
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"Error deleting title: {ex.Message}", "Error");
            }
        }
    }

    partial void OnSearchTextChanged(string value)
    {
        // Save search text to preferences
        _userPreferencesService.LastTitleSearchText = value;
        ApplyFilter();
    }

    // Save selected title when changed
    partial void OnSelectedTitleChanged(Title? value)
    {
        _userPreferencesService.LastSelectedTitleCode = value?.Code;
    }

    /// <summary>
    /// Loads persisted state from preferences and applies it to the view.
    /// </summary>
    public void LoadPersistedState()
    {
        var savedCode = _userPreferencesService.LastSelectedTitleCode;
        var savedSearchText = _userPreferencesService.LastTitleSearchText;
        if (!string.IsNullOrWhiteSpace(savedSearchText))
        {
            SearchText = savedSearchText;
        }
        Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
        {
            if (!string.IsNullOrWhiteSpace(savedCode))
            {
                SelectedTitle = Titles.FirstOrDefault(t => t.Code == savedCode);
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
        SelectedTitle = null;
        _userPreferencesService.LastSelectedTitleCode = null;
        _userPreferencesService.LastTitleSearchText = null;
    }

    private void ApplyFilter()
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            Titles = new ObservableCollection<Title>(_allTitles);
        }
        else
        {
            var searchTerm = SearchText.Trim();
            var stringProperties = typeof(Title).GetProperties()
                .Where(p => p.PropertyType == typeof(string) && p.CanRead)
                .ToList();

            var filtered = _allTitles.Where(t =>
                stringProperties.Any(prop =>
                {
                    var value = prop.GetValue(t)?.ToString();
                    return value?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) == true;
                })
            ).ToList();
            Titles = new ObservableCollection<Title>(filtered);
        }
        TotalCount = Titles.Count;
    }

    /// <summary>
    /// Saves the current column order to preferences.
    /// Called from view when columns are reordered.
    /// </summary>
    public void SaveColumnOrder(List<string> columnNames)
    {
        _userPreferencesService.TitlesColumnOrder = columnNames;
    }

    /// <summary>
    /// Gets the saved column order from preferences.
    /// Called from view to restore column order.
    /// </summary>
    public List<string>? GetSavedColumnOrder()
    {
        var order = _userPreferencesService.TitlesColumnOrder;
        return order;
    }

    /// <summary>
    /// Saves the current column widths to preferences.
    /// Called from view when column widths are changed.
    /// </summary>
    public void SaveColumnWidths(Dictionary<string, double> columnWidths)
    {
        _userPreferencesService.TitlesColumnWidths = columnWidths;
    }

    /// <summary>
    /// Gets the saved column widths from preferences.
    /// Called from view to restore column widths.
    /// </summary>
    public Dictionary<string, double>? GetSavedColumnWidths()
    {
        var widths = _userPreferencesService.TitlesColumnWidths;
        return widths;
    }

    /// <summary>
    /// Resets the saved column order to default (XAML order).
    /// </summary>
    [RelayCommand]
    private void ResetColumnOrder()
    {
        _userPreferencesService.TitlesColumnOrder = null;
    }

    /// <summary>
    /// Resets the saved column widths to default (XAML widths).
    /// </summary>
    [RelayCommand]
    private void ResetColumnWidths()
    {
        _userPreferencesService.TitlesColumnWidths = null;
    }
}
