using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HelloID.Vault.Core.Models.Entities;
using HelloID.Vault.Core.Models.DTOs;
using HelloID.Vault.Data.Repositories.Interfaces;

namespace HelloID.Vault.Management.ViewModels.ReferenceData;

/// <summary>
/// ViewModel for adding or editing a source system.
/// </summary>
public partial class SourceSystemEditViewModel : ObservableObject
{
    private readonly ISourceSystemRepository _sourceSystemRepository;
    private readonly bool _isEditMode;

    [ObservableProperty]
    private string _windowTitle = "Add Source System";

    [ObservableProperty]
    private string _systemId = string.Empty;

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private string _identificationKey = string.Empty;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isSaving;

    public event Action<bool>? CloseRequested;

    /// <summary>
    /// Constructor for adding a new source system.
    /// </summary>
    public SourceSystemEditViewModel(ISourceSystemRepository sourceSystemRepository)
    {
        _sourceSystemRepository = sourceSystemRepository ?? throw new ArgumentNullException(nameof(sourceSystemRepository));
        _isEditMode = false;
        WindowTitle = "Add Source System";

        // Auto-generate SystemId for new source systems
        SystemId = Guid.NewGuid().ToString();
        IdentificationKey = SystemId;  // Same as SystemId
    }

    /// <summary>
    /// Constructor for editing an existing source system.
    /// </summary>
    public SourceSystemEditViewModel(ISourceSystemRepository sourceSystemRepository, SourceSystemDto existingSource)
    {
        _sourceSystemRepository = sourceSystemRepository ?? throw new ArgumentNullException(nameof(sourceSystemRepository));
        _isEditMode = true;
        WindowTitle = "Edit Source System";

        SystemId = existingSource.SystemId;
        DisplayName = existingSource.DisplayName;
        IdentificationKey = existingSource.IdentificationKey;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (!ValidateInputs())
            return;

        try
        {
            IsSaving = true;
            ErrorMessage = null;

            if (_isEditMode)
            {
                // Update existing source system
                var sourceSystem = new SourceSystem
                {
                    SystemId = SystemId,
                    DisplayName = DisplayName,
                    IdentificationKey = IdentificationKey
                };

                await _sourceSystemRepository.UpdateAsync(sourceSystem);
            }
            else
            {
                // Create new source system using the already-generated SystemId
                var sourceSystem = new SourceSystem
                {
                    SystemId = SystemId,
                    DisplayName = DisplayName,
                    IdentificationKey = IdentificationKey
                };

                await _sourceSystemRepository.InsertAsync(sourceSystem);
            }

            CloseRequested?.Invoke(true);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error saving source system: {ex.Message}";
        }
        finally
        {
            IsSaving = false;
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        CloseRequested?.Invoke(false);
    }

    private bool ValidateInputs()
    {
        if (string.IsNullOrWhiteSpace(DisplayName))
        {
            ErrorMessage = "Display Name is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(IdentificationKey))
        {
            ErrorMessage = "Identification Key is required.";
            return false;
        }

        return true;
    }
}
