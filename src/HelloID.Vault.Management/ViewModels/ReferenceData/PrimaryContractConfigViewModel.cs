using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HelloID.Vault.Core.Models.DTOs;
using HelloID.Vault.Core.Models.Entities;
using HelloID.Vault.Data.Repositories.Interfaces;
using HelloID.Vault.Services;
using HelloID.Vault.Services.Interfaces;
using HelloID.Vault.Management.Views.ReferenceData;

namespace HelloID.Vault.Management.ViewModels.ReferenceData;

/// <summary>
/// ViewModel for configuring primary contract determination rules.
/// </summary>
public partial class PrimaryContractConfigViewModel : ObservableObject
{
    private readonly IPrimaryContractConfigRepository _configRepository;
    private readonly ICustomFieldRepository _customFieldRepository;
    private readonly INavigationService _navigationService;
    private readonly IPersonService _personService;
    private List<PrimaryContractConfig> _originalConfig = new();
    private int _currentPreviewIndex = 0;

    public ObservableCollection<PrimaryContractConfigItemViewModel> ConfigItems { get; } = new();
    public ObservableCollection<string> AvailableFields { get; } = new();

    [ObservableProperty]
    private bool _isLoading = true;

    [ObservableProperty]
    private bool _isSaving = false;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string? _successMessage;

    [ObservableProperty]
    private PrimaryContractConfigItemViewModel? _selectedConfigItem;

    [ObservableProperty]
    private string? _selectedFieldToAdd;

    public PrimaryContractConfigViewModel(
        IPrimaryContractConfigRepository configRepository,
        ICustomFieldRepository customFieldRepository,
        INavigationService navigationService,
        IPersonService personService)
    {
        _configRepository = configRepository ?? throw new ArgumentNullException(nameof(configRepository));
        _customFieldRepository = customFieldRepository ?? throw new ArgumentNullException(nameof(customFieldRepository));
        _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
        _personService = personService ?? throw new ArgumentNullException(nameof(personService));
    }

    public async Task LoadAsync()
    {
        try
        {
            IsLoading = true;
            ErrorMessage = null;
            SuccessMessage = null;

            var configs = await _configRepository.GetAllAsync();
            // Store original configuration for cancel/restore functionality
            _originalConfig = configs.OrderBy(c => c.PriorityOrder).ToList();

            ConfigItems.Clear();

            foreach (var config in configs.OrderBy(c => c.PriorityOrder))
            {
                var item = new PrimaryContractConfigItemViewModel(config);
                item.PropertyChanged += ConfigItem_PropertyChanged;
                ConfigItems.Add(item);
            }

            // Update priority numbers based on current order
            UpdatePriorityNumbers();

            // Load available fields for dropdown
            await LoadAvailableFieldsAsync();

            // Initialize selected field
            SelectedFieldToAdd = AvailableFields.Any() ? AvailableFields.First() : null;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load configuration: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ConfigItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PrimaryContractConfigItemViewModel.IsActive) ||
            e.PropertyName == nameof(PrimaryContractConfigItemViewModel.SortOrder))
        {
            UpdatePriorityNumbers();
        }
    }

    private void UpdatePriorityNumbers()
    {
        var activeItems = ConfigItems.Where(item => item.IsActive).OrderBy(item => item.DisplayOrder).ToList();
        for (int i = 0; i < activeItems.Count; i++)
        {
            activeItems[i].PriorityNumber = i + 1;
        }
    }

    [RelayCommand]
    private void MoveUp(PrimaryContractConfigItemViewModel item)
    {
        if (item == null) return;

        var currentIndex = ConfigItems.IndexOf(item);
        if (currentIndex > 0)
        {
            ConfigItems.Move(currentIndex, currentIndex - 1);
            UpdatePriorityNumbers();
        }
    }

    [RelayCommand]
    private void MoveDown(PrimaryContractConfigItemViewModel item)
    {
        if (item == null) return;

        var currentIndex = ConfigItems.IndexOf(item);
        if (currentIndex < ConfigItems.Count - 1)
        {
            ConfigItems.Move(currentIndex, currentIndex + 1);
            UpdatePriorityNumbers();
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        try
        {
            IsSaving = true;
            ErrorMessage = null;
            SuccessMessage = null;

            // Validate that at least one field is active
            if (!ConfigItems.Any(item => item.IsActive))
            {
                ErrorMessage = "At least one field must be active for primary contract determination.";
                return;
            }

            // Separate configs into existing (to update), new (to add), and deleted (to remove)
            var originalFieldNames = _originalConfig.Select(c => c.FieldName).ToHashSet();
            var currentFieldNames = ConfigItems.Select(item => item.FieldName).ToHashSet();
            var configsToUpdate = new List<PrimaryContractConfig>();
            var configsToAdd = new List<PrimaryContractConfig>();
            var fieldsToDelete = originalFieldNames.Except(currentFieldNames).ToList();

            foreach (var item in ConfigItems)
            {
                var config = new PrimaryContractConfig
                {
                    FieldName = item.FieldName,
                    DisplayName = item.DisplayName,
                    SortOrder = item.SortOrder,
                    PriorityOrder = item.PriorityNumber,
                    IsActive = item.IsActive
                };

                if (originalFieldNames.Contains(item.FieldName))
                {
                    configsToUpdate.Add(config);
                }
                else
                {
                    configsToAdd.Add(config);
                }
            }

            // Delete removed configs
            if (fieldsToDelete.Any())
            {
                await _configRepository.DeleteConfigAsync(fieldsToDelete);
            }

            // Update existing configs
            if (configsToUpdate.Any())
            {
                await _configRepository.UpdateConfigAsync(configsToUpdate);
            }

            // Add new configs
            if (configsToAdd.Any())
            {
                await _configRepository.AddConfigAsync(configsToAdd);
            }

            SuccessMessage = "Configuration saved successfully!";

            // Update original config to match saved values (so Cancel won't restore old values)
            _originalConfig = ConfigItems.Select(item => new PrimaryContractConfig
            {
                FieldName = item.FieldName,
                DisplayName = item.DisplayName,
                SortOrder = item.SortOrder,
                PriorityOrder = item.PriorityNumber,
                IsActive = item.IsActive
            }).OrderBy(c => c.PriorityOrder).ToList();

            // Auto-clear success message after brief delay
            await Task.Delay(3000);
            SuccessMessage = null;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to save configuration: {ex.Message}";
        }
        finally
        {
            IsSaving = false;
        }
    }

    [RelayCommand]
    private async Task ResetToDefaultAsync()
    {
        try
        {
            IsSaving = true;
            ErrorMessage = null;
            SuccessMessage = null;

            var result = MessageBox.Show(
                "Are you sure you want to reset to default configuration? This will restore the original priority ordering.",
                "Reset Configuration",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                await _configRepository.ResetToDefaultAsync();
                await LoadAsync();
                SuccessMessage = "Configuration reset to defaults successfully!";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to reset configuration: {ex.Message}";
        }
        finally
        {
            IsSaving = false;
        }
    }

    [RelayCommand]
    private void NavigateBack()
    {
        // Restore original values when navigating back (Cancel functionality)
        RestoreOriginalValues();
        _navigationService.NavigateBack();
    }

    /// <summary>
    /// Loads available fields that can be added to the configuration.
    /// </summary>
    private async Task LoadAvailableFieldsAsync()
    {
        try
        {
            AvailableFields.Clear();

            // Core fields from contract details (using actual database column names)
            var coreFields = new[]
            {
                // Contract basic fields
                "fte", "hours_per_week", "percentage", "sequence", "start_date", "end_date",
                "external_id", "type_code", "type_description",

                // Person fields
                // "person_name", "person_external_id",

                // Manager fields
                "manager_person_id", "manager_person_name", "manager_external_id",

                // Location fields
                "location_external_id", "location_code", "location_name",

                // Cost Center fields
                "cost_center_external_id", "cost_center_code", "cost_center_name",

                // Cost Bearer fields
                "cost_bearer_external_id", "cost_bearer_code", "cost_bearer_name",

                // Employer fields
                "employer_external_id", "employer_code", "employer_name",

                // Team fields
                "team_external_id", "team_code", "team_name",

                // Department fields
                "department_external_id", "department_name", "department_code",
                "department_manager_person_id", "department_manager_name", "department_parent_external_id",
                "department_parent_department_name",

                // Division fields
                "division_external_id", "division_code", "division_name",

                // Title fields
                "title_external_id", "title_code", "title_name",

                // Organization fields
                "organization_external_id", "organization_code", "organization_name",

                // Contract status and derived fields
                // "contract_status", "contract_date_range"
            };

            var existingFields = ConfigItems.Select(item => item.FieldName).ToHashSet();

            // Add core fields that aren't already in config
            foreach (var field in coreFields.Where(f => !existingFields.Contains(f)))
            {
                AvailableFields.Add(field);
            }

            // Load custom fields for contracts
            var customFieldSchemas = await _customFieldRepository.GetSchemasAsync("contracts");
            var contractCustomFields = customFieldSchemas
                .Select(cf => cf.FieldKey)
                .Where(f => !existingFields.Contains(f));

            foreach (var field in contractCustomFields)
            {
                AvailableFields.Add(field);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load available fields: {ex.Message}";
        }
    }

    /// <summary>
    /// Gets display name for a field name.
    /// </summary>
    private string GetDisplayNameForField(string fieldName)
    {
        return fieldName.ToLowerInvariant() switch
        {
            // Contract basic fields
            "fte" => "FTE",
            "hours_per_week" => "Hours Per Week",
            "percentage" => "Percentage",
            "sequence" => "Sequence",
            "start_date" => "Start Date",
            "end_date" => "End Date",
            "contract_id" => "Contract ID",
            "external_id" => "External ID",
            "type_code" => "Type Code",
            "type_description" => "Type Description",

            // Person fields
            "person_name" => "Person Name",
            "person_external_id" => "Person External ID",

            // Manager fields
            "manager_person_id" => "Manager Person ID",
            "manager_person_name" => "Manager Name",
            "manager_external_id" => "Manager External ID",

            // Location fields
            "location_id" => "Location ID",
            "location_external_id" => "Location External ID",
            "location_code" => "Location Code",
            "location_name" => "Location Name",

            // Cost Center fields
            "cost_center_id" => "Cost Center ID",
            "cost_center_external_id" => "Cost Center External ID",
            "cost_center_code" => "Cost Center Code",
            "cost_center_name" => "Cost Center Name",

            // Cost Bearer fields
            "cost_bearer_id" => "Cost Bearer ID",
            "cost_bearer_external_id" => "Cost Bearer External ID",
            "cost_bearer_code" => "Cost Bearer Code",
            "cost_bearer_name" => "Cost Bearer Name",

            // Employer fields
            "employer_id" => "Employer ID",
            "employer_external_id" => "Employer External ID",
            "employer_code" => "Employer Code",
            "employer_name" => "Employer Name",

            // Team fields
            "team_id" => "Team ID",
            "team_external_id" => "Team External ID",
            "team_code" => "Team Code",
            "team_name" => "Team Name",

            // Department fields
            "department_id" => "Department ID",
            "department_external_id" => "Department External ID",
            "department_name" => "Department Name",
            "department_code" => "Department Code",
            "department_manager_person_id" => "Department Manager ID",
            "department_manager_name" => "Department Manager Name",
            "department_parent_external_id" => "Department Parent External ID",
            "department_parent_department_name" => "Department Parent Name",

            // Division fields
            "division_id" => "Division ID",
            "division_external_id" => "Division External ID",
            "division_code" => "Division Code",
            "division_name" => "Division Name",

            // Title fields
            "title_id" => "Title ID",
            "title_external_id" => "Title External ID",
            "title_code" => "Title Code",
            "title_name" => "Title Name",

            // Organization fields
            "organization_id" => "Organization ID",
            "organization_external_id" => "Organization External ID",
            "organization_code" => "Organization Code",
            "organization_name" => "Organization Name",

            // Contract status and derived fields
            "contract_status" => "Contract Status",
            "contract_date_range" => "Contract Date Range",

            _ => fieldName // For custom fields, use FieldKey as is
        };
    }

    /// <summary>
    /// Adds a new field to the configuration.
    /// </summary>
    [RelayCommand]
    private void AddField()
    {
        if (string.IsNullOrEmpty(SelectedFieldToAdd) ||
            string.IsNullOrWhiteSpace(SelectedFieldToAdd))
            return;

        // Check if field already exists
        if (ConfigItems.Any(item => item.FieldName == SelectedFieldToAdd))
        {
            ErrorMessage = "This field is already in the configuration.";
            return;
        }

        // Add new field
        var newField = new PrimaryContractConfigItemViewModel(
            new PrimaryContractConfig
            {
                FieldName = SelectedFieldToAdd,
                DisplayName = GetDisplayNameForField(SelectedFieldToAdd),
                SortOrder = "DESC",
                PriorityOrder = ConfigItems.Count + 1,
                IsActive = true
            });

        ConfigItems.Add(newField);
        UpdatePriorityNumbers();

        // Remove from available fields
        AvailableFields.Remove(SelectedFieldToAdd);
        SelectedFieldToAdd = AvailableFields.Any() ? AvailableFields.FirstOrDefault() : null;

        SuccessMessage = $"Field '{newField.DisplayName}' added successfully.";

        // Auto-clear success message after brief delay
        _ = Task.Delay(3000).ContinueWith(_ => SuccessMessage = null);
    }

    /// <summary>
    /// Deletes the selected field from the configuration.
    /// </summary>
    [RelayCommand]
    private void DeleteField(PrimaryContractConfigItemViewModel? item = null)
    {
        var itemToDelete = item ?? SelectedConfigItem;
        if (itemToDelete == null) return;

        // Ensure at least one field remains
        if (ConfigItems.Count <= 1)
        {
            ErrorMessage = "At least one field must remain in the configuration.";
            return;
        }

        var result = MessageBox.Show(
            $"Are you sure you want to delete '{itemToDelete.DisplayName}'?",
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            var deletedField = itemToDelete;

            // Add back to available fields
            if (!AvailableFields.Contains(deletedField.FieldName))
            {
                var sortedFields = AvailableFields.OrderBy(f => f).ToList();
                sortedFields.Add(deletedField.FieldName);
                sortedFields = sortedFields.OrderBy(f => f).ToList();

                AvailableFields.Clear();
                foreach (var field in sortedFields)
                {
                    AvailableFields.Add(field);
                }
            }

            ConfigItems.Remove(deletedField);
            UpdatePriorityNumbers();
            if (SelectedConfigItem == deletedField)
                SelectedConfigItem = null;

            SuccessMessage = $"Field '{deletedField.DisplayName}' deleted successfully.";

            // Auto-clear success message after brief delay
            _ = Task.Delay(3000).ContinueWith(_ => SuccessMessage = null);
        }
    }

    /// <summary>
    /// Opens the preview dialog to test the current configuration.
    /// </summary>
    [RelayCommand]
    private async Task RunPreviewAsync()
    {
        try
        {
            // Get a person with multiple contracts for preview
            var person = await _personService.GetPersonWithMostContractsAsync(_currentPreviewIndex);

            if (person == null)
            {
                // Wrap around to start
                _currentPreviewIndex = 0;
                person = await _personService.GetPersonWithMostContractsAsync(0);

                if (person == null)
                {
                    ErrorMessage = "No persons with contracts found in database.";
                    return;
                }
            }

            // Run preview
            var previewResult = await _personService.PreviewPrimaryContractAsync(person.PersonId!);

            if (previewResult == null)
            {
                ErrorMessage = $"No contracts found for person '{person.DisplayName}'.";
                return;
            }

            // Create and show dialog
            var dialog = new PrimaryContractPreviewDialog();
            var viewModel = new PrimaryContractPreviewViewModel();
            viewModel.LoadPreview(previewResult);
            dialog.SetViewModel(viewModel);
            dialog.Owner = Application.Current.MainWindow;
            dialog.ShowDialog();

            // Increment index for next click (will wrap via fallback logic)
            _currentPreviewIndex++;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to run preview: {ex.Message}";
        }
    }

    /// <summary>
    /// Restores the original configuration values (used when Cancel is clicked)
    /// </summary>
    private void RestoreOriginalValues()
    {
        if (_originalConfig == null || !_originalConfig.Any())
            return;

        ConfigItems.Clear();

        foreach (var config in _originalConfig.OrderBy(c => c.PriorityOrder))
        {
            var item = new PrimaryContractConfigItemViewModel(config);
            item.PropertyChanged += ConfigItem_PropertyChanged;
            ConfigItems.Add(item);
        }

        UpdatePriorityNumbers();
    }
}

/// <summary>
/// ViewModel for a single configuration item in the primary contract configuration.
/// </summary>
public class PrimaryContractConfigItemViewModel : ObservableObject
{
    private readonly PrimaryContractConfig _config;
    private int _displayOrder;
    private int _priorityNumber;

    public string FieldName => _config.FieldName;
    public string DisplayName => _config.DisplayName;

    public string SortOrder
    {
        get => _config.SortOrder;
        set
        {
            if (_config.SortOrder != value)
            {
                _config.SortOrder = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsActive
    {
        get => _config.IsActive;
        set
        {
            if (_config.IsActive != value)
            {
                _config.IsActive = value;
                OnPropertyChanged();
            }
        }
    }

    public int DisplayOrder
    {
        get => _displayOrder;
        set
        {
            if (_displayOrder != value)
            {
                _displayOrder = value;
                OnPropertyChanged();
            }
        }
    }

    public int PriorityNumber
    {
        get => _priorityNumber;
        set
        {
            if (_priorityNumber != value)
            {
                _priorityNumber = value;
                OnPropertyChanged();
            }
        }
    }

    public PrimaryContractConfigItemViewModel(PrimaryContractConfig config)
    {
        _config = config;
        _displayOrder = 0; // Will be set by parent ViewModel
        _priorityNumber = config.PriorityOrder;
    }
}