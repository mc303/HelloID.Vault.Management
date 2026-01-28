using System.ComponentModel.DataAnnotations;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HelloID.Vault.Core.Models.Entities;
using HelloID.Vault.Data.Repositories.Interfaces;

namespace HelloID.Vault.Management.ViewModels.ReferenceData;

/// <summary>
/// ViewModel for adding or editing custom field schemas.
/// </summary>
public partial class CustomFieldEditViewModel : ObservableValidator
{
    private readonly ICustomFieldRepository _customFieldRepository;
    private bool _isEditMode;
    private string? _originalFieldKey;

    [ObservableProperty]
    private string _windowTitle = "Add Custom Field";

    [ObservableProperty]
    [Required(ErrorMessage = "Table Name is required.")]
    [StringLength(50, ErrorMessage = "Table Name cannot exceed 50 characters.")]
    private string _tableName = "persons";

    [ObservableProperty]
    [Required(ErrorMessage = "Field Key is required.")]
    [StringLength(100, ErrorMessage = "Field Key cannot exceed 100 characters.")]
    private string _fieldKey = string.Empty;

    [ObservableProperty]
    [Required(ErrorMessage = "Display Name is required.")]
    [StringLength(200, ErrorMessage = "Display Name cannot exceed 200 characters.")]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private int _sortOrder;

    [ObservableProperty]
    private string? _validationRegex;

    [ObservableProperty]
    private string? _helpText;

    [ObservableProperty]
    private bool _isFieldKeyReadOnly;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isSaving;

    [ObservableProperty]
    private string? _savingMessage;

    public event Action<bool>? CloseRequested;

    public CustomFieldEditViewModel(ICustomFieldRepository customFieldRepository)
    {
        _customFieldRepository = customFieldRepository ?? throw new ArgumentNullException(nameof(customFieldRepository));
        _isEditMode = false;
        IsFieldKeyReadOnly = false;
    }

    public void LoadExistingField(CustomFieldSchema schema)
    {
        _isEditMode = true;
        _originalFieldKey = schema.FieldKey;
        WindowTitle = "Edit Custom Field";

        TableName = schema.TableName;
        FieldKey = schema.FieldKey;
        DisplayName = schema.DisplayName;
        SortOrder = schema.SortOrder;
        ValidationRegex = schema.ValidationRegex;
        HelpText = schema.HelpText;

        IsFieldKeyReadOnly = true;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
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

        try
        {
            IsSaving = true;

            // Determine entity type for progress message
            var entityType = TableName == "persons" ? "persons" : "contracts";
            SavingMessage = _isEditMode
                ? $"Updating custom field for all {entityType}..."
                : $"Creating custom field for all {entityType}...";

            var schema = new CustomFieldSchema
            {
                TableName = TableName,
                FieldKey = FieldKey,
                DisplayName = DisplayName,
                SortOrder = SortOrder,
                ValidationRegex = string.IsNullOrWhiteSpace(ValidationRegex) ? null : ValidationRegex,
                HelpText = string.IsNullOrWhiteSpace(HelpText) ? null : HelpText
            };

            if (_isEditMode)
            {
                await _customFieldRepository.UpdateSchemaAsync(schema);
            }
            else
            {
                await _customFieldRepository.InsertSchemaAsync(schema);
            }

            CloseRequested?.Invoke(true);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error saving custom field: {ex.Message}";
        }
        finally
        {
            IsSaving = false;
            SavingMessage = null;
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        CloseRequested?.Invoke(false);
    }
}
