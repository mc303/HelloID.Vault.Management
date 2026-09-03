using System.Net;
using System.Security.Cryptography;
using System.Text;
using HelloID.Vault.Core.Models.Ad;
using HelloID.Vault.Services.Interfaces;
using System.DirectoryServices.Protocols;

namespace HelloID.Vault.Services.ActiveDirectory;

/// <summary>
/// LDAP implementation of <see cref="IActiveDirectoryService"/> using
/// System.DirectoryServices.Protocols. Supports Negotiate (integrated),
/// Simple, and Anonymous binds, LDAPS, and paged searches.
/// </summary>
public class ActiveDirectoryService : IActiveDirectoryService
{
    private const int PageSize = 1000;
    private const string UserFilter = "(&(objectClass=user)(objectCategory=person))";

    private static readonly string[] BaseAttributes =
    {
        "objectGUID", "distinguishedName", "sAMAccountName", "userPrincipalName",
        "displayName", "givenName", "sn", "mail", "userAccountControl"
    };

    public async Task<(bool Success, string Message)> TestConnectionAsync(AdCorrelationConfig config, string? plainPassword = null)
    {
        try
        {
            using var connection = CreateConnection(config, plainPassword);
            await connection.BindAsync(BuildCredential(config, plainPassword)).ConfigureAwait(false);

            var bindIdentity = config.AuthType is "Negotiate" or "Anonymous"
                ? "integrated/anonymous"
                : config.Username;
            return (true, $"Connected to {config.LdapHost} as {bindIdentity}");
        }
        catch (LdapException ex)
        {
            return (false, $"LDAP error {ex.ErrorCode}: {ex.Message}");
        }
        catch (Exception ex)
        {
            return (false, $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    public async Task<List<AdUserDto>> SearchAllUsersAsync(AdCorrelationConfig config, string? plainPassword = null, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        var users = new List<AdUserDto>();

        using var connection = CreateConnection(config, plainPassword);
        await connection.BindAsync(BuildCredential(config, plainPassword)).ConfigureAwait(false);

        // Request base set + configured correlation attribute + any match fields
        var requestedAttributes = BaseAttributes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(config.CorrelationAttribute))
        {
            requestedAttributes.Add(config.CorrelationAttribute);
        }

        var searchRequest = new SearchRequest(
            config.SearchBase,
            UserFilter,
            System.DirectoryServices.Protocols.SearchScope.Subtree,
            requestedAttributes.ToArray());

        var pageControl = new PageResultRequestControl(PageSize);
        searchRequest.Controls.Add(pageControl);

        var page = 0;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var response = (SearchResponse)await connection.SendRequestAsync(searchRequest, TimeSpan.FromSeconds(30)).ConfigureAwait(false);

            page++;
            progress?.Report($"Retrieved {users.Count + response.Entries.Count} AD accounts (page {page})...");

            foreach (SearchResultEntry entry in response.Entries)
            {
                users.Add(MapEntry(entry, config.CorrelationAttribute));
            }

            // Find the page response control to check for more pages
            PageResultResponseControl? pageResponse = null;
            foreach (DirectoryControl control in response.Controls)
            {
                if (control is PageResultResponseControl prc)
                {
                    pageResponse = prc;
                    break;
                }
            }

            if (pageResponse == null || pageResponse.Cookie.Length == 0)
            {
                break;
            }

            pageControl.Cookie = pageResponse.Cookie;
        }

        progress?.Report($"Retrieved {users.Count} AD accounts total");
        return users;
    }

    public async Task<AdUserDto?> GetUserByGuidAsync(AdCorrelationConfig config, string objectGuid, string? plainPassword = null)
    {
        if (!Guid.TryParse(objectGuid, out var guid))
        {
            return null;
        }

        using var connection = CreateConnection(config, plainPassword);
        await connection.BindAsync(BuildCredential(config, plainPassword)).ConfigureAwait(false);

        // LDAP filter with escaped binary objectGUID: each byte as \HH
        var escapedGuid = string.Join(string.Empty, guid.ToByteArray().Select(b => @"\" + b.ToString("X2")));
        var filter = "(objectGUID=" + escapedGuid + ")";

        var requestedAttributes = BaseAttributes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(config.CorrelationAttribute))
        {
            requestedAttributes.Add(config.CorrelationAttribute);
        }

        var request = new SearchRequest(
            config.SearchBase,
            $"(&(objectClass=user)(objectCategory=person){filter})",
            System.DirectoryServices.Protocols.SearchScope.Subtree,
            requestedAttributes.ToArray());

        var response = (SearchResponse)await connection.SendRequestAsync(request, TimeSpan.FromSeconds(30)).ConfigureAwait(false);
        return response.Entries.Count > 0 ? MapEntry(response.Entries[0], config.CorrelationAttribute) : null;
    }

    private static LdapConnection CreateConnection(AdCorrelationConfig config, string? plainPassword)
    {
        var identifier = new LdapDirectoryIdentifier(config.LdapHost, config.LdapPort, fullyQualifiedDnsHostName: false, connectionless: false);
        var connection = new LdapConnection(identifier)
        {
            AuthType = config.AuthType switch
            {
                "Simple" => AuthType.Simple,
                "Anonymous" => AuthType.Anonymous,
                _ => AuthType.Negotiate
            },
            Timeout = TimeSpan.FromSeconds(30)
        };

        if (config.UseLdaps)
        {
            connection.SessionOptions.ProtocolVersion = 3;
            // LDAPS is selected by port; the identifier above carries the port
        }

        return connection;
    }

    private static NetworkCredential? BuildCredential(AdCorrelationConfig config, string? plainPassword)
    {
        return config.AuthType switch
        {
            "Anonymous" => null,
            "Simple" when !string.IsNullOrEmpty(config.Username) && !string.IsNullOrEmpty(plainPassword)
                => new NetworkCredential(config.Username, plainPassword),
            _ => null // Negotiate uses current process credentials
        };
    }

    private static AdUserDto MapEntry(SearchResultEntry entry, string correlationAttribute)
    {
        var user = new AdUserDto
        {
            DistinguishedName = entry.DistinguishedName,
            SamAccountName = GetSingleValue(entry, "sAMAccountName"),
            UserPrincipalName = GetSingleValue(entry, "userPrincipalName"),
            DisplayName = GetSingleValue(entry, "displayName"),
            GivenName = GetSingleValue(entry, "givenName"),
            Surname = GetSingleValue(entry, "sn"),
            Mail = GetSingleValue(entry, "mail")
        };

        // objectGUID is a binary blob
        if (entry.Attributes["objectGUID"]?[0] is byte[] guidBytes && guidBytes.Length == 16)
        {
            user.ObjectGuid = new Guid(guidBytes).ToString();
        }

        // userAccountControl bit 2 = ACCOUNTDISABLE
        var uac = GetSingleValue(entry, "userAccountControl");
        if (int.TryParse(uac, out var userAccountControl))
        {
            user.Enabled = (userAccountControl & 0x0002) == 0;
        }

        if (!string.IsNullOrWhiteSpace(correlationAttribute))
        {
            user.CorrelationValue = GetSingleValue(entry, correlationAttribute);
        }

        // Capture all requested attributes for potential match-field usage
        foreach (string attributeName in entry.Attributes.AttributeNames)
        {
            user.Attributes[attributeName] = GetSingleValue(entry, attributeName);
        }

        return user;
    }

    private static string? GetSingleValue(SearchResultEntry entry, string attributeName)
    {
        var attribute = entry.Attributes[attributeName];
        if (attribute == null || attribute.Count == 0)
        {
            return null;
        }

        var value = attribute[0];
        return value switch
        {
            byte[] bytes => Convert.ToHexString(bytes),
            _ => value?.ToString()
        };
    }
}
