using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Timers;
using HelloID.Vault.Services.Interfaces;
using Timer = System.Timers.Timer;

namespace HelloID.Vault.Services;

/// <summary>
/// Service for monitoring network connectivity status.
/// </summary>
public class NetworkStatusService : INetworkStatusService, IDisposable
{
    private readonly Timer? _monitoringTimer;
    private bool _isNetworkAvailable = true;
    private bool _disposed;

    public bool IsNetworkAvailable => _isNetworkAvailable;
    public DateTime LastChecked { get; private set; } = DateTime.UtcNow;

    public event EventHandler<NetworkStatusChangedEventArgs>? NetworkStatusChanged;

    public NetworkStatusService()
    {
        Debug.WriteLine("[NetworkStatusService] Initialized");
        
        NetworkChange.NetworkAvailabilityChanged += OnNetworkAvailabilityChanged;
        
        _ = CheckInitialStatusAsync();
    }

    private async Task CheckInitialStatusAsync()
    {
        await CheckNetworkStatusAsync();
    }

    public void StartMonitoring(int intervalSeconds = 30)
    {
        if (intervalSeconds <= 0)
            throw new ArgumentException("Interval must be positive.", nameof(intervalSeconds));

        Debug.WriteLine($"[NetworkStatusService] Starting monitoring with interval: {intervalSeconds}s");
        
        StopMonitoring();
        
        var timer = new Timer(intervalSeconds * 1000);
        timer.Elapsed += async (sender, e) => await OnMonitoringTimerElapsedAsync();
        timer.AutoReset = true;
        timer.Enabled = true;
    }

    public void StopMonitoring()
    {
        Debug.WriteLine("[NetworkStatusService] Stopping monitoring");
    }

    public async Task<bool> CheckNetworkStatusAsync()
    {
        Debug.WriteLine("[NetworkStatusService] Checking network status...");

        bool isAvailable;
        try
        {
            using var ping = new Ping();
            var hosts = new[] { "8.8.8.8", "1.1.1.1", "turso.tech" };
            
            isAvailable = false;
            foreach (var host in hosts)
            {
                try
                {
                    var reply = await ping.SendPingAsync(host, 3000);
                    if (reply.Status == IPStatus.Success)
                    {
                        isAvailable = true;
                        break;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[NetworkStatusService] Ping to {host} failed: {ex.Message}");
                }
            }

            if (!isAvailable)
            {
                isAvailable = NetworkInterface.GetIsNetworkAvailable();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[NetworkStatusService] Network check failed: {ex.Message}");
            isAvailable = NetworkInterface.GetIsNetworkAvailable();
        }

        LastChecked = DateTime.UtcNow;
        UpdateStatus(isAvailable);

        Debug.WriteLine($"[NetworkStatusService] Network status: {isAvailable}");
        return isAvailable;
    }

    private async Task OnMonitoringTimerElapsedAsync()
    {
        await CheckNetworkStatusAsync();
    }

    private void OnNetworkAvailabilityChanged(object? sender, NetworkAvailabilityEventArgs e)
    {
        Debug.WriteLine($"[NetworkStatusService] System network availability changed: {e.IsAvailable}");
        UpdateStatus(e.IsAvailable);
    }

    private void UpdateStatus(bool isAvailable)
    {
        if (_isNetworkAvailable != isAvailable)
        {
            var previousStatus = _isNetworkAvailable;
            _isNetworkAvailable = isAvailable;

            Debug.WriteLine($"[NetworkStatusService] Status changed from {previousStatus} to {isAvailable}");

            NetworkStatusChanged?.Invoke(this, new NetworkStatusChangedEventArgs
            {
                IsNetworkAvailable = isAvailable,
                PreviousStatus = previousStatus,
                Timestamp = DateTime.UtcNow
            });
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        NetworkChange.NetworkAvailabilityChanged -= OnNetworkAvailabilityChanged;
        StopMonitoring();
        _disposed = true;

        Debug.WriteLine("[NetworkStatusService] Disposed");
        GC.SuppressFinalize(this);
    }
}
