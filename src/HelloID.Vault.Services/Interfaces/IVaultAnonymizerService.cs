using HelloID.Vault.Services.Anonymization.Models;

namespace HelloID.Vault.Services.Interfaces;

/// <summary>
/// Service for anonymizing vault.json files.
/// </summary>
public interface IVaultAnonymizerService
{
    /// <summary>
    /// Creates an anonymized copy of vault.json with progress reporting.
    /// </summary>
    /// <param name="inputFilePath">Path to the original vault.json file.</param>
    /// <param name="outputFilePath">Path where the anonymized file will be saved.</param>
    /// <param name="options">Anonymization configuration options.</param>
    /// <param name="progress">Optional progress reporter for tracking anonymization status.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>Result containing statistics and status of the anonymization operation.</returns>
    Task<AnonymizationResult> AnonymizeAsync(
        string inputFilePath,
        string outputFilePath,
        AnonymizationOptions options,
        IProgress<AnonymizationProgress>? progress = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Validates that a file can be anonymized.
    /// </summary>
    /// <param name="filePath">Path to the file to validate.</param>
    /// <returns>True if the file can be anonymized, false otherwise.</returns>
    Task<bool> CanAnonymizeAsync(string filePath);
}
