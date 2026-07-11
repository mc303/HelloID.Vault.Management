using CommunityToolkit.Mvvm.ComponentModel;
using HelloID.Vault.Core.Models.DTOs;
using System.Text.Json;

namespace HelloID.Vault.Management.ViewModels.Contracts;

/// <summary>
/// ViewModel for displaying contract details in JSON format identical to vault.json structure
/// </summary>
public partial class ContractViewViewModel : ObservableObject
{
    [ObservableProperty]
    private string _jsonContent = string.Empty;

    public ContractViewViewModel(ContractJsonDto contract)
    {
        JsonContent = FormatContractAsJson(contract);
    }

    /// <summary>
    /// Formats the contract data as JSON matching the vault.json structure exactly
    /// </summary>
    private string FormatContractAsJson(ContractJsonDto contract)
    {
        var contractJson = new
        {
            context = contract.Context,
            externalId = contract.ExternalId,
            startDate = contract.StartDate,
            endDate = contract.EndDate,
            type = contract.Type,
            details = contract.Details,
            location = contract.Location,
            costCenter = contract.CostCenter,
            costBearer = contract.CostBearer,
            employer = contract.Employer,
            manager = contract.Manager,
            team = contract.Team,
            department = contract.Department,
            division = contract.Division,
            title = contract.Title,
            organization = contract.Organization,
            custom = contract.Custom
        };

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        return JsonSerializer.Serialize(contractJson, options);
    }
}