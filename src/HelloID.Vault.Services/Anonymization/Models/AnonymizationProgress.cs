namespace HelloID.Vault.Services.Anonymization.Models;

/// <summary>
/// Progress information during anonymization.
/// </summary>
public class AnonymizationProgress
{
    public string CurrentPhase { get; set; } = string.Empty;
    public int TotalItems { get; set; }
    public int ProcessedItems { get; set; }
    public int Percentage => TotalItems > 0 ? (int)((double)ProcessedItems / TotalItems * 100) : 0;

    // Additional info for logging
    public string? CurrentItem { get; set; } // "Person 1 of 1234"
    public string? BusinessDomainUsed { get; set; } // "morissettetechnologies.com"
}
