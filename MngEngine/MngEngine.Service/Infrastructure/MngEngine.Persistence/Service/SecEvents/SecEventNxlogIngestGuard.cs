using System.Text.Json;
using MngEngine.Application.Features.SecEvents;

namespace MngEngine.Persistence.Service.SecEvents;

/// <summary>
/// Detects legacy NXLog payloads so Engine can drop them before Reactor.
/// Windows/Linux host telemetry must come from MngLogs agent, not NXLog.
/// </summary>
public static class SecEventNxlogIngestGuard
{
    public const string ProductWindowsNxlog = "windows-nxlog";
    public const string ProductWindowsNxlogJson = "windows-nxlog-json";

    public static bool IsNxlogProduct(string? product)
    {
        if (string.IsNullOrWhiteSpace(product))
            return false;

        var p = product.Trim();
        return p.Equals(ProductWindowsNxlog, StringComparison.OrdinalIgnoreCase)
               || p.Equals(ProductWindowsNxlogJson, StringComparison.OrdinalIgnoreCase);
    }

    public static bool LooksLikeNxlogJson(string? raw) =>
        SecEventSyslogItemBuilder.LooksLikeNxlogJson(raw ?? string.Empty);

    public static bool LooksLikeNxlogRaw(object? raw)
    {
        if (raw is null)
            return false;

        if (raw is string s)
            return LooksLikeNxlogJson(s);

        if (raw is JsonElement el)
        {
            if (el.ValueKind == JsonValueKind.Object)
                return el.TryGetProperty("EventID", out _) && el.TryGetProperty("Hostname", out _);
            if (el.ValueKind == JsonValueKind.String)
                return LooksLikeNxlogJson(el.GetString());
            return LooksLikeNxlogJson(el.GetRawText());
        }

        return LooksLikeNxlogJson(raw.ToString());
    }

    public static bool ShouldReject(SecEventIngestItem item, bool acceptNxlogIngest)
    {
        if (acceptNxlogIngest)
            return false;

        if (IsNxlogProduct(item.Source?.Product))
            return true;

        return LooksLikeNxlogRaw(item.Raw);
    }
}
