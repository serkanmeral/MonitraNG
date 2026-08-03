using System.Text.Json;
using MngEngine.Application.Features.SecEvents;

namespace MngEngine.Persistence.Service.SecEvents;

/// <summary>
/// Detects legacy Linux rsyslog/auth syslog so Engine can drop them before Reactor.
/// Linux host telemetry must come from MngLogs agent.
/// </summary>
public static class SecEventLinuxSyslogIngestGuard
{
    public const string ProductLinuxSyslog = "linux-syslog";

    public static bool IsLinuxSyslogProduct(string? product) =>
        !string.IsNullOrWhiteSpace(product)
        && product.Trim().Equals(ProductLinuxSyslog, StringComparison.OrdinalIgnoreCase);

    public static bool LooksLikeLinuxAuthSyslog(string? raw) =>
        !string.IsNullOrWhiteSpace(raw)
        && (raw.Contains("sshd[", StringComparison.Ordinal)
            || raw.Contains("sshd-session[", StringComparison.OrdinalIgnoreCase)
            || raw.Contains("sudo:", StringComparison.Ordinal));

    public static bool LooksLikeLinuxAuthSyslog(object? raw)
    {
        if (raw is null)
            return false;

        if (raw is string s)
            return LooksLikeLinuxAuthSyslog(s);

        if (raw is JsonElement el)
        {
            if (el.ValueKind == JsonValueKind.String)
                return LooksLikeLinuxAuthSyslog(el.GetString());
            return LooksLikeLinuxAuthSyslog(el.GetRawText());
        }

        return LooksLikeLinuxAuthSyslog(raw.ToString());
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
