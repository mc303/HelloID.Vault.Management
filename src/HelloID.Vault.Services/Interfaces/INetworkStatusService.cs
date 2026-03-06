namespace HelloID.Vault.Services.Interfaces;

/// <summary>
/// Service for monitoring network connectivity status.
/// </summary>
public interface INetworkStatusService
{
    /// <summary>
    /// Gets a value indicating whether the network is available.
    /// </summary>
    bool IsNetworkAvailable { get; }

    /// <summary>
    /// Gets the timestamp of the last status check.
    /// </summary>
    DateTime LastChecked { get; }

    /// <summary>
    /// Starts monitoring network status.
    /// </summary>
    void StartMonitoring(int intervalSeconds = 30);

    /// <summary>
    /// Stops monitoring network status.
    /// </summary>
    void StopMonitoring();

    /// <summary>
    /// Checks network status immediately.
    /// </summary>
    /// <returns>True if network is available.</returns>
    Task<bool> CheckNetworkStatusAsync();

    /// <summary>
    /// Event raised when network status changes.
    /// </summary>
    event EventHandler<NetworkStatusChangedEventArgs>? NetworkStatusChanged;
}

/// <summary>
/// Event arguments for network status changes.
/// </summary>
public class NetworkStatusChangedEventArgs : EventArgs
{
    /// <summary>
    /// Gets a value indicating whether the network is available.
    /// </summary>
    public bool IsNetworkAvailable { get; init; }

    /// <summary>
    /// Gets the timestamp when the status changed.
    /// </summary>
    public DateTime Timestamp { get; init; }

    /// <summary>
    /// Gets the previous network status.
    /// </summary>
    public bool PreviousStatus { get; init; }
}
