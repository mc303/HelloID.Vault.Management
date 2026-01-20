using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HelloID.Vault.Core.Models.DTOs;

namespace HelloID.Vault.Management.ViewModels.ReferenceData;

/// <summary>
/// ViewModel for the Primary Contract Preview dialog.
/// </summary>
public partial class PrimaryContractPreviewViewModel : ObservableObject
{
    [ObservableProperty]
    private string _personName = string.Empty;

    [ObservableProperty]
    private string _personExternalId = string.Empty;

    [ObservableProperty]
    private int _contractCount;

    [ObservableProperty]
    private bool _hasWinner;

    [ObservableProperty]
    private string _winningContractDescription = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    public ObservableCollection<PrimaryContractSelectionStep> SelectionSteps { get; } = new();
    public ObservableCollection<ContractDetailDto> AllContracts { get; } = new();

    public event Action? CloseRequested;

    public PrimaryContractPreviewViewModel()
    {
    }

    /// <summary>
    /// Loads the preview data from the result.
    /// </summary>
    public void LoadPreview(PrimaryContractPreviewResult result)
    {
        if (result == null)
            return;

        PersonName = result.Person.DisplayName ?? "Unknown";
        PersonExternalId = result.Person.ExternalId ?? "N/A";
        ContractCount = result.AllContracts.Count;
        HasWinner = result.WinningContract != null;

        if (result.WinningContract != null)
        {
            WinningContractDescription = $"{result.WinningContract.TypeDescription} " +
                $"({result.WinningContract.ContractStatus}) " +
                $"from {result.WinningContract.StartDate:yyyy-MM-dd} " +
                $"to {GetEndDateDisplay(result.WinningContract.EndDate)}";
        }

        SelectionSteps.Clear();
        foreach (var step in result.SelectionSteps)
        {
            SelectionSteps.Add(step);
        }

        AllContracts.Clear();
        foreach (var contract in result.AllContracts)
        {
            AllContracts.Add(contract);
        }
    }

    private string GetEndDateDisplay(string? endDate)
    {
        if (string.IsNullOrWhiteSpace(endDate))
            return "-";
        return endDate;
    }

    [RelayCommand]
    private void Close()
    {
        CloseRequested?.Invoke();
    }
}
