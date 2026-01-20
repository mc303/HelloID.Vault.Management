using HelloID.Vault.Core.Models.DTOs;

namespace HelloID.Vault.Core.Models.DTOs;

/// <summary>
/// Result of primary contract preview with step-by-step breakdown.
/// </summary>
public class PrimaryContractPreviewResult
{
    /// <summary>
    /// The person being previewed.
    /// </summary>
    public PersonDetailDto Person { get; set; } = null!;

    /// <summary>
    /// The winning contract selected as primary.
    /// </summary>
    public ContractDetailDto WinningContract { get; set; } = null!;

    /// <summary>
    /// All contracts for this person.
    /// </summary>
    public List<ContractDetailDto> AllContracts { get; set; } = new();

    /// <summary>
    /// Step-by-step breakdown of how the winner was determined.
    /// </summary>
    public List<PrimaryContractSelectionStep> SelectionSteps { get; set; } = new();
}

/// <summary>
/// Represents one step in the primary contract selection process.
/// </summary>
public class PrimaryContractSelectionStep
{
    /// <summary>
    /// Step number (1, 2, 3, etc.)
    /// </summary>
    public int StepNumber { get; set; }

    /// <summary>
    /// Field name being compared (e.g., "Contract Status", "FTE", "Hours Per Week")
    /// </summary>
    public string FieldName { get; set; } = null!;

    /// <summary>
    /// Sort direction (Ascending/Descending)
    /// </summary>
    public string SortDirection { get; set; } = null!;

    /// <summary>
    /// Description of what happened in this step.
    /// </summary>
    public string Description { get; set; } = null!;

    /// <summary>
    /// Contracts in order after this step (top 3 for reference).
    /// </summary>
    public List<ContractSummary> ContractOrder { get; set; } = new();
}

/// <summary>
/// Lightweight summary of a contract for preview display.
/// </summary>
public class ContractSummary
{
    public int ContractId { get; set; }
    public string ContractStatus { get; set; } = null!;
    public string DisplayValue { get; set; } = null!;
    public bool IsWinner { get; set; }
}
