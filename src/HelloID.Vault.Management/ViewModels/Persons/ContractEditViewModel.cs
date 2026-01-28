using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HelloID.Vault.Core.Models.Entities;
using HelloID.Vault.Services.Interfaces;
using HelloID.Vault.Data.Repositories.Interfaces;
using SourceSystemDto = HelloID.Vault.Core.Models.DTOs.SourceSystemDto;

namespace HelloID.Vault.Management.ViewModels.Persons;

public partial class ContractEditViewModel : ObservableValidator
{
    private readonly IContractService _contractService;
    private readonly IReferenceDataService _referenceDataService;
    private readonly ICustomFieldRepository _customFieldRepository;
    private readonly ISourceSystemRepository _sourceSystemRepository;
    private readonly string _personId;

    [ObservableProperty]
    private Contract _contract;

    // Wrapper property for Contract.Source to enable validation
    [Required(ErrorMessage = "Source is required.")]
    [StringLength(50, ErrorMessage = "Source cannot exceed 50 characters.")]
    private string? _contractSource;

    // Expose Contract.Source for binding with validation
    public string? ContractSource
    {
        get => _contractSource ?? Contract?.Source;
        set
        {
            if (Contract != null)
            {
                Contract.Source = value;
                _contractSource = value;
                ValidateProperty(value, nameof(ContractSource));
                OnPropertyChanged(nameof(ContractSource));
            }
        }
    }

    partial void OnContractChanged(Contract? value)
    {
        if (value != null)
        {
            _contractSource = value.Source;
            OnPropertyChanged(nameof(ContractSource));
        }
    }

    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string _title = "Add Contract";

    // Lookup Collections
    public ObservableCollection<Location> Locations { get; } = new();
    public ObservableCollection<Title> Titles { get; } = new();
    public ObservableCollection<Department> Departments { get; } = new();
    public ObservableCollection<Division> Divisions { get; } = new();
    public ObservableCollection<Team> Teams { get; } = new();
    public ObservableCollection<Organization> Organizations { get; } = new();
    public ObservableCollection<Employer> Employers { get; } = new();
    public ObservableCollection<CostCenter> CostCenters { get; } = new();

    public ObservableCollection<CostBearer> CostBearers { get; } = new();
    public ObservableCollection<Person> Managers { get; } = new();
    public ObservableCollection<SourceSystemDto> SourceSystems { get; } = new();

    [ObservableProperty]
    private SourceSystemDto? _selectedSourceSystem;

    [ObservableProperty]
    private Person? _selectedManager;

    partial void OnSelectedManagerChanged(Person? value)
    {
        Contract.ManagerPersonExternalId = value?.PersonId;
    }

    [ObservableProperty]
    private Location? _selectedLocation;

    partial void OnSelectedLocationChanged(Location? value)
    {
        Contract.LocationExternalId = value?.ExternalId;
        Contract.LocationSource = value?.Source;
    }

    [ObservableProperty]
    private Department? _selectedDepartment;

    partial void OnSelectedDepartmentChanged(Department? value)
    {
        Contract.DepartmentExternalId = value?.ExternalId;
        Contract.DepartmentSource = value?.Source;
    }

    [ObservableProperty]
    private Title? _selectedTitle;

    partial void OnSelectedTitleChanged(Title? value)
    {
        Contract.TitleExternalId = value?.ExternalId;
        Contract.TitleSource = value?.Source;
    }

    [ObservableProperty]
    private Division? _selectedDivision;

    partial void OnSelectedDivisionChanged(Division? value)
    {
        Contract.DivisionExternalId = value?.ExternalId;
        Contract.DivisionSource = value?.Source;
    }

    [ObservableProperty]
    private Team? _selectedTeam;

    partial void OnSelectedTeamChanged(Team? value)
    {
        Contract.TeamExternalId = value?.ExternalId;
        Contract.TeamSource = value?.Source;
    }

    [ObservableProperty]
    private Organization? _selectedOrganization;

    partial void OnSelectedOrganizationChanged(Organization? value)
    {
        Contract.OrganizationExternalId = value?.ExternalId;
        Contract.OrganizationSource = value?.Source;
    }

    [ObservableProperty]
    private Employer? _selectedEmployer;

    partial void OnSelectedEmployerChanged(Employer? value)
    {
        Contract.EmployerExternalId = value?.ExternalId;
        Contract.EmployerSource = value?.Source;
    }

    [ObservableProperty]
    private CostCenter? _selectedCostCenter;

    partial void OnSelectedCostCenterChanged(CostCenter? value)
    {
        Contract.CostCenterExternalId = value?.ExternalId;
        Contract.CostCenterSource = value?.Source;
    }

    [ObservableProperty]
    private CostBearer? _selectedCostBearer;

    partial void OnSelectedCostBearerChanged(CostBearer? value)
    {
        Contract.CostBearerExternalId = value?.ExternalId;
        Contract.CostBearerSource = value?.Source;
    }

    [ObservableProperty]
    private ObservableCollection<CustomFieldEditDto> _customFields = new();

    public bool HasCustomFields => CustomFields.Count > 0;

    // Helper method to ensure all source fields are populated before save
    private void PopulateSourceFields()
    {
        var location = string.IsNullOrWhiteSpace(Contract.LocationExternalId) ? null : Locations.FirstOrDefault(l => l.ExternalId == Contract.LocationExternalId);
        Contract.LocationSource = location?.Source;

        var department = string.IsNullOrWhiteSpace(Contract.DepartmentExternalId) ? null : Departments.FirstOrDefault(d => d.ExternalId == Contract.DepartmentExternalId);
        Contract.DepartmentSource = department?.Source;

        var title = string.IsNullOrWhiteSpace(Contract.TitleExternalId) ? null : Titles.FirstOrDefault(t => t.ExternalId == Contract.TitleExternalId);
        Contract.TitleSource = title?.Source;

        var division = string.IsNullOrWhiteSpace(Contract.DivisionExternalId) ? null : Divisions.FirstOrDefault(d => d.ExternalId == Contract.DivisionExternalId);
        Contract.DivisionSource = division?.Source;

        var team = string.IsNullOrWhiteSpace(Contract.TeamExternalId) ? null : Teams.FirstOrDefault(t => t.ExternalId == Contract.TeamExternalId);
        Contract.TeamSource = team?.Source;

        var organization = string.IsNullOrWhiteSpace(Contract.OrganizationExternalId) ? null : Organizations.FirstOrDefault(o => o.ExternalId == Contract.OrganizationExternalId);
        Contract.OrganizationSource = organization?.Source;

        var employer = string.IsNullOrWhiteSpace(Contract.EmployerExternalId) ? null : Employers.FirstOrDefault(e => e.ExternalId == Contract.EmployerExternalId);
        Contract.EmployerSource = employer?.Source;

        var costCenter = string.IsNullOrWhiteSpace(Contract.CostCenterExternalId) ? null : CostCenters.FirstOrDefault(c => c.ExternalId == Contract.CostCenterExternalId);
        Contract.CostCenterSource = costCenter?.Source;

        var costBearer = string.IsNullOrWhiteSpace(Contract.CostBearerExternalId) ? null : CostBearers.FirstOrDefault(c => c.ExternalId == Contract.CostBearerExternalId);
        Contract.CostBearerSource = costBearer?.Source;
    }

    // Constructor for "Add Contract" (no existing contract)
    public ContractEditViewModel(
        IContractService contractService,
        IReferenceDataService referenceDataService,
        ICustomFieldRepository customFieldRepository,
        ISourceSystemRepository sourceSystemRepository,
        string personId)
        : this(contractService, referenceDataService, customFieldRepository, sourceSystemRepository, personId, null)
    {
    }

    // Main constructor
    public ContractEditViewModel(
        IContractService contractService,
        IReferenceDataService referenceDataService,
        ICustomFieldRepository customFieldRepository,
        ISourceSystemRepository sourceSystemRepository,
        string personId,
        Contract? existingContract)
    {
        _contractService = contractService ?? throw new ArgumentNullException(nameof(contractService));
        _referenceDataService = referenceDataService ?? throw new ArgumentNullException(nameof(referenceDataService));
        _customFieldRepository = customFieldRepository ?? throw new ArgumentNullException(nameof(customFieldRepository));
        _sourceSystemRepository = sourceSystemRepository ?? throw new ArgumentNullException(nameof(sourceSystemRepository));
        _personId = personId ?? throw new ArgumentNullException(nameof(personId));

        if (existingContract != null)
        {
            // Clone existing contract
            Contract = new Contract
            {
                ContractId = existingContract.ContractId,
                ExternalId = existingContract.ExternalId,
                PersonId = existingContract.PersonId,
                StartDate = existingContract.StartDate,
                EndDate = existingContract.EndDate,
                TypeCode = existingContract.TypeCode,
                TypeDescription = existingContract.TypeDescription,
                Fte = existingContract.Fte,
                HoursPerWeek = existingContract.HoursPerWeek,
                Percentage = existingContract.Percentage,
                Sequence = existingContract.Sequence,
                ManagerPersonExternalId = existingContract.ManagerPersonExternalId,
                LocationExternalId = existingContract.LocationExternalId,
                CostCenterExternalId = existingContract.CostCenterExternalId,
                CostBearerExternalId = existingContract.CostBearerExternalId,
                EmployerExternalId = existingContract.EmployerExternalId,
                TeamExternalId = existingContract.TeamExternalId,
                DepartmentExternalId = existingContract.DepartmentExternalId,
                DivisionExternalId = existingContract.DivisionExternalId,
                TitleExternalId = existingContract.TitleExternalId,
                OrganizationExternalId = existingContract.OrganizationExternalId,
                Source = existingContract.Source
            };
            IsEditing = true;
            Title = "Edit Contract";
        }
        else
        {
            Contract = new Contract
            {
                PersonId = _personId,
                StartDate = DateTime.Today.ToString("yyyy-MM-dd")
            };
            IsEditing = false;
            Title = "Add Contract";
        }
    }

    public async Task LoadAsync()
    {
        if (IsLoading) return;
        IsLoading = true;
        ErrorMessage = null;

        try
        {
            await Task.WhenAll(
                LoadLookup(Locations, _referenceDataService.GetLocationsAsync),
                LoadLookup(Titles, _referenceDataService.GetTitlesAsync),
                LoadLookup(Departments, _referenceDataService.GetDepartmentsAsync),
                LoadLookup(Divisions, _referenceDataService.GetDivisionsAsync),
                LoadLookup(Teams, _referenceDataService.GetTeamsAsync),
                LoadLookup(Organizations, _referenceDataService.GetOrganizationsAsync),
                LoadLookup(Employers, _referenceDataService.GetEmployersAsync),
                LoadLookup(CostCenters, _referenceDataService.GetCostCentersAsync),
                LoadLookup(CostBearers, _referenceDataService.GetCostBearersAsync),
                LoadLookup(Managers, _referenceDataService.GetPersonsAsync),
                LoadSourceSystemsAsync()
            );

            // Set selected references after loading (for existing contracts)
            if (IsEditing)
            {
                SelectedLocation = Locations.FirstOrDefault(l => l.ExternalId == Contract.LocationExternalId);
                SelectedDepartment = Departments.FirstOrDefault(d => d.ExternalId == Contract.DepartmentExternalId);
                SelectedTitle = Titles.FirstOrDefault(t => t.ExternalId == Contract.TitleExternalId);
                SelectedDivision = Divisions.FirstOrDefault(d => d.ExternalId == Contract.DivisionExternalId);
                SelectedTeam = Teams.FirstOrDefault(t => t.ExternalId == Contract.TeamExternalId);
                SelectedOrganization = Organizations.FirstOrDefault(o => o.ExternalId == Contract.OrganizationExternalId);
                SelectedEmployer = Employers.FirstOrDefault(e => e.ExternalId == Contract.EmployerExternalId);
                SelectedCostCenter = CostCenters.FirstOrDefault(c => c.ExternalId == Contract.CostCenterExternalId);
                SelectedCostBearer = CostBearers.FirstOrDefault(c => c.ExternalId == Contract.CostBearerExternalId);
            }

            // Set selected manager after loading
            if (!string.IsNullOrWhiteSpace(Contract.ManagerPersonExternalId))
            {
                SelectedManager = Managers.FirstOrDefault(m => m.PersonId == Contract.ManagerPersonExternalId);
            }

            // Set selected source system after loading
            if (!string.IsNullOrWhiteSpace(Contract.Source))
            {
                SelectedSourceSystem = SourceSystems.FirstOrDefault(s => s.SystemId == Contract.Source);
            }

            // Load custom fields
            if (IsEditing && !string.IsNullOrWhiteSpace(Contract.ExternalId))
            {
                await LoadCustomFieldsAsync(Contract.ExternalId);
            }
            else
            {
                await LoadCustomFieldSchemasAsync();
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load reference data: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadLookup<T>(ObservableCollection<T> collection, Func<Task<IEnumerable<T>>> fetcher)
    {
        var items = await fetcher();
        collection.Clear();
        foreach (var item in items)
        {
            collection.Add(item);
        }
    }

    private async Task LoadSourceSystemsAsync()
    {
        var sourceSystems = await _sourceSystemRepository.GetAllAsync();
        SourceSystems.Clear();
        foreach (var source in sourceSystems.OrderBy(s => s.DisplayName))
        {
            SourceSystems.Add(source);
        }
    }

    partial void OnSelectedSourceSystemChanged(SourceSystemDto? value)
    {
        ContractSource = value?.SystemId;
    }

    [RelayCommand]
    private async Task SaveAsync(Window window)
    {
        if (IsLoading) return;

        try
        {
            IsLoading = true;
            ErrorMessage = null;

            // Validate all properties using ObservableValidator
            ValidateAllProperties();
            if (HasErrors)
            {
                // Get all errors and display the first one
                var allErrors = GetErrors(null);
                if (allErrors != null)
                {
                    // ObservableValidator returns ValidationResult objects
                    var firstResult = allErrors.OfType<ValidationResult>().FirstOrDefault();
                    ErrorMessage = firstResult?.ErrorMessage ?? "Please fix validation errors before saving.";
                }
                return;
            }

            // Check if custom fields have values
            var hasCustomFieldValues = CustomFields.Any(cf => !string.IsNullOrWhiteSpace(cf.Value));

            // Generate External ID if not provided and custom fields have values
            if (string.IsNullOrWhiteSpace(Contract.ExternalId) && hasCustomFieldValues)
            {
                Contract.ExternalId = Guid.NewGuid().ToString();
            }

            // Ensure all source fields are populated before save
            PopulateSourceFields();

            await _contractService.SaveAsync(Contract);

            // Save custom fields if external ID is provided
            if (!string.IsNullOrWhiteSpace(Contract.ExternalId))
            {
                await SaveCustomFieldsAsync(Contract.ExternalId);
            }

            window.DialogResult = true;
            window.Close();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void Cancel(Window window)
    {
        window.DialogResult = false;
        window.Close();
    }

    /// <summary>
    /// Loads custom field schemas for a new contract.
    /// </summary>
    private async Task LoadCustomFieldSchemasAsync()
    {
        try
        {
            var schemas = await _customFieldRepository.GetSchemasAsync("contracts");
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
            ErrorMessage = $"Error loading custom field schemas: {ex.Message}";
        }
    }

    /// <summary>
    /// Loads custom field values for an existing contract.
    /// </summary>
    private async Task LoadCustomFieldsAsync(string externalId)
    {
        try
        {
            var schemas = await _customFieldRepository.GetSchemasAsync("contracts");
            var values = await _customFieldRepository.GetValuesAsync(externalId, "contracts");
            var valuesDict = values.ToDictionary(v => v.FieldKey);

            CustomFields.Clear();
            foreach (var schema in schemas.OrderBy(s => s.SortOrder))
            {
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
            ErrorMessage = $"Error loading custom fields: {ex.Message}";
        }
    }

    /// <summary>
    /// Saves custom field values for a contract.
    /// </summary>
    private async Task SaveCustomFieldsAsync(string externalId)
    {
        try
        {
            foreach (var customField in CustomFields)
            {
                if (!string.IsNullOrWhiteSpace(customField.Value))
                {
                    var fieldValue = new CustomFieldValue
                    {
                        EntityId = externalId,
                        TableName = "contracts",
                        FieldKey = customField.FieldKey,
                        TextValue = customField.Value
                    };

                    await _customFieldRepository.UpsertValueAsync(fieldValue);
                }
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error saving custom fields: {ex.Message}";
            throw;
        }
    }
}
