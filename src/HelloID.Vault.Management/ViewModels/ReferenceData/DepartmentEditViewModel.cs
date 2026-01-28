using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HelloID.Vault.Core.Models.DTOs;
using HelloID.Vault.Core.Models.Entities;
using HelloID.Vault.Services.Interfaces;
using HelloID.Vault.Data.Repositories.Interfaces;

namespace HelloID.Vault.Management.ViewModels.ReferenceData;

/// <summary>
/// ViewModel for editing Department entities.
/// Handles unique Department fields: DisplayName, ParentExternalId, ManagerPersonId
/// </summary>
public partial class DepartmentEditViewModel : ObservableValidator
{
    private readonly IReferenceDataService _service;
    private readonly ISourceSystemRepository _sourceSystemRepository;
    private readonly bool _isEditMode;

    [ObservableProperty]
    private string _windowTitle = string.Empty;

    [ObservableProperty]
    [Required(ErrorMessage = "External ID is required.")]
    [StringLength(200, ErrorMessage = "External ID cannot exceed 200 characters.")]
    private string _externalId = string.Empty;

    [ObservableProperty]
    [Required(ErrorMessage = "Display Name is required.")]
    [StringLength(200, ErrorMessage = "Display Name cannot exceed 200 characters.")]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private string? _code;

    [ObservableProperty]
    private string? _parentExternalId;

    [ObservableProperty]
    private string? _managerPersonId;

    [ObservableProperty]
    [Required(ErrorMessage = "Source is required.")]
    [StringLength(50, ErrorMessage = "Source cannot exceed 50 characters.")]
    private string? _source;

    [ObservableProperty]
    private ObservableCollection<SourceSystemDto> _sourceSystems = new();

    [ObservableProperty]
    private SourceSystemDto? _selectedSourceSystem;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isExternalIdReadOnly;

    public event Action<bool>? CloseRequested;

    public DepartmentEditViewModel(
        IReferenceDataService service,
        ISourceSystemRepository sourceSystemRepository,
        Department? department = null)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _sourceSystemRepository = sourceSystemRepository ?? throw new ArgumentNullException(nameof(sourceSystemRepository));
        _isEditMode = department != null;

        if (department != null)
        {
            ExternalId = department.ExternalId;
            DisplayName = department.DisplayName;
            Code = department.Code;
            ParentExternalId = department.ParentExternalId;
            ManagerPersonId = department.ManagerPersonId;
            Source = department.Source;
        }

        IsExternalIdReadOnly = _isEditMode;
        WindowTitle = _isEditMode ? "Edit Department" : "Add Department";

        // Load source systems asynchronously
        _ = LoadSourceSystemsAsync();
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
            var department = new Department
            {
                ExternalId = ExternalId,
                DisplayName = DisplayName,
                Code = Code,
                ParentExternalId = ParentExternalId,
                ManagerPersonId = ManagerPersonId,
                Source = Source
            };

            if (_isEditMode)
            {
                await _service.UpdateDepartmentAsync(department);
            }
            else
            {
                await _service.CreateDepartmentAsync(department);
            }

            CloseRequested?.Invoke(true);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error saving: {ex.Message}";
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        CloseRequested?.Invoke(false);
    }

    private async Task LoadSourceSystemsAsync()
    {
        try
        {
            var sourceSystems = await _sourceSystemRepository.GetAllAsync();
            SourceSystems.Clear();
            foreach (var source in sourceSystems.OrderBy(s => s.DisplayName))
            {
                SourceSystems.Add(source);
            }

            // Set SelectedSourceSystem after loading
            if (!string.IsNullOrWhiteSpace(Source))
            {
                SelectedSourceSystem = SourceSystems.FirstOrDefault(s => s.SystemId == Source);
            }
        }
        catch
        {
            // Silently fail - source systems are critical, but don't block UI
        }
    }

    partial void OnSelectedSourceSystemChanged(SourceSystemDto? value)
    {
        Source = value?.SystemId;
    }
}
