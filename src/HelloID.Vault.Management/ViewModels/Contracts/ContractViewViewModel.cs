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
            Context = contract.Context,
            ExternalId = contract.ExternalId,
            StartDate = contract.StartDate, // Keep as yyyy-MM-dd
            EndDate = contract.EndDate,     // Keep as yyyy-MM-dd
            Type = contract.Type,
            Details = contract.Details,
            Location = contract.Location,
            CostCenter = contract.CostCenter,
            CostBearer = contract.CostBearer,
            Employer = contract.Employer,
            Manager = contract.Manager,
            Team = contract.Team,
            Department = contract.Department,
            Division = contract.Division,
            Title = contract.Title,
            Organization = contract.Organization
        };

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        return JsonSerializer.Serialize(contractJson, options);
    }
}