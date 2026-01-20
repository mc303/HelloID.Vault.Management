namespace HelloID.Vault.Services.Interfaces;

/// <summary>
/// Service interface for Primary Manager business logic.
/// </summary>
public interface IPrimaryManagerService
{
    /// <summary>
    /// Calculates the primary manager for a person based on the specified logic.
    /// </summary>
    /// <param name="personId">The person ID to calculate for</param>
    /// <param name="logic">The logic to apply (FromJson, ContractBased, DepartmentBased)</param>
    /// <returns>The manager person ID, or null if no manager found</returns>
    Task<string?> CalculatePrimaryManagerAsync(string personId, PrimaryManagerLogic logic);

    /// <summary>
    /// Updates the primary manager for a single person using the configured logic.
    /// </summary>
    /// <param name="personId">The person ID to update</param>
    /// <param name="logic">The logic to apply</param>
    Task UpdatePrimaryManagerForPersonAsync(string personId, PrimaryManagerLogic logic);

    /// <summary>
    /// Updates the primary manager for all persons in a department.
    /// Called when a department's manager changes.
    /// </summary>
    /// <param name="departmentExternalId">The department external ID</param>
    /// <param name="source">The source system</param>
    /// <param name="logic">The logic to apply</param>
    Task UpdatePrimaryManagerForDepartmentAsync(string departmentExternalId, string source, PrimaryManagerLogic logic);

    /// <summary>
    /// Recalculates and updates primary managers for all persons in the database.
    /// </summary>
    /// <param name="logic">The logic to apply</param>
    /// <returns>The number of persons updated</returns>
    Task<int> RefreshAllPrimaryManagersAsync(PrimaryManagerLogic logic);

    /// <summary>
    /// Gets statistics about primary managers in the system.
    /// </summary>
    /// <returns>Statistics about primary manager distribution</returns>
    Task<HelloID.Vault.Core.Models.DTOs.PrimaryManagerStatisticsDto> GetStatisticsAsync();
}

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
