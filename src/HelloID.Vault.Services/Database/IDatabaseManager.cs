using HelloID.Vault.Data.Connection;

namespace HelloID.Vault.Services.Database;

/// <summary>
/// Interface for database management operations including backup, deletion, and data checks.
/// </summary>
public interface IDatabaseManager
{
    /// <summary>
    /// Checks if the database contains any data.
    /// </summary>
    /// <returns>True if any main tables have data, false otherwise.</returns>
    Task<bool> HasDataAsync();

    /// <summary>
    /// Deletes the database file and recreates the schema.
    /// </summary>
    Task DeleteDatabaseAsync();

    /// <summary>
    /// Creates a timestamped backup of the database and recreates the schema.
    /// </summary>
    Task CreateDatabaseBackupAsync();
}
