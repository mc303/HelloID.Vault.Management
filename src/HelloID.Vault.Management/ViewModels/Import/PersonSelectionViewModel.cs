using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace HelloID.Vault.Management.ViewModels.Import;

public partial class PersonSelectionItemViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isSelected;

    public string PersonId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? ExternalId { get; set; }
}

public partial class PersonSelectionViewModel : ObservableObject
{
    public event Action<bool>? CloseRequested;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private int _selectedCount;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _clearMissingManagerReferences = true;

    [ObservableProperty]
    private bool _cascadeImportManagers;

    public ObservableCollection<PersonSelectionItemViewModel> AllPersons { get; } = new();
    public ObservableCollection<PersonSelectionItemViewModel> FilteredPersons { get; } = new();

    public PersonSelectionViewModel()
    {
    }

    public void LoadPersons(IEnumerable<(string PersonId, string DisplayName, string? ExternalId)> persons)
    {
        AllPersons.Clear();
        FilteredPersons.Clear();
        SelectedCount = 0;

        foreach (var p in persons)
        {
            var item = new PersonSelectionItemViewModel
            {
                PersonId = p.PersonId,
                DisplayName = p.DisplayName,
                ExternalId = p.ExternalId,
                IsSelected = true
            };
            item.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(PersonSelectionItemViewModel.IsSelected))
                {
                    UpdateSelectedCount();
                }
            };
            AllPersons.Add(item);
            FilteredPersons.Add(item);
        }

        UpdateSelectedCount();
    }

    partial void OnSearchTextChanged(string value)
    {
        FilteredPersons.Clear();

        var filtered = string.IsNullOrWhiteSpace(value)
            ? AllPersons
            : AllPersons.Where(p =>
                p.DisplayName.Contains(value, StringComparison.OrdinalIgnoreCase) ||
                (p.ExternalId?.Contains(value, StringComparison.OrdinalIgnoreCase) ?? false));

        foreach (var item in filtered)
        {
            FilteredPersons.Add(item);
        }
    }

    private void UpdateSelectedCount()
    {
        SelectedCount = AllPersons.Count(p => p.IsSelected);
    }

    [RelayCommand]
    private void SelectAll()
    {
        foreach (var item in AllPersons)
        {
            item.IsSelected = true;
        }
    }

    [RelayCommand]
    private void DeselectAll()
    {
        foreach (var item in AllPersons)
        {
            item.IsSelected = false;
        }
    }

    [RelayCommand]
    private void Confirm()
    {
        CloseRequested?.Invoke(true);
    }

    [RelayCommand]
    private void Cancel()
    {
        CloseRequested?.Invoke(false);
    }

    public HashSet<string> GetSelectedPersonIds()
    {
        return AllPersons
            .Where(p => p.IsSelected)
            .Select(p => p.PersonId)
            .ToHashSet();
    }
}
