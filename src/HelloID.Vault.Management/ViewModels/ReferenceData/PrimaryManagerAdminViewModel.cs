using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HelloID.Vault.Core.Models.DTOs;
using HelloID.Vault.Services.Interfaces;

namespace HelloID.Vault.Management.ViewModels.ReferenceData;

/// <summary>
/// ViewModel for Primary Manager administration - refresh all primary managers.
/// </summary>
public partial class PrimaryManagerAdminViewModel : ObservableObject
{
    private readonly IPrimaryManagerService _primaryManagerService;
    private readonly IUserPreferencesService _userPreferencesService;
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    private PrimaryManagerLogic _selectedLogic = PrimaryManagerLogic.DepartmentBased;

    [ObservableProperty]
    private bool _isRefreshing;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _isRecalculatingWithContractRules;

    [ObservableProperty]
    private string? _recalculateContractRulesStatusMessage;

    [ObservableProperty]
    private int _totalPersons;

    [ObservableProperty]
    private int _personsWithManager;

    [ObservableProperty]
    private int _personsWithoutManager;

    [ObservableProperty]
    private int _contractBasedCount;

    [ObservableProperty]
    private int _departmentBasedCount;

    [ObservableProperty]
    private int _fromJsonCount;

    public PrimaryManagerAdminViewModel(
        IPrimaryManagerService primaryManagerService,
        IUserPreferencesService userPreferencesService,
        IDialogService dialogService)
    {
        _primaryManagerService = primaryManagerService ?? throw new ArgumentNullException(nameof(primaryManagerService));
        _userPreferencesService = userPreferencesService ?? throw new ArgumentNullException(nameof(userPreferencesService));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));

        // Load the last-used import logic as the default
        _selectedLogic = _userPreferencesService.LastPrimaryManagerLogic;
    }

    /// <summary>
    /// Loads current statistics about primary managers in the database.
    /// </summary>
    public async Task LoadStatisticsAsync()
    {
        var stats = await _primaryManagerService.GetStatisticsAsync();

        TotalPersons = stats.TotalPersons;
        PersonsWithManager = stats.PersonsWithManager;
        PersonsWithoutManager = stats.PersonsWithoutManager;
        ContractBasedCount = stats.ContractBasedCount;
        DepartmentBasedCount = stats.DepartmentBasedCount;
        FromJsonCount = stats.FromJsonCount;
    }

    /// <summary>
    /// Refreshes all primary managers using the selected logic.
    /// </summary>
    [RelayCommand]
    private async Task RefreshAllAsync()
    {
        var result = _dialogService.ShowConfirm(
            $"This will recalculate primary managers for all {TotalPersons} persons using {SelectedLogic} logic.\n\n" +
            "Existing primary manager values will be overwritten.\n\n" +
            "Do you want to continue?",
            "Confirm Refresh All Primary Managers");

        if (!result)
            return;

        try
        {
            IsRefreshing = true;
            StatusMessage = $"Refreshing all primary managers using {SelectedLogic} logic...";

            int updatedCount = await _primaryManagerService.RefreshAllPrimaryManagersAsync(SelectedLogic);

            StatusMessage = $"Successfully updated {updatedCount} persons using {SelectedLogic} logic.";

            // Reload statistics after refresh
            await LoadStatisticsAsync();

            // Auto-clear success message after delay
            await Task.Delay(5000);
            StatusMessage = null;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error refreshing primary managers: {ex.Message}";
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    /// <summary>
    /// Recalculates all primary managers using Primary Contract Rules (ContractBased logic).
    /// </summary>
    [RelayCommand]
    private async Task RecalculateUsingContractRulesAsync()
    {
        try
        {
            IsRecalculatingWithContractRules = true;
            RecalculateContractRulesStatusMessage = null;

            // Force UI update before starting heavy operation
            await Task.Delay(1).ConfigureAwait(true);

            // Run heavy work on background thread to keep UI responsive
            int updatedCount = await Task.Run(async () =>
            {
                return await _primaryManagerService.RefreshAllPrimaryManagersAsync(PrimaryManagerLogic.ContractBased);
            }).ConfigureAwait(true);

            RecalculateContractRulesStatusMessage = $"Successfully updated {updatedCount:N0} primary managers using Primary Contract Rules. Changes applied.";

            // Reload statistics after refresh
            await LoadStatisticsAsync();

            // Auto-clear after delay
            await Task.Delay(5000);
            RecalculateContractRulesStatusMessage = null;
        }
        catch (Exception ex)
        {
            RecalculateContractRulesStatusMessage = $"Failed to recalculate: {ex.Message}";
        }
        finally
        {
            IsRecalculatingWithContractRules = false;
        }
    }
}
