using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HelloID.Vault.Core.Models.Entities;
using HelloID.Vault.Core.Models.DTOs;
using HelloID.Vault.Services.Interfaces;
using HelloID.Vault.Data.Repositories.Interfaces;

namespace HelloID.Vault.Management.ViewModels.Persons;

/// <summary>
/// ViewModel for adding or editing a person.
/// </summary>
/// <summary>
/// Represents a custom field for editing.
/// </summary>
public partial class CustomFieldEditDto : ObservableObject
{
    [ObservableProperty]
    private string _fieldKey = string.Empty;

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private string? _value;
}

public partial class PersonEditViewModel : ObservableObject
{
    private readonly IPersonService _personService;
    private readonly ICustomFieldRepository _customFieldRepository;
    private readonly ISourceSystemRepository _sourceSystemRepository;
    private readonly bool _isEditMode;
    private readonly string? _existingPersonId;
    private readonly string? _existingExternalId;

    public event Action<bool>? CloseRequested;

    [ObservableProperty]
    private string _windowTitle = "Add Person";

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private string? _externalId;

    [ObservableProperty]
    private string? _userName;

    [ObservableProperty]
    private string? _gender;

    [ObservableProperty]
    private string? _honorificPrefix;

    [ObservableProperty]
    private string? _honorificSuffix;

    [ObservableProperty]
    private DateTime? _birthDate;

    [ObservableProperty]
    private string? _birthLocality;

    [ObservableProperty]
    private string? _maritalStatus;

    [ObservableProperty]
    private string? _initials;

    [ObservableProperty]
    private string? _givenName;

    [ObservableProperty]
    private string? _familyName;

    [ObservableProperty]
    private string? _familyNamePrefix;

    [ObservableProperty]
    private string? _convention;

    [ObservableProperty]
    private string? _nickName;

    [ObservableProperty]
    private string? _familyNamePartner;

    [ObservableProperty]
    private string? _familyNamePartnerPrefix;

    [ObservableProperty]
    private bool _blocked;

    [ObservableProperty]
    private string? _statusReason;

    [ObservableProperty]
    private bool _excluded;

    [ObservableProperty]
    private bool _hrExcluded;

    [ObservableProperty]
    private bool _manualExcluded;

    [ObservableProperty]
    private string? _primaryManagerPersonId;

    [ObservableProperty]
    private string? _primaryManagerUpdatedAt;

    [ObservableProperty]
    private ObservableCollection<SourceSystemDto> _sourceSystems = new();

    [ObservableProperty]
    private SourceSystemDto? _selectedSourceSystem;

    partial void OnSelectedSourceSystemChanged(SourceSystemDto? value)
    {
        if (value != null)
        {
            Source = value.SystemId;
        }
    }

    [ObservableProperty]
    private string? _source;

    [ObservableProperty]
    private ObservableCollection<CustomFieldEditDto> _customFields = new();

    public bool HasCustomFields => CustomFields.Count > 0;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isSaving;

    /// <summary>
    /// Constructor for adding a new person.
    /// </summary>
    public PersonEditViewModel(IPersonService personService, ICustomFieldRepository customFieldRepository, ISourceSystemRepository sourceSystemRepository)
    {
        _personService = personService ?? throw new ArgumentNullException(nameof(personService));
        _customFieldRepository = customFieldRepository ?? throw new ArgumentNullException(nameof(customFieldRepository));
        _sourceSystemRepository = sourceSystemRepository ?? throw new ArgumentNullException(nameof(sourceSystemRepository));
        _isEditMode = false;
        WindowTitle = "Add Person";
        // Note: Call InitializeAsync() after construction to load data
    }

    /// <summary>
    /// Constructor for editing an existing person.
    /// </summary>
    public PersonEditViewModel(IPersonService personService, ICustomFieldRepository customFieldRepository, ISourceSystemRepository sourceSystemRepository, Person existingPerson)
    {
        _personService = personService ?? throw new ArgumentNullException(nameof(personService));
        _customFieldRepository = customFieldRepository ?? throw new ArgumentNullException(nameof(customFieldRepository));
        _sourceSystemRepository = sourceSystemRepository ?? throw new ArgumentNullException(nameof(sourceSystemRepository));
        _isEditMode = true;
        _existingPersonId = existingPerson.PersonId;
        _existingExternalId = existingPerson.ExternalId;
        WindowTitle = "Edit Person";

        System.Diagnostics.Debug.WriteLine($"[PersonEditViewModel] EDIT constructor: Set Source to '{existingPerson.Source}'");

        // Pre-fill form with existing data
        DisplayName = existingPerson.DisplayName;
        ExternalId = existingPerson.ExternalId;
        UserName = existingPerson.UserName;
        Gender = existingPerson.Gender;
        HonorificPrefix = existingPerson.HonorificPrefix;
        HonorificSuffix = existingPerson.HonorificSuffix;
        BirthDate = ParseDateString(existingPerson.BirthDate);
        BirthLocality = existingPerson.BirthLocality;
        MaritalStatus = existingPerson.MaritalStatus;
        Initials = existingPerson.Initials;
        GivenName = existingPerson.GivenName;
        FamilyName = existingPerson.FamilyName;
        FamilyNamePrefix = existingPerson.FamilyNamePrefix;
        Convention = existingPerson.Convention;
        NickName = existingPerson.NickName;
        FamilyNamePartner = existingPerson.FamilyNamePartner;
        FamilyNamePartnerPrefix = existingPerson.FamilyNamePartnerPrefix;
        Blocked = existingPerson.Blocked;
        StatusReason = existingPerson.StatusReason;
        Excluded = existingPerson.Excluded;
        HrExcluded = existingPerson.HrExcluded;
        ManualExcluded = existingPerson.ManualExcluded;
        PrimaryManagerPersonId = existingPerson.PrimaryManagerPersonId;
        PrimaryManagerUpdatedAt = existingPerson.PrimaryManagerUpdatedAt;
        Source = existingPerson.Source;
        // Note: Call InitializeAsync() after construction to load data
    }

    /// <summary>
    /// Saves the person (creates new or updates existing).
    /// </summary>
    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsSaving) return;

        try
        {
            IsSaving = true;
            ErrorMessage = null;

            System.Diagnostics.Debug.WriteLine($"[PersonEditViewModel] SaveAsync started - Mode: {(_isEditMode ? "EDIT" : "ADD")}");
            System.Diagnostics.Debug.WriteLine($"[PersonEditViewModel] ExternalId before processing: '{ExternalId}'");
            System.Diagnostics.Debug.WriteLine($"[PersonEditViewModel] _existingExternalId: '{_existingExternalId}'");

            // Validate required fields
            System.Diagnostics.Debug.WriteLine($"[PersonEditViewModel] Validating DisplayName: '{DisplayName}'");
            if (string.IsNullOrWhiteSpace(DisplayName))
            {
                System.Diagnostics.Debug.WriteLine($"[PersonEditViewModel] Validation failed: DisplayName is empty");
                ErrorMessage = "Display name is required.";
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[PersonEditViewModel] Validating Source: '{Source}'");
            if (string.IsNullOrWhiteSpace(Source))
            {
                System.Diagnostics.Debug.WriteLine($"[PersonEditViewModel] Validation failed: Source is empty");
                ErrorMessage = "Source is required.";
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[PersonEditViewModel] Validation passed");

            // Fallback: if Source is empty but SelectedSourceSystem is set, use it
            if (string.IsNullOrWhiteSpace(Source) && SelectedSourceSystem != null)
            {
                Source = SelectedSourceSystem.SystemId;
                System.Diagnostics.Debug.WriteLine($"[PersonEditViewModel] Using SelectedSourceSystem.SystemId for Source: '{Source}'");
            }

            // Check if custom fields have values
            var hasCustomFieldValues = CustomFields.Any(cf => !string.IsNullOrWhiteSpace(cf.Value));
            System.Diagnostics.Debug.WriteLine($"[PersonEditViewModel] hasCustomFieldValues: {hasCustomFieldValues}");
            System.Diagnostics.Debug.WriteLine($"[PersonEditViewModel] CustomFields.Count: {CustomFields.Count}");

            if (hasCustomFieldValues)
            {
                var fieldsWithValues = CustomFields.Where(cf => !string.IsNullOrWhiteSpace(cf.Value)).Select(cf => $"{cf.FieldKey}={cf.Value}");
                System.Diagnostics.Debug.WriteLine($"[PersonEditViewModel] Custom fields with values: {string.Join(", ", fieldsWithValues)}");
            }

            // Generate External ID if not provided and custom fields have values
            var externalId = ExternalId;
            if (string.IsNullOrWhiteSpace(externalId) && hasCustomFieldValues)
            {
                externalId = Guid.NewGuid().ToString();
                ExternalId = externalId; // Update the property so it's saved
                System.Diagnostics.Debug.WriteLine($"[PersonEditViewModel] Generated new External ID: '{externalId}'");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[PersonEditViewModel] Using existing External ID: '{externalId}'");
            }

            var person = new Person
            {
                PersonId = _isEditMode ? _existingPersonId! : Guid.NewGuid().ToString(),
                DisplayName = DisplayName,
                ExternalId = string.IsNullOrWhiteSpace(ExternalId) ? null : ExternalId,
                UserName = string.IsNullOrWhiteSpace(UserName) ? null : UserName,
                Gender = string.IsNullOrWhiteSpace(Gender) ? null : Gender,
                HonorificPrefix = string.IsNullOrWhiteSpace(HonorificPrefix) ? null : HonorificPrefix,
                HonorificSuffix = string.IsNullOrWhiteSpace(HonorificSuffix) ? null : HonorificSuffix,
                BirthDate = BirthDate?.ToString("yyyy-MM-dd"),
                BirthLocality = string.IsNullOrWhiteSpace(BirthLocality) ? null : BirthLocality,
                MaritalStatus = string.IsNullOrWhiteSpace(MaritalStatus) ? null : MaritalStatus,
                Initials = string.IsNullOrWhiteSpace(Initials) ? null : Initials,
                GivenName = string.IsNullOrWhiteSpace(GivenName) ? null : GivenName,
                FamilyName = string.IsNullOrWhiteSpace(FamilyName) ? null : FamilyName,
                FamilyNamePrefix = string.IsNullOrWhiteSpace(FamilyNamePrefix) ? null : FamilyNamePrefix,
                Convention = string.IsNullOrWhiteSpace(Convention) ? null : Convention,
                NickName = string.IsNullOrWhiteSpace(NickName) ? null : NickName,
                FamilyNamePartner = string.IsNullOrWhiteSpace(FamilyNamePartner) ? null : FamilyNamePartner,
                FamilyNamePartnerPrefix = string.IsNullOrWhiteSpace(FamilyNamePartnerPrefix) ? null : FamilyNamePartnerPrefix,
                Blocked = Blocked,
                StatusReason = string.IsNullOrWhiteSpace(StatusReason) ? null : StatusReason,
                Excluded = Excluded,
                HrExcluded = HrExcluded,
                ManualExcluded = ManualExcluded,
                PrimaryManagerPersonId = string.IsNullOrWhiteSpace(PrimaryManagerPersonId) ? null : PrimaryManagerPersonId,
                PrimaryManagerUpdatedAt = PrimaryManagerUpdatedAt,
                Source = Source
            };

            System.Diagnostics.Debug.WriteLine($"[PersonEditViewModel] Person object created with ExternalId: '{person.ExternalId}'");

            if (_isEditMode)
            {
                System.Diagnostics.Debug.WriteLine($"[PersonEditViewModel] Calling UpdateAsync for PersonId: '{person.PersonId}', ExternalId: '{person.ExternalId}'");
                await _personService.UpdateAsync(person);
                System.Diagnostics.Debug.WriteLine($"[PersonEditViewModel] UpdateAsync completed successfully");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[PersonEditViewModel] Calling CreateAsync for PersonId: '{person.PersonId}', ExternalId: '{person.ExternalId}'");
                await _personService.CreateAsync(person);
                System.Diagnostics.Debug.WriteLine($"[PersonEditViewModel] CreateAsync completed successfully");
            }

            System.Diagnostics.Debug.WriteLine($"[PersonEditViewModel] Person save completed");

            // Save custom fields if external ID is provided
            if (!string.IsNullOrWhiteSpace(person.ExternalId))
            {
                System.Diagnostics.Debug.WriteLine($"[PersonEditViewModel] Calling SaveCustomFieldsAsync with ExternalId: '{person.ExternalId}'");
                await SaveCustomFieldsAsync(person.ExternalId);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[PersonEditViewModel] Skipping SaveCustomFieldsAsync - ExternalId is empty");
            }

            // Close dialog with success
            System.Diagnostics.Debug.WriteLine($"[PersonEditViewModel] SaveAsync completed successfully, calling CloseRequested");
            CloseRequested?.Invoke(true);
            System.Diagnostics.Debug.WriteLine($"[PersonEditViewModel] CloseRequested invoked");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error saving person: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"[PersonEditViewModel] Error saving person: {ex}");
        }
        finally
        {
            IsSaving = false;
        }
    }

    /// <summary>
    /// Cancels the operation and closes the window.
    /// </summary>
    [RelayCommand]
    private void Cancel()
    {
        CloseRequested?.Invoke(false);
    }

    /// <summary>
    /// Parses an ISO 8601 date string to a DateTime.
    /// </summary>
    private DateTime? ParseDateString(string? dateString)
    {
        if (string.IsNullOrWhiteSpace(dateString))
            return null;

        if (DateTime.TryParse(dateString, out var result))
            return result;

        return null;
    }

    /// <summary>
    /// Initializes the view model by loading source systems and custom fields.
    /// Call this after construction to ensure data is loaded before the UI binds.
    /// </summary>
    public async Task InitializeAsync()
    {
        System.Diagnostics.Debug.WriteLine($"[PersonEditViewModel] InitializeAsync started, isEditMode={_isEditMode}");
        await LoadSourceSystemsAsync();
        if (_isEditMode)
        {
            System.Diagnostics.Debug.WriteLine($"[PersonEditViewModel] Calling LoadCustomFieldsAsync");
            await LoadCustomFieldsAsync();
            System.Diagnostics.Debug.WriteLine($"[PersonEditViewModel] LoadCustomFieldsAsync completed");
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"[PersonEditViewModel] Calling LoadCustomFieldSchemasAsync");
            await LoadCustomFieldSchemasAsync();
            System.Diagnostics.Debug.WriteLine($"[PersonEditViewModel] LoadCustomFieldSchemasAsync completed");
        }
        System.Diagnostics.Debug.WriteLine($"[PersonEditViewModel] InitializeAsync completed");
    }

    /// <summary>
    /// Loads source systems for the dropdown.
    /// </summary>
    private async Task LoadSourceSystemsAsync()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"[PersonEditViewModel] LoadSourceSystemsAsync called with Source: '{Source}'");
            var sourceSystems = await _sourceSystemRepository.GetAllAsync();
            SourceSystems.Clear();
            foreach (var source in sourceSystems.OrderBy(s => s.DisplayName))
            {
                SourceSystems.Add(source);
                System.Diagnostics.Debug.WriteLine($"[PersonEditViewModel] Added source: {source.DisplayName} (SystemId: '{source.SystemId}')");
            }

            // Set SelectedSourceSystem after loading - this runs on UI thread
            if (!string.IsNullOrWhiteSpace(Source))
            {
                System.Diagnostics.Debug.WriteLine($"[PersonEditViewModel] Looking for source with SystemId: '{Source}'");
                var matched = SourceSystems.FirstOrDefault(s => s.SystemId == Source);
                SelectedSourceSystem = matched;
                System.Diagnostics.Debug.WriteLine($"[PersonEditViewModel] SelectedSourceSystem set to: {(matched?.DisplayName ?? "NULL")}");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[PersonEditViewModel] Source is empty, skipping SelectedSourceSystem selection");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PersonEditViewModel] Error loading source systems: {ex}");
        }
    }

    /// <summary>
    /// Loads custom field schemas for a new person.
    /// </summary>
    private async Task LoadCustomFieldSchemasAsync()
    {
        try
        {
            var schemas = await _customFieldRepository.GetSchemasAsync("persons");
            CustomFields.Clear();
            foreach (var schema in schemas.OrderBy(s => s.SortOrder))
            {
                CustomFields.Add(new CustomFieldEditDto
                {
                    FieldKey = schema.FieldKey,
                    DisplayName = schema.DisplayName,
                    Value = string.Empty
                });
            }
            OnPropertyChanged(nameof(HasCustomFields));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading custom field schemas: {ex}");
        }
    }

    /// <summary>
    /// Loads custom field values for an existing person.
    /// </summary>
    private async Task LoadCustomFieldsAsync()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_existingExternalId))
            {
                await LoadCustomFieldSchemasAsync();
                return;
            }

            var schemas = await _customFieldRepository.GetSchemasAsync("persons");
            var values = await _customFieldRepository.GetValuesAsync(_existingExternalId, "persons");
            var valuesDict = values.ToDictionary(v => v.FieldKey);

            CustomFields.Clear();
            foreach (var schema in schemas.OrderBy(s => s.SortOrder))
            {
                string? value = null;
                if (valuesDict.TryGetValue(schema.FieldKey, out var fieldValue))
                {
                    CustomFields.Add(new CustomFieldEditDto
                    {
                        FieldKey = schema.FieldKey,
                        DisplayName = schema.DisplayName,
                        Value = fieldValue.TextValue
                    });
                }
            }
            OnPropertyChanged(nameof(HasCustomFields));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading custom fields: {ex}");
        }
    }

    /// <summary>
    /// Saves custom field values for a person.
    /// </summary>
    private async Task SaveCustomFieldsAsync(string externalId)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"[PersonEditViewModel] SaveCustomFieldsAsync called with externalId='{externalId}', CustomFields.Count={CustomFields.Count}");

            int savedCount = 0;
            foreach (var customField in CustomFields)
            {
                if (string.IsNullOrWhiteSpace(customField.Value))
                {
                    System.Diagnostics.Debug.WriteLine($"[PersonEditViewModel] Skipping custom field '{customField.FieldKey}' - value is empty");
                    continue;
                }

                var fieldValue = new CustomFieldValue
                {
                    EntityId = externalId,
                    TableName = "persons",
                    FieldKey = customField.FieldKey,
                    TextValue = customField.Value
                };

                System.Diagnostics.Debug.WriteLine($"[PersonEditViewModel] Saving custom field '{customField.FieldKey}' as text: {customField.Value}");
                await _customFieldRepository.UpsertValueAsync(fieldValue);
                savedCount++;
            }

            System.Diagnostics.Debug.WriteLine($"[PersonEditViewModel] Successfully saved {savedCount} custom field(s)");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PersonEditViewModel] Error saving custom fields: {ex}");
            ErrorMessage = $"Error saving custom fields: {ex.Message}";
        }
    }
}
