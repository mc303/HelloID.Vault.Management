using System.IO;
using System.Text.Json;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HelloID.Vault.Core.Models.Json;
using HelloID.Vault.Data.Connection;
using HelloID.Vault.Management.Views.Import;
using HelloID.Vault.Services.Anonymization.Models;
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
    private readonly IVaultAnonymizerService? _anonymizerService;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartImportCommand))]
    [NotifyCanExecuteChangedFor(nameof(StartCompanyImportCommand))]
    [NotifyCanExecuteChangedFor(nameof(StartPersonSelectionImportCommand))]
    private string? _selectedFilePath;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartImportCommand))]
    [NotifyCanExecuteChangedFor(nameof(StartCompanyImportCommand))]
    [NotifyCanExecuteChangedFor(nameof(StartPersonSelectionImportCommand))]
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

    // Anonymization properties
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartImportCommand))]
    private bool _anonymizeBeforeImport;

    [ObservableProperty]
    private string _anonymizationStatus = string.Empty;

    [ObservableProperty]
    private int _anonymizationProgress;

    [ObservableProperty]
    private bool _showAdvancedAnonymizationOptions;

    [ObservableProperty]
    private string? _customBusinessEmailDomain;

    [ObservableProperty]
    private bool _useConsistentBusinessDomain = true;

    [ObservableProperty]
    private bool _useMultiEmployerDomains = true;

    [ObservableProperty]
    private AnonymizationResult? _anonymizationResult;

    [ObservableProperty]
    private int _nameSharingMode = 1;

    [ObservableProperty]
    private bool _limitDatasetSize;

    [ObservableProperty]
    private int _maxPersonsToImport = 10;

    [ObservableProperty]
    private string _seed = "default";

    public ImportViewModel(
        IVaultImportService importService,
        IUserPreferencesService userPreferencesService,
        IReferenceDataService referenceDataService,
        IVaultAnonymizerService? anonymizerService = null)
    {
        _importService = importService ?? throw new ArgumentNullException(nameof(importService));
        _userPreferencesService = userPreferencesService ?? throw new ArgumentNullException(nameof(userPreferencesService));
        _referenceDataService = referenceDataService ?? throw new ArgumentNullException(nameof(referenceDataService));
        _anonymizerService = anonymizerService;
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

        string filePathToImport = SelectedFilePath;

        try
        {
            // Step 1: Anonymize if requested
            if (AnonymizeBeforeImport)
            {
                if (_anonymizerService == null)
                {
                    StatusMessage = "Anonymization service is not available.";
                    return;
                }

                IsImporting = true;
                AnonymizationStatus = "Anonymizing data...";
                AnonymizationProgress = 0;
                AnonymizationResult = null;

                var options = BuildAnonymizationOptions();
                
                // Generate output file path: original_directory/originalname_anonymized.json
                var directory = System.IO.Path.GetDirectoryName(SelectedFilePath) ?? System.IO.Path.GetTempPath();
                var fileNameWithoutExt = System.IO.Path.GetFileNameWithoutExtension(SelectedFilePath);
                var outputFilePath = System.IO.Path.Combine(directory, $"{fileNameWithoutExt}_anonymized.json");

                var anonymizationProgress = new Progress<AnonymizationProgress>(p =>
                {
                    AnonymizationStatus = p.CurrentPhase;
                    AnonymizationProgress = p.Percentage;
                    CurrentOperation = $"Anonymizing: {p.CurrentPhase}";
                    ProgressPercentage = p.Percentage / 2; // Anonymization is first half
                });

                try
                {
                    AnonymizationResult = await _anonymizerService.AnonymizeAsync(
                        SelectedFilePath, outputFilePath, options, anonymizationProgress);

                    if (!AnonymizationResult.Success)
                    {
                        StatusMessage = $"Anonymization failed: {AnonymizationResult.ErrorMessage}";
                        ImportCompleted = true;
                        return;
                    }

                    filePathToImport = AnonymizationResult.OutputFilePath ?? outputFilePath;
                    var totalRecords = GetTotalAnonymizedRecords(AnonymizationResult);
                    AnonymizationStatus = $"Anonymization completed. {totalRecords} records processed.";
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Anonymization error: {ex.Message}";
                    ImportCompleted = true;
                    return;
                }
            }

            // Step 2: Check if database already has data
            bool hasData = await _importService.HasDataAsync();
            bool createBackup = false;

            if (hasData)
            {
                // Show custom 3-option dialog
                var dialog = new ConfirmImportDialog(_importService.DatabaseType)
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
                        // Delete database manually without backup (skip for Turso - import handles it)
                        if (_importService.DatabaseType != DatabaseType.Turso)
                        {
                            await DeleteDatabaseAsync();
                        }
                        break;

                    case ImportConfirmResult.Cancel:
                    default:
                        StatusMessage = "Import cancelled by user.";
                        return;
                }
            }

            if (!AnonymizeBeforeImport)
            {
                IsImporting = true;
            }

            ImportCompleted = false;
            ImportResult = null;
            StatusMessage = AnonymizeBeforeImport ? "Starting import (anonymized data)..." : "Starting import...";

            var progress = new Progress<ImportProgress>(p =>
            {
                CurrentOperation = p.CurrentOperation;
                // If anonymization was done, import progress is second half
                ProgressPercentage = AnonymizeBeforeImport ? 50 + (p.Percentage / 2) : p.Percentage;
            });

            ImportResult = await _importService.ImportAsync(filePathToImport, SelectedPrimaryManagerLogic, createBackup, progress);

            ImportCompleted = true;

            if (ImportResult.Success)
            {
                // Save the selected logic for future use (e.g., default in Primary Manager Admin)
                _userPreferencesService.LastPrimaryManagerLogic = SelectedPrimaryManagerLogic;

                // Reset column visibility initialization flag so empty columns will be hidden on next view load
                _userPreferencesService.ContractsColumnVisibilityInitialized = false;

                var durationMsg = $"Import completed successfully in {ImportResult.Duration.TotalSeconds:F1} seconds!";
                if (AnonymizeBeforeImport && AnonymizationResult != null)
                {
                    var totalRecords = GetTotalAnonymizedRecords(AnonymizationResult);
                    StatusMessage = $"{durationMsg} (Anonymized {totalRecords} records)";
                }
                else
                {
                    StatusMessage = durationMsg;
                }
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

    /// <summary>
    /// Calculates total anonymized records from the result.
    /// </summary>
    private static int GetTotalAnonymizedRecords(AnonymizationResult result)
    {
        return result.PersonsAnonymized +
               result.PersonExternalIdsAnonymized +
               result.BusinessEmailsAnonymized +
               result.PersonalEmailsAnonymized +
               result.ContactsAnonymized +
               result.ManagersAnonymized +
               result.DepartmentsAnonymized +
               result.LocationsAnonymized +
               result.EmployersAnonymized +
               result.CostCentersAnonymized +
               result.CostBearersAnonymized +
               result.TeamsAnonymized +
               result.DivisionsAnonymized +
               result.TitlesAnonymized +
               result.OrganizationsAnonymized;
    }

    /// <summary>
    /// Builds anonymization options from current UI settings.
    /// </summary>
    private AnonymizationOptions BuildAnonymizationOptions()
    {
        return new AnonymizationOptions
        {
            Locale = AnonymizationLocale.Dutch, // European pool is used for Unique mode
            UseConsistentBusinessDomain = UseConsistentBusinessDomain,
            UseMultiEmployerDomains = UseMultiEmployerDomains,
            CustomBusinessEmailDomain = CustomBusinessEmailDomain,
            KeepAnonymizedFile = true,
            NameSharingMode = (NameSharingMode)NameSharingMode,
            ForeignNamePercentage = 0, // Not needed - European pool includes all locales
            ForeignNameMix = ForeignNameMix.EasternEuropean | ForeignNameMix.WesternEuropean, // Not needed - European pool includes all
            MaxPersonsToImport = LimitDatasetSize ? MaxPersonsToImport : 0,
            Seed = Seed
        };
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
                var dialog = new CompanyImportDialog(_importService.DatabaseType)
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
                        // Delete database manually without backup (skip for Turso - import handles it)
                        if (_importService.DatabaseType != DatabaseType.Turso)
                        {
                            await DeleteDatabaseAsync();
                        }
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
    /// Starts the import process with person selection.
    /// Opens a dialog to select which persons to import, then imports company data + selected persons.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanStartImport))]
    private async Task StartPersonSelectionImportAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedFilePath))
        {
            StatusMessage = "Please select a file first.";
            return;
        }

        try
        {
            // Step 1: Load vault.json and extract person list
            StatusMessage = "Loading persons from vault.json...";
            var json = await File.ReadAllTextAsync(SelectedFilePath);
            var vaultData = JsonSerializer.Deserialize<VaultRoot>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true
            });

            if (vaultData?.Persons == null || vaultData.Persons.Count == 0)
            {
                StatusMessage = "No persons found in vault.json.";
                return;
            }

            // Step 2: Show person selection dialog
            var selectionViewModel = new ViewModels.Import.PersonSelectionViewModel();
            selectionViewModel.LoadPersons(vaultData.Persons.Select(p => (p.PersonId, p.DisplayName, p.ExternalId)));

            var dialog = new PersonSelectionDialog(selectionViewModel)
            {
                Owner = Application.Current.MainWindow
            };

            var dialogResult = dialog.ShowDialog();
            if (dialogResult != true)
            {
                StatusMessage = "Import cancelled by user.";
                return;
            }

            var selectedPersonIds = dialog.GetSelectedPersonIds();
            if (selectedPersonIds.Count == 0)
            {
                StatusMessage = "No persons selected. Import cancelled.";
                return;
            }

            var cascadeImportManagers = dialog.CascadeImportManagers;
            var clearMissingManagers = dialog.ClearMissingManagerReferences;

            // Cascade: expand selection to include referenced managers
            if (cascadeImportManagers)
            {
                StatusMessage = "Resolving manager references for cascade import...";

                var managerMap = new Dictionary<string, List<string>>();
                foreach (var vp in vaultData.Persons)
                {
                    var managers = new List<string>();

                    if (!string.IsNullOrWhiteSpace(vp.PrimaryManager?.PersonId))
                        managers.Add(vp.PrimaryManager.PersonId);

                    foreach (var vc in vp.Contracts)
                    {
                        if (!string.IsNullOrWhiteSpace(vc.Manager?.PersonId))
                            managers.Add(vc.Manager.PersonId);
                    }

                    managerMap[vp.PersonId] = managers;
                }

                var allPersonIds = new HashSet<string>(vaultData.Persons.Select(p => p.PersonId));
                var queue = new Queue<string>(selectedPersonIds);
                var visited = new HashSet<string>(selectedPersonIds);
                int addedCount = 0;

                while (queue.Count > 0)
                {
                    var currentId = queue.Dequeue();

                    if (!managerMap.TryGetValue(currentId, out var managers))
                        continue;

                    foreach (var managerId in managers)
                    {
                        if (visited.Contains(managerId))
                            continue;

                        if (allPersonIds.Contains(managerId))
                        {
                            visited.Add(managerId);
                            queue.Enqueue(managerId);
                            selectedPersonIds.Add(managerId);
                            addedCount++;
                        }
                    }
                }

                if (addedCount > 0)
                {
                    StatusMessage = $"Cascade: added {addedCount} manager(s) to selection ({selectedPersonIds.Count} total).";
                    await Task.Delay(1500);
                }
            }

            // Step 3: Check if database already has data
            bool hasData = await _importService.HasDataAsync();
            bool createBackup = false;

            if (hasData)
            {
                var confirmDialog = new ConfirmImportDialog(_importService.DatabaseType)
                {
                    Owner = Application.Current.MainWindow
                };

                var confirmResult = confirmDialog.ShowDialog();
                if (confirmResult != true)
                {
                    StatusMessage = "Import cancelled by user.";
                    return;
                }

                switch (confirmDialog.Result)
                {
                    case ImportConfirmResult.BackupAndOverwrite:
                        createBackup = true;
                        break;
                    case ImportConfirmResult.OverwriteWithoutBackup:
                        createBackup = false;
                        if (_importService.DatabaseType != DatabaseType.Turso)
                        {
                            await DeleteDatabaseAsync();
                        }
                        break;
                    case ImportConfirmResult.Cancel:
                    default:
                        StatusMessage = "Import cancelled by user.";
                        return;
                }
            }

            // Step 4: Import with selected persons
            IsImporting = true;
            ImportCompleted = false;
            ImportResult = null;
            ProgressPercentage = 0;
            StatusMessage = $"Importing {selectedPersonIds.Count} selected persons + company data...";

            var progress = new Progress<ImportProgress>(p =>
            {
                CurrentOperation = p.CurrentOperation;
                ProgressPercentage = p.Percentage;
            });

            ImportResult = await _importService.ImportWithPersonSelectionAsync(
                SelectedFilePath, selectedPersonIds, SelectedPrimaryManagerLogic, createBackup, progress);

            // Clear dangling manager references if option is enabled
            if (ImportResult.Success && clearMissingManagers)
            {
                StatusMessage = "Clearing dangling manager references...";
                await _importService.ClearMissingManagerReferencesAsync(progress);
            }

            ImportCompleted = true;

            if (ImportResult.Success)
            {
                _userPreferencesService.LastPrimaryManagerLogic = SelectedPrimaryManagerLogic;
                _userPreferencesService.ContractsColumnVisibilityInitialized = false;
                StatusMessage = $"Import completed successfully in {ImportResult.Duration.TotalSeconds:F1} seconds! ({selectedPersonIds.Count} persons selected)";
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
        AnonymizationStatus = string.Empty;
        AnonymizationProgress = 0;
        AnonymizationResult = null;
    }
}
