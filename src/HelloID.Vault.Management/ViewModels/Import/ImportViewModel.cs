using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HelloID.Vault.Management.Views.Import;
using HelloID.Vault.Services.Interfaces;
using Microsoft.Win32;

namespace HelloID.Vault.Management.ViewModels.Import;

/// <summary>
/// ViewModel for the Vault Import view.
/// </summary>
public partial class ImportViewModel : ObservableObject
{
    private readonly IVaultImportService _importService;
    private readonly IUserPreferencesService _userPreferencesService;
    private readonly IReferenceDataService _referenceDataService;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartImportCommand))]
    [NotifyCanExecuteChangedFor(nameof(StartCompanyImportCommand))]
    private string? _selectedFilePath;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartImportCommand))]
    [NotifyCanExecuteChangedFor(nameof(StartCompanyImportCommand))]
    private bool _isImporting;

    [ObservableProperty]
    private string _currentOperation = string.Empty;

    [ObservableProperty]
    private int _progressPercentage;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _importCompleted;

    [ObservableProperty]
    private ImportResult? _importResult;

    [ObservableProperty]
    private PrimaryManagerLogic _selectedPrimaryManagerLogic = PrimaryManagerLogic.FromJson;

    public ImportViewModel(IVaultImportService importService, IUserPreferencesService userPreferencesService, IReferenceDataService referenceDataService)
    {
        _importService = importService ?? throw new ArgumentNullException(nameof(importService));
        _userPreferencesService = userPreferencesService ?? throw new ArgumentNullException(nameof(userPreferencesService));
        _referenceDataService = referenceDataService ?? throw new ArgumentNullException(nameof(referenceDataService));
    }

    /// <summary>
    /// Opens a file dialog to select a vault.json file.
    /// </summary>
    [RelayCommand]
    private void SelectFile()
    {
        var openFileDialog = new OpenFileDialog
        {
            Title = "Select vault.json file",
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            CheckFileExists = true
        };

        if (openFileDialog.ShowDialog() == true)
        {
            SelectedFilePath = openFileDialog.FileName;
            StatusMessage = $"Selected: {System.IO.Path.GetFileName(SelectedFilePath)}";
            ImportCompleted = false;
            ImportResult = null;
        }
    }

    /// <summary>
    /// Starts the import process for the selected file.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanStartImport))]
    private async Task StartImportAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedFilePath))
        {
            StatusMessage = "Please select a file first.";
            return;
        }

        try
        {
            // Check if database already has data
            bool hasData = await _importService.HasDataAsync();
            bool createBackup = false;

            if (hasData)
            {
                // Show custom 3-option dialog
                var dialog = new ConfirmImportDialog
                {
                    Owner = Application.Current.MainWindow
                };

                var dialogResult = dialog.ShowDialog();

                if (dialogResult != true)
                {
                    StatusMessage = "Import cancelled by user.";
                    return;
                }

                // Handle user's choice
                switch (dialog.Result)
                {
                    case ImportConfirmResult.BackupAndOverwrite:
                        createBackup = true;
                        break;

                    case ImportConfirmResult.OverwriteWithoutBackup:
                        createBackup = false;
                        // Delete database manually without backup
                        await DeleteDatabaseAsync();
                        break;

                    case ImportConfirmResult.Cancel:
                    default:
                        StatusMessage = "Import cancelled by user.";
                        return;
                }
            }

            IsImporting = true;
            ImportCompleted = false;
            ImportResult = null;
            ProgressPercentage = 0;
            StatusMessage = "Starting import...";

            var progress = new Progress<ImportProgress>(p =>
            {
                CurrentOperation = p.CurrentOperation;
                ProgressPercentage = p.Percentage;
            });

            ImportResult = await _importService.ImportAsync(SelectedFilePath, SelectedPrimaryManagerLogic, createBackup, progress);

            ImportCompleted = true;

            if (ImportResult.Success)
            {
                // Save the selected logic for future use (e.g., default in Primary Manager Admin)
                _userPreferencesService.LastPrimaryManagerLogic = SelectedPrimaryManagerLogic;

                // Reset column visibility initialization flag so empty columns will be hidden on next view load
                _userPreferencesService.ContractsColumnVisibilityInitialized = false;

                StatusMessage = $"Import completed successfully in {ImportResult.Duration.TotalSeconds:F1} seconds!";
            }
            else
            {
                StatusMessage = $"Import failed: {ImportResult.ErrorMessage}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            ImportCompleted = true;
        }
        finally
        {
            IsImporting = false;
        }
    }

    private bool CanStartImport() => !IsImporting && !string.IsNullOrWhiteSpace(SelectedFilePath);

    private async Task DeleteDatabaseAsync()
    {
        await _importService.DeleteDatabaseAsync();
    }

    /// <summary>
    /// Starts the company-only import process for the selected file.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanStartImport))]
    private async Task StartCompanyImportAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedFilePath))
        {
            StatusMessage = "Please select a file first.";
            return;
        }

        try
        {
            // Check if database already has data
            bool hasData = await _importService.HasDataAsync();
            bool createBackup = false;

            if (hasData)
            {
                // Show company import dialog
                var dialog = new CompanyImportDialog
                {
                    Owner = Application.Current.MainWindow
                };

                var dialogResult = dialog.ShowDialog();

                if (dialogResult != true)
                {
                    StatusMessage = "Company import cancelled by user.";
                    return;
                }

                // Handle user's choice
                switch (dialog.Result)
                {
                    case ImportConfirmResult.BackupAndOverwrite:
                        createBackup = true;
                        break;

                    case ImportConfirmResult.OverwriteWithoutBackup:
                        createBackup = false;
                        // Delete database manually without backup
                        await DeleteDatabaseAsync();
                        break;

                    case ImportConfirmResult.Cancel:
                    default:
                        StatusMessage = "Company import cancelled by user.";
                        return;
                }
            }

            IsImporting = true;
            ImportCompleted = false;
            ImportResult = null;
            ProgressPercentage = 0;
            StatusMessage = "Starting company data import...";

            var progress = new Progress<ImportProgress>(p =>
            {
                CurrentOperation = p.CurrentOperation;
                ProgressPercentage = p.Percentage;
            });

            ImportResult = await _importService.ImportCompanyOnlyAsync(SelectedFilePath, SelectedPrimaryManagerLogic, createBackup, progress);

            ImportCompleted = true;

            if (ImportResult.Success)
            {
                // Save the selected logic for future use (e.g., default in Primary Manager Admin)
                _userPreferencesService.LastPrimaryManagerLogic = SelectedPrimaryManagerLogic;

                StatusMessage = $"Company data import completed successfully in {ImportResult.Duration.TotalSeconds:F1} seconds!";
            }
            else
            {
                StatusMessage = $"Company import failed: {ImportResult.ErrorMessage}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            ImportCompleted = true;
        }
        finally
        {
            IsImporting = false;
        }
    }

    /// <summary>
    /// Resets the import view to start a new import.
    /// </summary>
    [RelayCommand]
    private void Reset()
    {
        SelectedFilePath = null;
        StatusMessage = null;
        CurrentOperation = string.Empty;
        ProgressPercentage = 0;
        ImportCompleted = false;
        ImportResult = null;
    }
}
