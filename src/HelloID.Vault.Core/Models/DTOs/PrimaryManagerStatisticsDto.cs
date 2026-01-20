namespace HelloID.Vault.Core.Models.DTOs;

/// <summary>
/// Statistics about Primary Manager data in the system.
/// </summary>
public class PrimaryManagerStatisticsDto
{
    /// <summary>
    /// Total number of persons in the system.
    /// </summary>
    public int TotalPersons { get; set; }

    /// <summary>
    /// Number of persons with a primary manager assigned.
    /// </summary>
    public int PersonsWithManager { get; set; }

    /// <summary>
    /// Number of persons without a primary manager assigned.
    /// </summary>
    public int PersonsWithoutManager { get; set; }

    /// <summary>
    /// Number of persons with contract-based primary manager.
    /// </summary>
    public int ContractBasedCount { get; set; }

    /// <summary>
    /// Number of persons with department-based primary manager.
    /// </summary>
    public int DepartmentBasedCount { get; set; }

    /// <summary>
    /// Number of persons with import-based primary manager (from JSON).
    /// </summary>
    public int FromJsonCount { get; set; }
}
