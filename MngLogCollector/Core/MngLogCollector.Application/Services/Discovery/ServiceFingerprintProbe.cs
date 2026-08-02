using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;

namespace MngLogCollector.Application.Services.Discovery;

public sealed class ServiceFingerprintSignals
{
    public string? HttpTitle { get; init; }
    public string? TlsCommonName { get; init; }
    public string? SshBanner { get; init; }
}

/// <summary>
/// Lightweight runZero-style signals: HTTP title, TLS CN, SSH banner.
/// Time-bounded; never throws to caller.
/// </summary>
public static class ServiceFingerprintProbe
{
    private static readonly Regex TitleRegex = new(
        @"<title[^>]*>(.*?)</title>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    public static async Task<ServiceFingerprintSignals> ProbeAsync(
        IPAddress ip,
        IReadOnlyList<int> openPorts,
        TimeSpan timeout,
        CancellationToken ct)
    {
        string? httpTitle = null;
        string? tlsCn = null;
        string? sshBanner = null;
        var ipStr = ip.ToString();

        try
        {
            if (openPorts.Contains(22))
                sshBanner = await TrySshBannerAsync(ipStr, timeout, ct);

            if (openPorts.Contains(443))
            {
                var https = await TryHttpsAsync(ipStr, timeout, ct);
                tlsCn = https.TlsCn;
                httpTitle ??= https.Title;
            }

            if (openPorts.Contains(80) && string.IsNullOrWhiteSpace(httpTitle))
                httpTitle = await TryHttpTitleAsync(ipStr, 80, useTls: false, timeout, ct);
        }
        catch
        {
            // ignore — fingerprint is best-effort
        }

        return new ServiceFingerprintSignals
        {
            HttpTitle = Truncate(httpTitle, 120),
            TlsCommonName = Truncate(tlsCn, 120),
            SshBanner = Truncate(sshBanner, 160)
        };
    }

    private static async Task<string?> TrySshBannerAsync(string host, TimeSpan timeout, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(host, 22, cts.Token);
            await using var stream = client.GetStream();
            stream.ReadTimeout = (int)timeout.TotalMilliseconds;
            var buf = new byte[256];
            var n = await stream.ReadAsync(buf.AsMemory(0, buf.Length), cts.Token);
            if (n <= 0) return null;
            var text = Encoding.ASCII.GetString(buf, 0, n).Trim();
            var line = text.Split('\n', 2)[0].Trim();
            return line.StartsWith("SSH-", StringComparison.Ordinal) ? line : null;
        }
        catch
        {
            return null;
        }
    }

    private static async Task<(string? Title, string? TlsCn)> TryHttpsAsync(
        string host,
        TimeSpan timeout,
        CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(host, 443, cts.Token);
            await using var net = client.GetStream();
            using var ssl = new SslStream(net, leaveInnerStreamOpen: false, (_, _, _, _) => true);
            await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
            {
                TargetHost = host,
                EnabledSslProtocols = System.Security.Authentication.SslProtocols.Tls12
                    | System.Security.Authentication.SslProtocols.Tls13,
                CertificateRevocationCheckMode = X509RevocationMode.NoCheck
            }, cts.Token);

            string? cn = null;
            if (ssl.RemoteCertificate is X509Certificate2 cert2)
            {
                cn = cert2.GetNameInfo(X509NameType.SimpleName, false);
                if (string.IsNullOrWhiteSpace(cn))
                    cn = cert2.Subject;
            }
            else if (ssl.RemoteCertificate is { } rawCert)
            {
                cn = rawCert.Subject;
            }

            var title = await ReadHttpTitleFromStreamAsync(ssl, host, cts.Token);
            return (title, cn);
        }
        catch
        {
            return (null, null);
        }
    }

    private static async Task<string?> TryHttpTitleAsync(
        string host,
        int port,
        bool useTls,
        TimeSpan timeout,
        CancellationToken ct)
    {
        if (useTls) return null;
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(host, port, cts.Token);
            await using var stream = client.GetStream();
            return await ReadHttpTitleFromStreamAsync(stream, host, cts.Token);
        }
        catch
        {
            return null;
        }
    }

    private static async Task<string?> ReadHttpTitleFromStreamAsync(
        Stream stream,
        string host,
        CancellationToken ct)
    {
        var req = Encoding.ASCII.GetBytes(
            $"GET / HTTP/1.0\r\nHost: {host}\r\nUser-Agent: MonitraNG-Discovery/1.0\r\nConnection: close\r\n\r\n");
        await stream.WriteAsync(req, ct);
        await stream.FlushAsync(ct);

        var buf = new byte[4096];
        var total = 0;
        while (total < buf.Length)
        {
            var n = await stream.ReadAsync(buf.AsMemory(total, buf.Length - total), ct);
            if (n <= 0) break;
            total += n;
            if (total > 512 && Encoding.ASCII.GetString(buf, 0, total).Contains("</title>", StringComparison.OrdinalIgnoreCase))
                break;
        }

        if (total == 0) return null;
        var body = Encoding.UTF8.GetString(buf, 0, total);
        var m = TitleRegex.Match(body);
        if (!m.Success) return null;
        return System.Net.WebUtility.HtmlDecode(m.Groups[1].Value).Trim();
    }

    private static string? Truncate(string? s, int max)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        s = s.Trim();
        return s.Length <= max ? s : s[..max] + "…";
    }
}
