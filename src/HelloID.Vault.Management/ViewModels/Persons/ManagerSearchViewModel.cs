using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HelloID.Vault.Core.Models.DTOs;
using HelloID.Vault.Services.Interfaces;

namespace HelloID.Vault.Management.ViewModels.Persons;

public partial class ManagerSearchViewModel : ObservableObject
{
    private readonly IReferenceDataService _referenceDataService;
    private CancellationTokenSource? _cts;
    private const int DebounceMs = 300;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private PersonSearchResultDto? _selectedPerson;

    [ObservableProperty]
    private string? _selectedPersonDisplay;

    [ObservableProperty]
    private bool _isSearching;

    [ObservableProperty]
    private bool _isPopupOpen;

    public ObservableCollection<PersonSearchResultDto> SearchResults { get; } = new();

    public ManagerSearchViewModel(IReferenceDataService referenceDataService)
    {
        _referenceDataService = referenceDataService ?? throw new ArgumentNullException(nameof(referenceDataService));
    }

    partial void OnSearchTextChanged(string value)
    {
        DebounceSearch(value);
    }

    partial void OnSelectedPersonChanged(PersonSearchResultDto? value)
    {
        SelectedPersonDisplay = value?.DisplayName;
    }

    partial void OnIsPopupOpenChanged(bool value)
    {
        if (!value)
        {
            SearchText = string.Empty;
            SearchResults.Clear();
        }
    }

    private async void DebounceSearch(string query)
    {
        _cts?.Cancel();

        if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
        {
            SearchResults.Clear();
            return;
        }

        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        try
        {
            await Task.Delay(DebounceMs, token);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        if (token.IsCancellationRequested) return;

        await PerformSearchAsync(query, token);
    }

    private async Task PerformSearchAsync(string query, CancellationToken token)
    {
        try
        {
            IsSearching = true;
            var results = await _referenceDataService.SearchPersonsAsync(query, 20);

            if (token.IsCancellationRequested) return;

            SearchResults.Clear();
            foreach (var result in results)
            {
                SearchResults.Add(result);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ManagerSearchViewModel] Search error: {ex.Message}");
        }
        finally
        {
            IsSearching = false;
        }
    }

    [RelayCommand]
    private void SelectResult(PersonSearchResultDto person)
    {
        SelectedPerson = person;
        SelectedPersonDisplay = person.DisplayName;
        IsPopupOpen = false;
    }

    [RelayCommand]
    private void OpenSearch()
    {
        SearchText = string.Empty;
        SearchResults.Clear();
        IsPopupOpen = true;
    }

    [RelayCommand]
    private void ClearSelection()
    {
        SelectedPerson = null;
        SelectedPersonDisplay = null;
        SearchText = string.Empty;
        SearchResults.Clear();
        IsPopupOpen = false;
    }

    public void SetInitialPerson(string? personId, string? displayName)
    {
        if (!string.IsNullOrWhiteSpace(personId))
        {
            var name = displayName ?? personId;
            SelectedPerson = new PersonSearchResultDto
            {
                PersonId = personId,
                DisplayName = name
            };
            SelectedPersonDisplay = name;
        }
    }

    public async Task ResolveManagerNameAsync(string? personId)
    {
        if (string.IsNullOrWhiteSpace(personId)) return;

        try
        {
            var results = await _referenceDataService.SearchPersonsAsync(personId, 1);
            var match = results.FirstOrDefault();
            if (match != null)
            {
                SetInitialPerson(personId, match.DisplayName);
            }
            else
            {
                Debug.WriteLine($"[ManagerSearchViewModel] Manager '{personId}' not found in database, not setting as selected");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ManagerSearchViewModel] ResolveManagerName error: {ex.Message}");
        }
    }
}
