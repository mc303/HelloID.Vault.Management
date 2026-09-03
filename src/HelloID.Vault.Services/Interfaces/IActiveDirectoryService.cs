using HelloID.Vault.Core.Models.Ad;

namespace HelloID.Vault.Services.Interfaces;

/// <summary>
/// Queries Active Directory over LDAP using the correlation configuration.
/// </summary>
public interface IActiveDirectoryService
{
    /// <summary>Tests connectivity and bind with the given configuration. Returns an error message on failure.</summary>
    Task<(bool Success, string Message)> TestConnectionAsync(AdCorrelationConfig config, string? plainPassword = null);

    /// <summary>
    /// Fetches all AD user objects (paged) with the minimal attribute set
    /// plus the configured correlation attribute.
    /// </summary>
    Task<List<AdUserDto>> SearchAllUsersAsync(AdCorrelationConfig config, string? plainPassword = null, IProgress<string>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches a single AD user by object GUID. Returns null when not found.
    /// </summary>
    Task<AdUserDto?> GetUserByGuidAsync(AdCorrelationConfig config, string objectGuid, string? plainPassword = null);
}
