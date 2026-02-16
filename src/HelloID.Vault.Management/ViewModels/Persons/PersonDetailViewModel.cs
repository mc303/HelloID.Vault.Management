using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HelloID.Vault.Core.Models.Entities;
using HelloID.Vault.Core.Models.DTOs;
using HelloID.Vault.Services.Interfaces;
using HelloID.Vault.Management.Views.Persons;
using HelloID.Vault.Data.Repositories.Interfaces;

using Microsoft.Extensions.DependencyInjection;

namespace HelloID.Vault.Management.ViewModels.Persons;

/// <summary>
/// Detail ViewModel for displaying and editing a single Person.
/// </summary>
public partial class PersonDetailViewModel : ObservableObject
{
    private readonly IPersonService _personService;
    private readonly ICustomFieldRepository _customFieldRepository;
    private readonly IContractService _contractService;
    private readonly ISourceSystemRepository _sourceSystemRepository;
    private readonly IServiceProvider _serviceProvider;
    private readonly IUserPreferencesService _userPreferencesService;
    private readonly IDialogService _dialogService;
    private readonly string _personId;

    [ObservableProperty]
    private PersonDetailDto? _person;

    /// <summary>
    /// Gets the formatted display name with external ID.
    /// Only appends external ID if it's not already in the display name.
    /// </summary>
    public string FormattedDisplayName
    {
        get
        {
            if (Person == null) return "Loading";

            var displayName = Person.DisplayName ?? "";
            var externalId = Person.ExternalId ?? "";

            // Check if external ID is already in the display name (with parentheses)
            if (displayName.Contains($"({externalId})"))
            {
                return displayName;
            }

            // Append external ID if not empty
            if (!string.IsNullOrEmpty(externalId))
            {
                return $"{displayName} ({externalId})";
            }

            return displayName;
        }
    }

    [ObservableProperty]
    private ObservableCollection<CustomFieldDto> _customFields = new();

    public bool HasCustomFields => CustomFields.Count > 0;

    [ObservableProperty]
    private ObservableCollection<ContractDetailDto> _contracts = new();

    public int ContractsCount => Contracts.Count;

    [ObservableProperty]
    private ObservableCollection<ContactDto> _contacts = new();

    public int ContactsCount => Contacts.Count;

    public bool CanAddContact => Contacts.Count < 2;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private int _activeTabIndex = 0;

    public PersonDetailViewModel(IPersonService personService, ICustomFieldRepository customFieldRepository, IServiceProvider serviceProvider, IUserPreferencesService userPreferencesService, string personId)
    {
        _personService = personService ?? throw new ArgumentNullException(nameof(personService));
        _customFieldRepository = customFieldRepository ?? throw new ArgumentNullException(nameof(customFieldRepository));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _userPreferencesService = userPreferencesService ?? throw new ArgumentNullException(nameof(userPreferencesService));
        _dialogService = serviceProvider.GetRequiredService<IDialogService>();
        _personId = personId ?? throw new ArgumentNullException(nameof(personId));

        _contractService = _serviceProvider.GetRequiredService<IContractService>();
        _sourceSystemRepository = _serviceProvider.GetRequiredService<ISourceSystemRepository>();

        // Initialize tab index from saved preferences
        _activeTabIndex = _userPreferencesService.LastSelectedPersonTabIndex;
    }

    /// <summary>
    /// Updates user preferences when the active tab changes.
    /// </summary>
    partial void OnActiveTabIndexChanged(int value)
    {
        _userPreferencesService.LastSelectedPersonTabIndex = value;
    }

    /// <summary>
    /// Loads the person details from the service including primary contract and business contact.
    /// </summary>
    public async Task LoadAsync()
    {
        if (IsLoading) return;

        System.Diagnostics.Debug.WriteLine($"[PersonDetailViewModel] LoadAsync START - PersonId: '{_personId}'");

        try
        {
            IsLoading = true;
            ErrorMessage = null;

            Person = await _personService.GetPersonDetailAsync(_personId);
            OnPropertyChanged(nameof(FormattedDisplayName));

            if (Person == null)
            {
                ErrorMessage = "Person not found.";
                System.Diagnostics.Debug.WriteLine($"[PersonDetailViewModel] Person NOT FOUND for PersonId: '{_personId}'");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[PersonDetailViewModel] Person loaded: '{Person.DisplayName}' (ExternalId: '{Person.ExternalId}')");

                // Load custom fields
                var customFields = await _personService.GetCustomFieldsAsync(_personId);
                CustomFields.Clear();
                foreach (var field in customFields)
                {
                    CustomFields.Add(field);
                }
                System.Diagnostics.Debug.WriteLine($"[PersonDetailViewModel] Loaded {CustomFields.Count} custom fields");
                OnPropertyChanged(nameof(HasCustomFields));

                // Load contracts
                System.Diagnostics.Debug.WriteLine($"[PersonDetailViewModel] Loading contracts for PersonId: '{_personId}'...");
                var contracts = await _personService.GetContractsByPersonIdAsync(_personId);
                Contracts.Clear();
                foreach (var contract in contracts)
                {
                    Contracts.Add(contract);
                }
                System.Diagnostics.Debug.WriteLine($"[PersonDetailViewModel] Loaded {Contracts.Count} contracts into ObservableCollection");
                OnPropertyChanged(nameof(ContractsCount));

                // Load contacts
                System.Diagnostics.Debug.WriteLine($"[PersonDetailViewModel] Loading contacts for PersonId: '{_personId}'...");
                var contacts_data = await _personService.GetContactsByPersonIdAsync(_personId);
                Contacts.Clear();
                foreach (var contact in contacts_data)
                {
                    Contacts.Add(contact);
                }
                System.Diagnostics.Debug.WriteLine($"[PersonDetailViewModel] Loaded {Contacts.Count} contacts into ObservableCollection");
                OnPropertyChanged(nameof(ContactsCount));
                OnPropertyChanged(nameof(CanAddContact));
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PersonDetailViewModel] LoadAsync FAILED: {ex.GetType().Name} - {ex.Message}");
            if (ex.InnerException != null)
            {
                System.Diagnostics.Debug.WriteLine($"[PersonDetailViewModel]   Inner Exception: {ex.InnerException.Message}");
            }
            System.Diagnostics.Debug.WriteLine($"[PersonDetailViewModel]   StackTrace: {ex.StackTrace}");
            ErrorMessage = $"Error loading person: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
            System.Diagnostics.Debug.WriteLine($"[PersonDetailViewModel] LoadAsync END - PersonId: '{_personId}'");
        }
    }

    /// <summary>
    /// Refreshes the person data from the database.
    /// </summary>
    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadAsync();
    }

    /// <summary>
    /// Opens a dialog to add a new person.
    /// </summary>
    [RelayCommand]
    private async Task AddPersonAsync()
    {
        var editViewModel = new PersonEditViewModel(_personService, _customFieldRepository, _sourceSystemRepository);
        await editViewModel.InitializeAsync();

        var editWindow = new PersonEditWindow();
        editWindow.SetViewModel(editViewModel);
        editWindow.Owner = Application.Current.MainWindow;

        var result = editWindow.ShowDialog();
        if (result == true)
        {
            // Notify parent to refresh the list
            PersonChangedEvent?.Invoke();
        }
    }

    /// <summary>
    /// Opens a dialog to edit the current person.
    /// </summary>
    [RelayCommand]
    private async Task EditPersonAsync()
    {
        if (Person == null) return;

        try
        {
            // Load the full Person entity for editing
            var personEntity = await _personService.GetByIdAsync(_personId);
            if (personEntity == null)
            {
                ErrorMessage = "Cannot edit: Person not found.";
                return;
            }

            var editViewModel = new PersonEditViewModel(_personService, _customFieldRepository, _sourceSystemRepository, personEntity);
            await editViewModel.InitializeAsync();

            var editWindow = new PersonEditWindow();
            editWindow.SetViewModel(editViewModel);
            editWindow.Owner = Application.Current.MainWindow;

            var result = editWindow.ShowDialog();
            if (result == true)
            {
                // Reload the detail view
                await LoadAsync();
                // Notify parent to refresh the list
                PersonChangedEvent?.Invoke();
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error loading person for edit: {ex.Message}";
        }
    }

    /// <summary>
    /// Deletes the current person with confirmation.
    /// </summary>
    [RelayCommand]
    private async Task DeletePersonAsync()
    {
        if (Person == null) return;

        var result = _dialogService.ShowConfirm(
            $"Are you sure you want to delete '{Person.DisplayName}'?\n\nThis action cannot be undone.",
            "Confirm Delete");

        if (!result)
            return;

        try
        {
            await _personService.DeleteAsync(_personId);

            _dialogService.ShowInfo("Person deleted successfully.", "Success");

            // Clear the detail view
            Person = null;
            OnPropertyChanged(nameof(FormattedDisplayName));

            // Notify parent to refresh the list
            PersonChangedEvent?.Invoke();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error deleting person: {ex.Message}";
            _dialogService.ShowError($"Failed to delete person:\n{ex.Message}", "Error");
        }
    }

    /// <summary>
    /// Opens a dialog to add a new contract.
    /// </summary>
    [RelayCommand]
    private async Task AddContractAsync()
    {
        try
        {
            // Use ActivatorUtilities to resolve dependencies and pass additional arguments
            // Constructor: (IContractService, IReferenceDataService, string personId, Contract? existingContract)
            var editViewModel = ActivatorUtilities.CreateInstance<ContractEditViewModel>(_serviceProvider, _personId);
            var editWindow = new ContractEditWindow();
            editWindow.SetViewModel(editViewModel);
            editWindow.Owner = Application.Current.MainWindow;

            if (editWindow.ShowDialog() == true)
            {
                await LoadAsync();
            }
        }
        catch (Exception ex)
        {
             _dialogService.ShowError($"Error opening contract window: {ex.Message}", "Error");
        }
    }

    /// <summary>
    /// Opens a dialog to edit a contract.
    /// </summary>
    [RelayCommand]
    private async Task EditContractAsync(ContractDetailDto? contractDto)
    {
        if (contractDto == null) return;

        try
        {
            // Load full contract entity
            var contract = await _contractService.GetByIdAsync(contractDto.ContractId);
            if (contract == null)
            {
                _dialogService.ShowError("Contract not found.", "Error");
                return;
            }

            var editViewModel = ActivatorUtilities.CreateInstance<ContractEditViewModel>(_serviceProvider, _personId, contract);
            var editWindow = new ContractEditWindow();
            editWindow.SetViewModel(editViewModel);
            editWindow.Owner = Application.Current.MainWindow;

            if (editWindow.ShowDialog() == true)
            {
                await LoadAsync();
            }
        }
        catch (Exception ex)
        {
             _dialogService.ShowError($"Error opening contract window: {ex.Message}", "Error");
        }
    }

    /// <summary>
    /// Event raised when a person is added, edited, or deleted.
    /// </summary>
    public event Action? PersonChangedEvent;

    /// <summary>
    /// Opens a dialog to add a new contact.
    /// Auto-determines contact type based on existing contacts.
    /// </summary>
    [RelayCommand]
    private void AddContact()
    {
        // Auto-determine contact type based on what's missing
        string contactType = "Personal"; // Default

        if (Contacts.Count == 1)
        {
            // If one contact exists, add the missing type
            var existingType = Contacts[0].Type;
            contactType = existingType == "Business" ? "Personal" : "Business";
        }

        var editViewModel = new ContactEditViewModel(_serviceProvider, null, _personId);
        // Set the contact type
        editViewModel.Type = contactType;

        var editWindow = new ContactEditWindow();
        editWindow.SetViewModel(editViewModel);
        editWindow.Owner = Application.Current.MainWindow;

        var result = editWindow.ShowDialog();
        if (result == true)
        {
            // Refresh contacts
            _ = LoadAsync();
        }
    }

    /// <summary>
    /// Opens a dialog to edit an existing contact.
    /// </summary>
    [RelayCommand]
    private async Task EditContactAsync(ContactDto? contact)
    {
        if (contact == null) return;

        try
        {
            // Load full contact entity for editing
            var contactRepository = _serviceProvider.GetRequiredService<IContactRepository>();
            var contactEntity = await contactRepository.GetByIdAsync(contact.ContactId);
            if (contactEntity == null)
            {
                ErrorMessage = "Cannot edit: Contact not found.";
                return;
            }

            var editViewModel = new ContactEditViewModel(_serviceProvider, contactEntity, _personId);
            var editWindow = new ContactEditWindow();
            editWindow.SetViewModel(editViewModel);
            editWindow.Owner = Application.Current.MainWindow;

            var result = editWindow.ShowDialog();
            if (result == true)
            {
                // Refresh contacts
                await LoadAsync();
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error loading contact for edit: {ex.Message}";
        }
    }

    /// <summary>
    /// Deletes a contact with confirmation.
    /// </summary>
    [RelayCommand]
    private async Task DeleteContactAsync(ContactDto? contact)
    {
        if (contact == null) return;

        var result = _dialogService.ShowConfirm(
            $"Are you sure you want to delete this {contact.Type} contact?\n\nThis action cannot be undone.",
            "Confirm Delete");

        if (!result)
            return;

        try
        {
            var contactRepository = _serviceProvider.GetRequiredService<IContactRepository>();
            await contactRepository.DeleteAsync(contact.ContactId);

            _dialogService.ShowInfo("Contact deleted successfully.", "Success");

            // Refresh contacts
            await LoadAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error deleting contact: {ex.Message}";
            _dialogService.ShowError($"Failed to delete contact:\n{ex.Message}", "Error");
        }
    }

    /// <summary>
    /// Deletes a contract with confirmation.
    /// </summary>
    [RelayCommand]
    private async Task DeleteContractAsync(ContractDetailDto? contract)
    {
        if (contract == null) return;

        var result = _dialogService.ShowConfirm(
            $"Are you sure you want to delete this contract?\n\nThis action cannot be undone.",
            "Confirm Delete");

        if (!result)
            return;

        try
        {
            await _contractService.DeleteAsync(contract.ContractId);

            _dialogService.ShowInfo("Contract deleted successfully.", "Success");

            // Refresh contracts
            await LoadAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error deleting contract: {ex.Message}";
            _dialogService.ShowError($"Failed to delete contract:\n{ex.Message}", "Error");
        }
    }
}
