using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HelloID.Vault.Core.Models.Entities;
using HelloID.Vault.Core.Models.DTOs;
using HelloID.Vault.Data.Repositories.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace HelloID.Vault.Management.ViewModels.Persons;

/// <summary>
/// ViewModel for editing or creating a Contact.
/// </summary>
public partial class ContactEditViewModel : ObservableObject
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IContactRepository _contactRepository;
    private readonly string _personId;

    [ObservableProperty]
    private Contact? _contact;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string _type = "Personal";

    [ObservableProperty]
    private string? _email;

    [ObservableProperty]
    private string? _phoneMobile;

    [ObservableProperty]
    private string? _phoneFixed;

    [ObservableProperty]
    private string? _addressStreet;

    [ObservableProperty]
    private string? _addressStreetExt;

    [ObservableProperty]
    private string? _addressHouseNumber;

    [ObservableProperty]
    private string? _addressHouseNumberExt;

    [ObservableProperty]
    private string? _addressPostal;

    [ObservableProperty]
    private string? _addressLocality;

    [ObservableProperty]
    private string? _addressCountry;

    [ObservableProperty]
    private bool _showContactTypeSelection = true;

    [ObservableProperty]
    private bool _isPersonalEnabled = true;

    [ObservableProperty]
    private bool _isBusinessEnabled = true;

    /// <summary>
    /// Gets the window title based on edit mode.
    /// </summary>
    public string WindowTitle
    {
        get
        {
            return Contact == null ? "Add Contact" : "Edit Contact";
        }
    }

    /// <summary>
    /// Gets whether this is edit mode.
    /// </summary>
    public bool IsEditMode => Contact != null;

    public ContactEditViewModel(IServiceProvider serviceProvider, Contact? existingContact, string personId)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _contactRepository = serviceProvider.GetRequiredService<IContactRepository>();
        _personId = personId ?? throw new ArgumentNullException(nameof(personId));

        Contact = existingContact;

        // DEBUG: Log initialization
        System.Diagnostics.Debug.WriteLine($"ContactEditViewModel initialized - IsEditMode: {existingContact != null}, PersonId: {_personId}");

        if (existingContact != null)
        {
            // EDIT MODE - hide contact type selection
            ShowContactTypeSelection = false;

            // Load existing contact data
            Type = existingContact.Type ?? "Personal";
            Email = existingContact.Email;
            PhoneMobile = existingContact.PhoneMobile;
            PhoneFixed = existingContact.PhoneFixed;
            AddressStreet = existingContact.AddressStreet;
            AddressStreetExt = existingContact.AddressStreetExt;
            AddressHouseNumber = existingContact.AddressHouseNumber;
            AddressHouseNumberExt = existingContact.AddressHouseNumberExt;
            AddressPostal = existingContact.AddressPostal;
            AddressLocality = existingContact.AddressLocality;
            AddressCountry = existingContact.AddressCountry;

            // DEBUG: Log loaded data
            System.Diagnostics.Debug.WriteLine($"Loaded existing contact - Type: {Type}, Email: {Email}, PhoneMobile: {PhoneMobile}");
        }
        else
        {
            // DEBUG: Log new contact mode
            System.Diagnostics.Debug.WriteLine("Creating new contact - Type: Personal (default)");
            // Note: InitializeAsync() must be called after construction to load existing contact types
        }
    }

    /// <summary>
    /// Initializes the ViewModel asynchronously. Must be called after construction.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (IsEditMode)
        {
            // Edit mode doesn't need async initialization
            return;
        }

        try
        {
            IsLoading = true;

            // ADD MODE - check existing contacts and enable/disable options
            var existingContacts = await _contactRepository.GetByPersonIdAsync(_personId);

            bool hasPersonal = existingContacts.Any(c => c.Type == "Personal");
            bool hasBusiness = existingContacts.Any(c => c.Type == "Business");

            IsPersonalEnabled = !hasPersonal;  // Disable if already exists
            IsBusinessEnabled = !hasBusiness;  // Disable if already exists

            // Auto-select the only available option
            if (hasPersonal && !hasBusiness)
            {
                Type = "Business";
                System.Diagnostics.Debug.WriteLine("Auto-selected Business (Personal already exists)");
            }
            else if (hasBusiness && !hasPersonal)
            {
                Type = "Personal";
                System.Diagnostics.Debug.WriteLine("Auto-selected Personal (Business already exists)");
            }
            // else both enabled, keep default "Personal"

            System.Diagnostics.Debug.WriteLine($"Contact type availability - Personal: {IsPersonalEnabled}, Business: {IsBusinessEnabled}");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error loading contact types: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"Error in InitializeAsync: {ex}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Saves the contact.
    /// </summary>
    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsLoading) return;

        try
        {
            IsLoading = true;
            ErrorMessage = null;

            // Validate required fields
            if (string.IsNullOrWhiteSpace(Type))
            {
                ErrorMessage = "Contact type is required.";
                return;
            }

            if (IsEditMode && Contact != null)
            {
                // Update existing contact properties
                Contact.Type = Type;
                Contact.Email = Email;
                Contact.PhoneMobile = PhoneMobile;
                Contact.PhoneFixed = PhoneFixed;
                Contact.AddressStreet = AddressStreet;
                Contact.AddressStreetExt = AddressStreetExt;
                Contact.AddressHouseNumber = AddressHouseNumber;
                Contact.AddressHouseNumberExt = AddressHouseNumberExt;
                Contact.AddressPostal = AddressPostal;
                Contact.AddressLocality = AddressLocality;
                Contact.AddressCountry = AddressCountry;

                await _contactRepository.UpdateAsync(Contact);
            }
            else
            {
                // Create new contact
                var newContact = new Contact
                {
                    PersonId = _personId,
                    Type = Type,
                    Email = Email,
                    PhoneMobile = PhoneMobile,
                    PhoneFixed = PhoneFixed,
                    AddressStreet = AddressStreet,
                    AddressStreetExt = AddressStreetExt,
                    AddressHouseNumber = AddressHouseNumber,
                    AddressHouseNumberExt = AddressHouseNumberExt,
                    AddressPostal = AddressPostal,
                    AddressLocality = AddressLocality,
                    AddressCountry = AddressCountry
                };

                await _contactRepository.InsertAsync(newContact);
            }

            // Close dialog with success
            if (Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive) is Window window)
            {
                window.DialogResult = true;
                window.Close();
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error saving contact: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"Error saving contact: {ex}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Cancels the contact edit.
    /// </summary>
    [RelayCommand]
    private void Cancel()
    {
        if (Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive) is Window window)
        {
            window.DialogResult = false;
            window.Close();
        }
    }
}