using System.Text.Json;
using MngReactor.Application.Features.Commands.Ingest;

namespace MngReactor.Application.Services.SecEvents;

/// <summary>
/// Detects legacy NXLog payloads so Reactor can refuse them.
/// Windows/Linux host telemetry must come from MngLogs agent (LogCollector), not NXLog.
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

    /// <summary>NXLog om_udp/om_tcp JSON shape: object with EventID + Hostname.</summary>
    public static bool LooksLikeNxlogJson(string? rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText))
            return false;

        var trimmed = rawText.TrimStart();
        return trimmed.StartsWith('{')
               && trimmed.Contains("\"EventID\"", StringComparison.Ordinal)
               && trimmed.Contains("\"Hostname\"", StringComparison.Ordinal);
    }

    public static bool LooksLikeNxlogJson(JsonElement raw)
    {
        if (raw.ValueKind == JsonValueKind.Object)
        {
            return raw.TryGetProperty("EventID", out _)
                   && raw.TryGetProperty("Hostname", out _);
        }

        if (raw.ValueKind == JsonValueKind.String)
            return LooksLikeNxlogJson(raw.GetString());

        // Engine sometimes forwards JSON as a stringified element; fall back to text.
        try
        {
            return LooksLikeNxlogJson(raw.GetRawText());
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    public static bool ShouldReject(SecEventIngestItem item, bool acceptNxlogIngest)
    {
        if (acceptNxlogIngest)
            return false;

        if (IsNxlogProduct(item.Source?.Product))
            return true;

        return LooksLikeNxlogJson(item.Raw);
    }
}
