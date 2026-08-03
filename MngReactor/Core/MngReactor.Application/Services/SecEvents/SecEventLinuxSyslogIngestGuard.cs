using System.Text.Json;
using MngReactor.Application.Features.Commands.Ingest;

namespace MngReactor.Application.Services.SecEvents;

/// <summary>
/// Detects legacy Linux rsyslog/auth syslog payloads so Reactor can refuse them.
/// Linux host telemetry must come from MngLogs agent (journal), not UDP syslog → Engine → Reactor.
/// </summary>
public static class SecEventLinuxSyslogIngestGuard
{
    public const string ProductLinuxSyslog = "linux-syslog";

    public static bool IsLinuxSyslogProduct(string? product) =>
        !string.IsNullOrWhiteSpace(product)
        && product.Trim().Equals(ProductLinuxSyslog, StringComparison.OrdinalIgnoreCase);

    /// <summary>Classic auth syslog markers used by Engine ClassifySource.</summary>
    public static bool LooksLikeLinuxAuthSyslog(string? rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText))
            return false;

        return rawText.Contains("sshd[", StringComparison.Ordinal)
               || rawText.Contains("sshd-session[", StringComparison.OrdinalIgnoreCase)
               || rawText.Contains("sudo:", StringComparison.Ordinal);
    }

    public static bool LooksLikeLinuxAuthSyslog(JsonElement raw)
    {
        if (raw.ValueKind == JsonValueKind.String)
            return LooksLikeLinuxAuthSyslog(raw.GetString());

        try
        {
            return LooksLikeLinuxAuthSyslog(raw.GetRawText());
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    public static bool ShouldReject(SecEventIngestItem item, bool acceptLinuxSyslogIngest)
    {
        if (acceptLinuxSyslogIngest)
            return false;

        if (IsLinuxSyslogProduct(item.Source?.Product))
            return true;

        return LooksLikeLinuxAuthSyslog(item.Raw);
    }
}
