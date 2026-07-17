using System.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HelloID.Vault.Core.Models.Entities;
using HelloID.Vault.Core.Models.Filters;
using HelloID.Vault.Data.Repositories.Interfaces;

namespace HelloID.Vault.Management.ViewModels.ReferenceData;

public abstract partial class CustomFieldDataViewModelBase : ObservableObject
{
    private readonly ICustomFieldRepository _customFieldRepository;
    private CancellationTokenSource? _cts;
    private const int BatchSize = 200;
    private int _currentOffset;
    private int _totalCount;
    private List<CustomFieldSchema> _currentSchemas = new();

    public IReadOnlyList<CustomFieldSchema> CurrentSchemas => _currentSchemas;

    public abstract string TableName { get; }
    public abstract string TableDisplayName { get; }

    public abstract List<(string FieldName, string DisplayName, double Width)> GetBaseColumns();

    public abstract List<(string FieldName, string DisplayName)> GetBaseSearchFields();

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private DataTable? _data;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private int _totalRecords;

    [ObservableProperty]
    private DataRowView? _selectedRow;

    [ObservableProperty]
    private int _activeFilterCount;

    private List<FieldFilterCriteria> _advancedFilters = new();

    [ObservableProperty]
    private string _loadingMessage = string.Empty;

    public bool HasMoreData => Data != null && Data.Rows.Count < _totalCount;

    public event Action<DataTable?, List<CustomFieldSchema>>? DataLoaded;

    protected CustomFieldDataViewModelBase(ICustomFieldRepository customFieldRepository)
    {
        _customFieldRepository = customFieldRepository ?? throw new ArgumentNullException(nameof(customFieldRepository));
    }

    partial void OnSearchTextChanged(string value)
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        _ = Task.Delay(500, token).ContinueWith(t =>
        {
            if (!t.IsCanceled) _ = ResetAndLoadAsync(isSearchRefinement: true);
        }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    [RelayCommand]
    public async Task ResetAndLoadAsync()
    {
        await ResetAndLoadAsync(isSearchRefinement: false);
    }

    public async Task ResetAndLoadAsync(bool isSearchRefinement)
    {
        _currentOffset = 0;

        if (!isSearchRefinement)
        {
            Data = null;
            TotalRecords = 0;
        }

        await LoadBatchAsync(isInitialLoad: true);
    }

    [RelayCommand]
    private async Task Search()
    {
        await ResetAndLoadAsync();
    }

    [RelayCommand]
    private async Task Refresh()
    {
        await ResetAndLoadAsync();
    }

    [RelayCommand]
    private async Task ResetSettings()
    {
        SearchText = string.Empty;
        SelectedRow = null;
        _advancedFilters.Clear();
        ActiveFilterCount = 0;
        await ResetAndLoadAsync();
    }

    [RelayCommand]
    private void ShowAdvancedSearch()
    {
        var availableFields = GetBaseSearchFields();

        foreach (var schema in _currentSchemas)
        {
            availableFields.Add((schema.FieldKey, schema.DisplayName));
        }

        var window = new Views.ReferenceData.AdvancedFieldSearchWindow(availableFields, _advancedFilters.Count > 0 ? _advancedFilters : null)
        {
            Owner = System.Windows.Application.Current.MainWindow
        };

        if (window.ShowDialog() == true)
        {
            _advancedFilters = window.ResultFilters;
            ActiveFilterCount = _advancedFilters.Count;
            _ = ResetAndLoadAsync(isSearchRefinement: false);
        }
    }

    [RelayCommand]
    private void ClearAdvancedFilters()
    {
        _advancedFilters.Clear();
        ActiveFilterCount = 0;
        _ = ResetAndLoadAsync(isSearchRefinement: false);
    }

    public async Task LoadMoreAsync()
    {
        if (IsLoading || !HasMoreData) return;
        await LoadBatchAsync(isInitialLoad: false);
    }

    private async Task LoadBatchAsync(bool isInitialLoad)
    {
        try
        {
            if (isInitialLoad && (Data == null || _currentOffset == 0))
            {
                IsLoading = true;
                LoadingMessage = $"Loading {TableDisplayName}...";
            }

            if (isInitialLoad)
            {
                _currentSchemas = (await _customFieldRepository.GetSchemasAsync(TableName))
                    .OrderBy(s => s.SortOrder)
                    .ThenBy(s => s.DisplayName)
                    .ToList();
            }

            var searchTerm = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText;

            if (isInitialLoad)
            {
                _totalCount = await _customFieldRepository.GetPivotCountAsync(TableName, searchTerm, _advancedFilters.Count > 0 ? _advancedFilters : null);
            }

            var batch = await _customFieldRepository.GetPivotDataAsync(TableName, _currentOffset / BatchSize + 1, BatchSize, searchTerm, _advancedFilters.Count > 0 ? _advancedFilters : null);
            _currentOffset += BatchSize;

            if (isInitialLoad || Data == null)
            {
                Data = batch;
            }
            else
            {
                foreach (System.Data.DataRow row in batch.Rows)
                {
                    Data.ImportRow(row);
                }
                Data.AcceptChanges();
            }

            TotalRecords = _totalCount;
            OnPropertyChanged(nameof(HasMoreData));

            DataLoaded?.Invoke(Data, _currentSchemas);
        }
        catch (Exception ex)
        {
            LoadingMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
