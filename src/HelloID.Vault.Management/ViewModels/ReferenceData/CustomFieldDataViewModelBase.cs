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

    /// <summary>
    /// Clears cached data so the next navigation reloads from the database.
    /// Called after imports that change the underlying data.
    /// </summary>
    public void InvalidateData()
    {
        Data = null;
        _currentOffset = 0;
        _totalCount = 0;
        _currentSchemas = new();
        TotalRecords = 0;
        SelectedRow = null;
    }

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
            System.Diagnostics.Debug.WriteLine($"[CustomFieldDataVM] LoadBatchAsync START: table={TableName}, isInitialLoad={isInitialLoad}, offset={_currentOffset}, search='{SearchText}', filters={_advancedFilters.Count}");

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
                System.Diagnostics.Debug.WriteLine($"[CustomFieldDataVM] Loaded {_currentSchemas.Count} schemas for '{TableName}'");
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

            System.Diagnostics.Debug.WriteLine($"[CustomFieldDataVM] LoadBatchAsync SUCCESS: batch={batch.Rows.Count} rows x {batch.Columns.Count} cols, total={_totalCount}");

            DataLoaded?.Invoke(Data, _currentSchemas);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CustomFieldDataVM] *** LoadBatchAsync FAILED: {ex.GetType().FullName}: {ex.Message} ***");
            System.Diagnostics.Debug.WriteLine($"[CustomFieldDataVM] Stack: {ex.StackTrace}");
            if (ex.InnerException != null)
            {
                System.Diagnostics.Debug.WriteLine($"[CustomFieldDataVM] Inner: {ex.InnerException.GetType().FullName}: {ex.InnerException.Message}");
            }
            LoadingMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
