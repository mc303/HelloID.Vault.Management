using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HelloID.Vault.Core.Models.DTOs;
using HelloID.Vault.Services.Interfaces;
using HelloID.Vault.Data.Repositories.Interfaces;

namespace HelloID.Vault.Management.ViewModels.ReferenceData;

/// <summary>
/// Generic ViewModel for editing simple reference data tables.
/// Handles: Locations, Titles, CostCenters, CostBearers, Employers, Teams, Divisions, Organizations
/// </summary>
public partial class ReferenceDataEditViewModel : ObservableObject
{
    private readonly IReferenceDataService _service;
    private readonly ISourceSystemRepository _sourceSystemRepository;
    private readonly string _tableName;
    private readonly bool _isEditMode;

    [ObservableProperty]
    private string _windowTitle = string.Empty;

    [ObservableProperty]
    private string _externalId = string.Empty;

    [ObservableProperty]
    private string? _code;

    [ObservableProperty]
    private string? _name;

    [ObservableProperty]
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

    public ReferenceDataEditViewModel(
        IReferenceDataService service,
        ISourceSystemRepository sourceSystemRepository,
        string tableName,
        string? externalId = null,
        string? code = null,
        string? name = null,
        string? source = null)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _sourceSystemRepository = sourceSystemRepository ?? throw new ArgumentNullException(nameof(sourceSystemRepository));
        _tableName = tableName ?? throw new ArgumentNullException(nameof(tableName));
        _isEditMode = !string.IsNullOrEmpty(externalId);

        ExternalId = externalId ?? string.Empty;
        Code = code;
        Name = name;
        Source = source;
        IsExternalIdReadOnly = _isEditMode;

        WindowTitle = _isEditMode ? $"Edit {GetSingularName(_tableName)}" : $"Add {GetSingularName(_tableName)}";

        // Load source systems asynchronously
        _ = LoadSourceSystemsAsync();
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        ErrorMessage = null;

        // Validation
        if (string.IsNullOrWhiteSpace(ExternalId))
        {
            ErrorMessage = "External ID is required.";
            return;
        }

        if (string.IsNullOrWhiteSpace(Source))
        {
            ErrorMessage = "Source is required.";
            return;
        }

        try
        {
            if (_isEditMode)
            {
                await UpdateEntityAsync();
            }
            else
            {
                await CreateEntityAsync();
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

    private async Task CreateEntityAsync()
    {
        switch (_tableName)
        {
            case "Locations":
                await _service.CreateLocationAsync(new Core.Models.Entities.Location
                {
                    ExternalId = ExternalId,
                    Code = Code,
                    Name = Name,
                    Source = Source
                });
                break;

            case "Titles":
                await _service.CreateTitleAsync(new Core.Models.Entities.Title
                {
                    ExternalId = ExternalId,
                    Code = Code,
                    Name = Name,
                    Source = Source
                });
                break;

            case "CostCenters":
                await _service.CreateCostCenterAsync(new Core.Models.Entities.CostCenter
                {
                    ExternalId = ExternalId,
                    Code = Code,
                    Name = Name,
                    Source = Source
                });
                break;

            case "CostBearers":
                await _service.CreateCostBearerAsync(new Core.Models.Entities.CostBearer
                {
                    ExternalId = ExternalId,
                    Code = Code,
                    Name = Name,
                    Source = Source
                });
                break;

            case "Employers":
                await _service.CreateEmployerAsync(new Core.Models.Entities.Employer
                {
                    ExternalId = ExternalId,
                    Code = Code,
                    Name = Name,
                    Source = Source
                });
                break;

            case "Teams":
                await _service.CreateTeamAsync(new Core.Models.Entities.Team
                {
                    ExternalId = ExternalId,
                    Code = Code,
                    Name = Name,
                    Source = Source
                });
                break;

            case "Divisions":
                await _service.CreateDivisionAsync(new Core.Models.Entities.Division
                {
                    ExternalId = ExternalId,
                    Code = Code,
                    Name = Name,
                    Source = Source
                });
                break;

            case "Organizations":
                await _service.CreateOrganizationAsync(new Core.Models.Entities.Organization
                {
                    ExternalId = ExternalId,
                    Code = Code,
                    Name = Name,
                    Source = Source
                });
                break;

            default:
                throw new InvalidOperationException($"Unknown table name: {_tableName}");
        }
    }

    private async Task UpdateEntityAsync()
    {
        switch (_tableName)
        {
            case "Locations":
                await _service.UpdateLocationAsync(new Core.Models.Entities.Location
                {
                    ExternalId = ExternalId,
                    Code = Code,
                    Name = Name,
                    Source = Source
                });
                break;

            case "Titles":
                await _service.UpdateTitleAsync(new Core.Models.Entities.Title
                {
                    ExternalId = ExternalId,
                    Code = Code,
                    Name = Name,
                    Source = Source
                });
                break;

            case "CostCenters":
                await _service.UpdateCostCenterAsync(new Core.Models.Entities.CostCenter
                {
                    ExternalId = ExternalId,
                    Code = Code,
                    Name = Name,
                    Source = Source
                });
                break;

            case "CostBearers":
                await _service.UpdateCostBearerAsync(new Core.Models.Entities.CostBearer
                {
                    ExternalId = ExternalId,
                    Code = Code,
                    Name = Name,
                    Source = Source
                });
                break;

            case "Employers":
                await _service.UpdateEmployerAsync(new Core.Models.Entities.Employer
                {
                    ExternalId = ExternalId,
                    Code = Code,
                    Name = Name,
                    Source = Source
                });
                break;

            case "Teams":
                await _service.UpdateTeamAsync(new Core.Models.Entities.Team
                {
                    ExternalId = ExternalId,
                    Code = Code,
                    Name = Name,
                    Source = Source
                });
                break;

            case "Divisions":
                await _service.UpdateDivisionAsync(new Core.Models.Entities.Division
                {
                    ExternalId = ExternalId,
                    Code = Code,
                    Name = Name,
                    Source = Source
                });
                break;

            case "Organizations":
                await _service.UpdateOrganizationAsync(new Core.Models.Entities.Organization
                {
                    ExternalId = ExternalId,
                    Code = Code,
                    Name = Name,
                    Source = Source
                });
                break;

            default:
                throw new InvalidOperationException($"Unknown table name: {_tableName}");
        }
    }

    private string GetSingularName(string tableName) => tableName switch
    {
        "Locations" => "Location",
        "Titles" => "Title",
        "CostCenters" => "Cost Center",
        "CostBearers" => "Cost Bearer",
        "Employers" => "Employer",
        "Teams" => "Team",
        "Divisions" => "Division",
        "Organizations" => "Organization",
        _ => tableName
    };

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
