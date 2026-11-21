namespace HelloID.Vault.Core.Models;

/// <summary>
/// Defines the logic for determining a person's Primary Manager.
/// </summary>
public enum PrimaryManagerLogic
{
    /// <summary>
    /// Use PrimaryManager value directly from vault.json import.
    /// No calculation needed.
    /// </summary>
    FromJson,

    /// <summary>
    /// Contract-Based: Primary Manager = Primary Contract's Manager.
    /// Priority: contract manager → department manager → null
    /// </summary>
    ContractBased,

    /// <summary>
    /// Department-Based: Primary Manager = Department Manager (or parent department manager).
    /// Priority: department manager → parent department manager → contract manager → null
    /// </summary>
    DepartmentBased
}
