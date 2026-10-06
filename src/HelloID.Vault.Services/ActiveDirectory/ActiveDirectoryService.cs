using System.DirectoryServices;
using System.Runtime.InteropServices;
using HelloID.Vault.Core.Models.Ad;
using HelloID.Vault.Services.Interfaces;

namespace HelloID.Vault.Services.ActiveDirectory;

/// <summary>
/// ADSI-based AD service using DirectoryEntry/DirectorySearcher.
/// Handles authentication (Kerberos/NTLM/LDAPS), paging, and session
/// security natively through the Windows ADSI layer.
/// </summary>
public class ActiveDirectoryService : IActiveDirectoryService
{
    private const string UserFilter = "(&(objectClass=user)(objectCategory=person))";
    private static readonly string[] BaseAttributes =
    {
        "objectGUID", "distinguishedName", "sAMAccountName", "userPrincipalName",
        "displayName", "givenName", "sn", "mail", "userAccountControl"
    };

    public async Task<(bool Success, string Message)> TestConnectionAsync(AdCorrelationConfig config, string? plainPassword = null)
    {
        var (username, password) = ResolveCredentials(config, plainPassword);
        var identity = string.IsNullOrWhiteSpace(config.Username) ? "integrated (Windows identity)" : config.Username;

        try
        {
            using var entry = CreateEntry(config, config.SearchBase, username, password);
            var dc = GetRootDseInfo(username, password);
            var dn = entry.Properties["distinguishedName"]?.Count > 0
                ? entry.Properties["distinguishedName"][0]?.ToString()
                : entry.Name;
            return (true, $"Connected via DC '{dc}' as {identity} (search base: {dn})");
        }
        catch (COMException cex)
        {
            return (false, $"Cannot connect to '{config.LdapHost}:{config.LdapPort}' as '{identity}': {cex.Message} (HRESULT 0x{cex.ErrorCode:X8})");
        }
        catch (Exception ex)
        {
            return (false, $"Cannot connect to '{config.LdapHost}:{config.LdapPort}' as '{identity}': {ex.Message}");
        }
    }

    public async Task<List<AdUserDto>> SearchAllUsersAsync(AdCorrelationConfig config, string? plainPassword = null, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        var (username, password) = ResolveCredentials(config, plainPassword);
        var users = new List<AdUserDto>();

        using var searchRoot = CreateEntry(config, config.SearchBase, username, password);
        GetRootDseInfo(username, password);

        var attributes = new List<string>(BaseAttributes);
        if (!string.IsNullOrWhiteSpace(config.CorrelationAttribute))
        {
            attributes.Add(config.CorrelationAttribute);
        }

        using var searcher = new DirectorySearcher(searchRoot)
        {
            Filter = UserFilter,
            PageSize = 1000,
            SearchScope = SearchScope.Subtree
        };
        foreach (var attr in attributes)
        {
            searcher.PropertiesToLoad.Add(attr);
        }

        System.Diagnostics.Debug.WriteLine(
            $"[AdSearch] path='{searcher.SearchRoot?.Path}' filter='{UserFilter}' scope={searcher.SearchScope} pageSize={searcher.PageSize} props=[{string.Join(", ", searcher.PropertiesToLoad.Cast<string>())}] boundAs='{DescribeIdentity(username)}'");

        LogAttributePresenceProbe(searchRoot, config.CorrelationAttribute);

        progress?.Report("Searching AD...");
        var searchResults = await Task.Run(() => searcher.FindAll(), cancellationToken).ConfigureAwait(false);
        System.Diagnostics.Debug.WriteLine($"[AdSearch] FindAll returned {searchResults.Count} results");

        var inspected = 0;
        foreach (SearchResult result in searchResults)
        {
            cancellationToken.ThrowIfCancellationRequested();
            users.Add(MapSearchResult(result, config.CorrelationAttribute));

            if (inspected < 5)
            {
                inspected++;
                var returned = result.Properties.PropertyNames.Cast<string>().OrderBy(n => n).ToList();
                var hasCorr = !string.IsNullOrWhiteSpace(config.CorrelationAttribute) &&
                              result.Properties.Contains(config.CorrelationAttribute);
                System.Diagnostics.Debug.WriteLine(
                    $"[AdSearch] sample {inspected}: dn='{GetProp(result, "distinguishedName")}' {config.CorrelationAttribute} present={hasCorr} returnedAttributes=[{string.Join(", ", returned)}]");
            }
        }

        var withValue = users.Count(u => !string.IsNullOrWhiteSpace(u.CorrelationValue));
        System.Diagnostics.Debug.WriteLine($"[AdSearch] Found {users.Count} users, {withValue} with '{config.CorrelationAttribute}'");
        progress?.Report($"Found {users.Count} AD accounts ({withValue} with {config.CorrelationAttribute})");
        return users;
    }

    public async Task<AdUserDto?> GetUserByGuidAsync(AdCorrelationConfig config, string objectGuid, string? plainPassword = null)
    {
        if (!Guid.TryParse(objectGuid, out var guid))
        {
            return null;
        }

        var (username, password) = ResolveCredentials(config, plainPassword);

        try
        {
            using var entry = new DirectoryEntry(
                $"LDAP://<GUID={guid}>",
                username, password, AuthenticationTypes.Secure);

            var dn = GetEntryProperty(entry, "distinguishedName") ?? entry.Path.Replace("LDAP://", "");
            var sam = GetEntryProperty(entry, "sAMAccountName");
            var upn = GetEntryProperty(entry, "userPrincipalName");
            var displayName = GetEntryProperty(entry, "displayName");
            var givenName = GetEntryProperty(entry, "givenName");
            var sn = GetEntryProperty(entry, "sn");
            var mail = GetEntryProperty(entry, "mail");
            var uacText = GetEntryProperty(entry, "userAccountControl");

            var user = new AdUserDto
            {
                ObjectGuid = guid.ToString(),
                DistinguishedName = dn,
                SamAccountName = sam,
                UserPrincipalName = upn,
                DisplayName = displayName,
                GivenName = givenName,
                Surname = sn,
                Mail = mail
            };

            if (int.TryParse(uacText, out var uac))
            {
                user.Enabled = (uac & 0x0002) == 0;
            }

            if (!string.IsNullOrWhiteSpace(config.CorrelationAttribute))
            {
                user.CorrelationValue = GetEntryProperty(entry, config.CorrelationAttribute);
            }

            foreach (string propName in entry.Properties.PropertyNames)
            {
                user.Attributes[propName] = GetEntryProperty(entry, propName);
            }

            return user;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AdSearch] GetUserByGuidAsync({objectGuid}): {ex.Message}");
            return null;
        }
    }

    public async Task<(bool Success, string Message)> SetUserAttributeAsync(
        AdCorrelationConfig config, string objectGuid, string attributeName, string value, string? plainPassword = null)
    {
        if (string.IsNullOrWhiteSpace(attributeName))
        {
            return (false, "No attribute name configured.");
        }

        var user = await GetUserByGuidAsync(config, objectGuid, plainPassword).ConfigureAwait(false);
        if (user == null)
        {
            return (false, $"AD account {objectGuid} not found.");
        }

        var (username, password) = ResolveCredentials(config, plainPassword);

        return await Task.Run(() =>
        {
            try
            {
                using var entry = new DirectoryEntry(
                    $"LDAP://{user.DistinguishedName}",
                    username, password, AuthenticationTypes.Secure);

                entry.Properties[attributeName].Value = value;
                entry.CommitChanges();
                System.Diagnostics.Debug.WriteLine($"[AdWrite] Set {attributeName}='{value}' on '{user.SamAccountName}' via DirectoryEntry");
                return (true, $"Set {attributeName} on {user.SamAccountName ?? user.DistinguishedName}");
            }
            catch (Exception ex)
            {
                var reason = ex.Message;
                if (reason.Contains("INSUFF_ACCESS_RIGHTS", StringComparison.OrdinalIgnoreCase) ||
                    reason.Contains("Access is denied", StringComparison.OrdinalIgnoreCase))
                {
                    reason += " - the account used for the connection needs write permission on this attribute.";
                }
                System.Diagnostics.Debug.WriteLine($"[AdWrite] FAILED {attributeName} on '{user.SamAccountName}': {reason}");
                return (false, $"{user.SamAccountName ?? objectGuid}: {reason}");
            }
        }).ConfigureAwait(false);
    }

    // ---- Helpers ----

    /// <summary>
    /// Resolves the effective bind identity for logging: the configured
    /// username, or the actual Windows identity of the process.
    /// </summary>
    private static string DescribeIdentity(string? username)
    {
        if (!string.IsNullOrWhiteSpace(username))
        {
            return username;
        }

        try
        {
            var wi = System.Security.Principal.WindowsIdentity.GetCurrent();
            var principal = new System.Security.Principal.WindowsPrincipal(wi);
            var adminRole = principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            return $"Windows identity '{wi.Name}' (auth: {wi.AuthenticationType}, adminRole: {adminRole})";
        }
        catch
        {
            return "Windows identity (unavailable)";
        }
    }

    private static DirectoryEntry CreateEntry(AdCorrelationConfig config, string? searchBase, string? username, string? password)
    {
        // Serverless bind: ADSI locates a DC via the Windows locator service.
        // The port is determined by AuthenticationTypes (Secure=389,
        // SecureSocketsLayer=636), never by host:port syntax in the path.
        var path = string.IsNullOrWhiteSpace(searchBase)
            ? "LDAP://RootDSE"
            : $"LDAP://{searchBase}";

        var authType = config.UseLdaps
            ? AuthenticationTypes.Secure | AuthenticationTypes.SecureSocketsLayer
            : AuthenticationTypes.Secure;

        System.Diagnostics.Debug.WriteLine(
            $"[AdBind] path='{path}' authType={authType} boundAs='{DescribeIdentity(username)}'");
        return new DirectoryEntry(path, username, password, authType);
    }

    /// <summary>
    /// Reads RootDSE and logs which DC actually answered the serverless bind.
    /// </summary>
    private static string GetRootDseInfo(string? username, string? password)
    {
        try
        {
            using var root = new DirectoryEntry("LDAP://RootDSE", username, password, AuthenticationTypes.Secure);
            var dns = root.Properties["dnsHostName"]?.Count > 0
                ? root.Properties["dnsHostName"][0]?.ToString() ?? "?"
                : "?";
            var nc = root.Properties["defaultNamingContext"]?.Count > 0
                ? root.Properties["defaultNamingContext"][0]?.ToString() ?? "?"
                : "?";
            System.Diagnostics.Debug.WriteLine(
                $"[AdBind] RootDSE: processMachine='{Environment.MachineName}' dc='{dns}' ips=[{string.Join(",", System.Net.Dns.GetHostAddresses(dns).Select(a => a.ToString()))}] nc='{nc}' boundAs='{DescribeIdentity(username)}'");
            return dns;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AdBind] RootDSE read failed: {ex.Message}");
            return "?";
        }
    }

    /// <summary>
    /// Server-side presence probe: (attribute=*) counts objects carrying the
    /// correlation attribute regardless of whether the caller can read the value.
    /// Matched &gt; 0 but attribute not returned in the main search = read-rights issue.
    /// Probe throws "undefined attribute type" when the configured name is not in schema.
    /// </summary>
    private static void LogAttributePresenceProbe(DirectoryEntry searchRoot, string? correlationAttribute)
    {
        if (string.IsNullOrWhiteSpace(correlationAttribute))
        {
            System.Diagnostics.Debug.WriteLine("[AdProbe] skipped - no correlation attribute configured");
            return;
        }

        try
        {
            using var probeSearcher = new DirectorySearcher(searchRoot)
            {
                Filter = $"({correlationAttribute}=*)",
                PageSize = 1000,
                SearchScope = SearchScope.Subtree
            };
            probeSearcher.PropertiesToLoad.Add("distinguishedName");
            using var probeResults = probeSearcher.FindAll();
            System.Diagnostics.Debug.WriteLine($"[AdProbe] ({correlationAttribute}=*) matched {probeResults.Count} objects");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AdProbe] ({correlationAttribute}=*) FAILED: {ex.Message}");
        }
    }


    private static (string? Username, string? Password) ResolveCredentials(AdCorrelationConfig config, string? plainPassword)
    {
        if (string.IsNullOrWhiteSpace(config.Username))
        {
            return (null, null); // ADSI uses the Windows identity
        }
        return (config.Username, plainPassword);
    }

    private static string? GetProp(SearchResult result, string attributeName)
    {
        if (result.Properties[attributeName] == null || result.Properties[attributeName].Count == 0)
        {
            return null;
        }

        var value = result.Properties[attributeName][0];
        return value switch
        {
            byte[] bytes when attributeName == "objectGUID" => new Guid(bytes).ToString(),
            byte[] bytes => Convert.ToHexString(bytes),
            _ => value?.ToString()
        };
    }

    private static string? GetEntryProperty(DirectoryEntry entry, string propertyName)
    {
        if (entry.Properties[propertyName] == null || entry.Properties[propertyName].Count == 0)
        {
            return null;
        }

        var value = entry.Properties[propertyName][0];
        return value switch
        {
            byte[] bytes when propertyName == "objectGUID" => new Guid(bytes).ToString(),
            byte[] bytes => Convert.ToHexString(bytes),
            _ => value?.ToString()
        };
    }

    private static AdUserDto MapSearchResult(SearchResult result, string correlationAttribute)
    {
        var user = new AdUserDto
        {
            DistinguishedName = GetProp(result, "distinguishedName"),
            SamAccountName = GetProp(result, "sAMAccountName"),
            UserPrincipalName = GetProp(result, "userPrincipalName"),
            DisplayName = GetProp(result, "displayName"),
            GivenName = GetProp(result, "givenName"),
            Surname = GetProp(result, "sn"),
            Mail = GetProp(result, "mail")
        };

        if (GetProp(result, "objectGUID") is string guidText && Guid.TryParse(guidText, out var guid))
        {
            user.ObjectGuid = guid.ToString();
        }

        var uac = GetProp(result, "userAccountControl");
        if (int.TryParse(uac, out var userAccountControl))
        {
            user.Enabled = (userAccountControl & 0x0002) == 0;
        }

        if (!string.IsNullOrWhiteSpace(correlationAttribute))
        {
            user.CorrelationValue = GetProp(result, correlationAttribute);
        }

        foreach (string propName in result.Properties.PropertyNames)
        {
            user.Attributes[propName] = GetProp(result, propName);
        }

        return user;
    }
}
