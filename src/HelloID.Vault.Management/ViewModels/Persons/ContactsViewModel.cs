using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HelloID.Vault.Core.Models.DTOs;
using HelloID.Vault.Management.Views.Persons;
using HelloID.Vault.Data.Repositories.Interfaces;
using HelloID.Vault.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace HelloID.Vault.Management.ViewModels.Persons;

/// <summary>
/// ViewModel for Contacts table view.
/// </summary>
public partial class ContactsViewModel : ObservableObject
{
    private readonly IContactRepository _contactRepository;
    private readonly IServiceProvider _serviceProvider;
    private readonly IUserPreferencesService _userPreferencesService;
    private List<ContactDto> _allItems = new();

    [ObservableProperty]
    private ObservableCollection<ContactDto> _contacts = new();

    [ObservableProperty]
    private ContactDto? _selectedContact;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private int _totalCount;

    public ContactsViewModel(IContactRepository contactRepository, IServiceProvider serviceProvider)
    {
        _contactRepository = contactRepository ?? throw new ArgumentNullException(nameof(contactRepository));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _userPreferencesService = serviceProvider.GetRequiredService<IUserPreferencesService>();
    }

    public async Task InitializeAsync() => await LoadDataAsync();

    [RelayCommand]
    private async Task LoadDataAsync()
    {
        if (IsBusy) return;

        try
        {
            IsBusy = true;
            _allItems = (await _contactRepository.GetAllAsync()).ToList();
            ApplyFilter();
            LoadPersistedState();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading contacts: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
        MessageBox.Show("Please add contacts through the Person detail view.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    [RelayCommand]
    private async Task EditItemAsync()
    {
        if (SelectedContact == null) return;

        var contact = await _contactRepository.GetByIdAsync(SelectedContact.ContactId);
        if (contact == null)
        {
            MessageBox.Show("Contact not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var viewModel = new ContactEditViewModel(_serviceProvider, contact, contact.PersonId);
        var window = new ContactEditWindow();
        window.SetViewModel(viewModel);
        window.Owner = Application.Current.MainWindow;

        if (window.ShowDialog() == true)
        {
            await RefreshAsync();
        }
    }

    [RelayCommand]
    private async Task DeleteItemAsync()
    {
        if (SelectedContact == null) return;

        var result = MessageBox.Show(
            $"Are you sure you want to delete this contact for '{SelectedContact.PersonDisplayName}'?",
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            try
            {
                await _contactRepository.DeleteAsync(SelectedContact.ContactId);
                await RefreshAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error deleting contact: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    partial void OnSearchTextChanged(string value)
    {
        // Save search text to preferences
        _userPreferencesService.LastContactSearchText = value;
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            Contacts = new ObservableCollection<ContactDto>(_allItems);
        }
        else
        {
            var searchTerm = SearchText.Trim();
            var stringProperties = typeof(ContactDto).GetProperties()
                .Where(p => p.PropertyType == typeof(string) && p.CanRead)
                .ToList();

            var filtered = _allItems.Where(c =>
                stringProperties.Any(prop =>
                {
                    var value = prop.GetValue(c)?.ToString();
                    return value?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) == true;
                })
            ).ToList();

            Contacts = new ObservableCollection<ContactDto>(filtered);
        }

        TotalCount = Contacts.Count;
    }

    partial void OnSelectedContactChanged(ContactDto? value)
    {
        _userPreferencesService.LastSelectedContactId = value?.ContactId;
    }

    /// <summary>
    /// Loads persisted state from preferences and applies it to the view.
    /// Called after contacts are loaded.
    /// </summary>
    public void LoadPersistedState()
    {
        // Get saved contact ID before applying filter (filter will rebuild Contacts collection)
        var savedContactId = _userPreferencesService.LastSelectedContactId;

        // Restore search text (triggers ApplyFilter via OnSearchTextChanged)
        var savedSearchText = _userPreferencesService.LastContactSearchText;
        if (!string.IsNullOrWhiteSpace(savedSearchText))
        {
            SearchText = savedSearchText;
        }

        // Restore selected contact AFTER filter is applied
        // We need to wait for the UI to update, so use Dispatcher
        Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
        {
            if (savedContactId.HasValue && savedContactId.Value > 0)
            {
                SelectedContact = Contacts.FirstOrDefault(c => c.ContactId == savedContactId.Value);
            }
        }), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    /// <summary>
    /// Resets all settings. Clears search text and selection.
    /// </summary>
    [RelayCommand]
    private void ResetSettings()
    {
        // Clear search text
        SearchText = string.Empty;

        // Clear selection
        SelectedContact = null;

        // Clear saved preferences
        _userPreferencesService.LastSelectedContactId = null;
        _userPreferencesService.LastContactSearchText = null;
    }

    /// <summary>
    /// Saves the current column order to preferences.
    /// Called from view when columns are reordered.
    /// </summary>
    public void SaveColumnOrder(List<string> columnNames)
    {
        System.Diagnostics.Debug.WriteLine($"[ContactsViewModel] SaveColumnOrder() - Saving {columnNames.Count} columns: [{string.Join(", ", columnNames)}]");
        _userPreferencesService.ContactsColumnOrder = columnNames;
    }

    /// <summary>
    /// Gets the saved column order from preferences.
    /// Called from view to restore column order.
    /// </summary>
    public List<string>? GetSavedColumnOrder()
    {
        var order = _userPreferencesService.ContactsColumnOrder;
        if (order == null)
        {
            System.Diagnostics.Debug.WriteLine("[ContactsViewModel] GetSavedColumnOrder() - Returning null (no saved order)");
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"[ContactsViewModel] GetSavedColumnOrder() - Returning {order.Count} columns: [{string.Join(", ", order)}]");
        }
        return order;
    }

    /// <summary>
    /// Saves the current column widths to preferences.
    /// Called from view when column widths are changed.
    /// </summary>
    public void SaveColumnWidths(Dictionary<string, double> columnWidths)
    {
        System.Diagnostics.Debug.WriteLine($"[ContactsViewModel] SaveColumnWidths() - Saving {columnWidths.Count} column widths");
        _userPreferencesService.ContactsColumnWidths = columnWidths;
    }

    /// <summary>
    /// Gets the saved column widths from preferences.
    /// Called from view to restore column widths.
    /// </summary>
    public Dictionary<string, double>? GetSavedColumnWidths()
    {
        var widths = _userPreferencesService.ContactsColumnWidths;
        if (widths == null)
        {
            System.Diagnostics.Debug.WriteLine("[ContactsViewModel] GetSavedColumnWidths() - Returning null (no saved widths)");
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"[ContactsViewModel] GetSavedColumnWidths() - Returning {widths.Count} column widths");
        }
        return widths;
    }

    /// <summary>
    /// Resets the saved column order to default (XAML order).
    /// </summary>
    [RelayCommand]
    private void ResetColumnOrder()
    {
        System.Diagnostics.Debug.WriteLine("[ContactsViewModel] ResetColumnOrder() - Clearing saved column order");
        _userPreferencesService.ContactsColumnOrder = null;
    }

    /// <summary>
    /// Resets the saved column widths to default (XAML widths).
    /// </summary>
    [RelayCommand]
    private void ResetColumnWidths()
    {
        System.Diagnostics.Debug.WriteLine("[ContactsViewModel] ResetColumnWidths() - Clearing saved column widths");
        _userPreferencesService.ContactsColumnWidths = null;
    }
}
