using System.DirectoryServices.Protocols;
using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;
using MngLogCollector.Application.Abstractions.Discovery;

namespace MngLogCollector.Persistence.Discovery;

/// <summary>AD computer search via LDAP (System.DirectoryServices.Protocols — Linux-friendly).</summary>
public sealed class AdComputerDirectoryClient : IAdComputerDirectoryClient
{
    private const int PageSize = 500;
    private static readonly string[] Attributes =
    [
        "objectGUID",
        "sAMAccountName",
        "dNSHostName",
        "operatingSystem",
        "operatingSystemVersion",
        "distinguishedName",
        "userAccountControl"
    ];

    private readonly ILogger<AdComputerDirectoryClient> _logger;

    public AdComputerDirectoryClient(ILogger<AdComputerDirectoryClient> logger)
    {
        _logger = logger;
    }

    public Task<IReadOnlyList<AdComputerRecord>> SearchComputersAsync(
        DirectoryLdapConfig ldap,
        CancellationToken ct = default)
    {
        return Task.Run(() => SearchComputers(ldap, ct), ct);
    }

    private IReadOnlyList<AdComputerRecord> SearchComputers(DirectoryLdapConfig ldap, CancellationToken ct)
    {
        var identifier = new LdapDirectoryIdentifier(ldap.Host, ldap.Port, fullyQualifiedDnsHostName: false, connectionless: false);
        using var connection = new LdapConnection(identifier);

        connection.SessionOptions.ProtocolVersion = 3;
        connection.SessionOptions.ReferralChasing = ReferralChasingOptions.None;
        if (ldap.UseSsl)
            connection.SessionOptions.SecureSocketLayer = true;

        connection.AuthType = AuthType.Basic;
        connection.Credential = new NetworkCredential(ldap.BindUsername, ldap.BindPassword);
        connection.Timeout = TimeSpan.FromSeconds(60);

        connection.Bind();

        var results = new List<AdComputerRecord>();
        var request = new SearchRequest(
            ldap.BaseDn,
            "(&(objectCategory=computer)(objectClass=computer))",
            SearchScope.Subtree,
            Attributes);

        var pageControl = new PageResultRequestControl(PageSize);
        request.Controls.Add(pageControl);

        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var response = (SearchResponse)connection.SendRequest(request);

            foreach (SearchResultEntry entry in response.Entries)
            {
                var mapped = MapEntry(entry);
                if (mapped is not null)
                    results.Add(mapped);
            }

            var pageResponse = response.Controls.OfType<PageResultResponseControl>().FirstOrDefault();
            if (pageResponse is null || pageResponse.Cookie.Length == 0)
                break;

            pageControl.Cookie = pageResponse.Cookie;
        }

        _logger.LogInformation(
            "AD computer search host={Host}:{Port} base={Base} count={Count}",
            ldap.Host, ldap.Port, ldap.BaseDn, results.Count);

        return results;
    }

    private static AdComputerRecord? MapEntry(SearchResultEntry entry)
    {
        var sam = ReadString(entry, "sAMAccountName");
        if (string.IsNullOrWhiteSpace(sam))
            return null;

        bool? enabled = null;
        var uacRaw = ReadString(entry, "userAccountControl");
        if (int.TryParse(uacRaw, out var uac))
            enabled = (uac & 0x2) == 0;

        return new AdComputerRecord
        {
            ObjectGuid = ReadGuid(entry),
            SamAccountName = sam,
            DnsHostName = ReadString(entry, "dNSHostName"),
            OperatingSystem = ReadString(entry, "operatingSystem"),
            OperatingSystemVersion = ReadString(entry, "operatingSystemVersion"),
            DistinguishedName = ReadString(entry, "distinguishedName") ?? entry.DistinguishedName,
            Enabled = enabled
        };
    }

    private static string? ReadString(SearchResultEntry entry, string attribute)
    {
        if (!entry.Attributes.Contains(attribute))
            return null;
        var attr = entry.Attributes[attribute];
        if (attr is null || attr.Count == 0)
            return null;

        var value = attr[0];
        return value switch
        {
            string s => s,
            byte[] bytes => Encoding.UTF8.GetString(bytes),
            _ => value?.ToString()
        };
    }

    private static string? ReadGuid(SearchResultEntry entry)
    {
        if (!entry.Attributes.Contains("objectGUID"))
            return null;
        var attr = entry.Attributes["objectGUID"];
        if (attr is null || attr.Count == 0)
            return null;

        try
        {
            if (attr[0] is byte[] bytes && bytes.Length == 16)
                return new Guid(bytes).ToString();

            if (attr[0] is string s && Guid.TryParse(s, out var g))
                return g.ToString();
        }
        catch
        {
            return null;
        }

        return null;
    }
}
