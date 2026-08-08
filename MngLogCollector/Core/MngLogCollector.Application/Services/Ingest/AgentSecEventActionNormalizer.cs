namespace MngLogCollector.Application.Services.Ingest;

/// <summary>
/// Resolves observation keys for agent ingest events.
/// Prefer optional semantic keys (e.g. rdp.logon); otherwise use the package id
/// so arbitrary Event IDs still publish and Alarm flows can filter on eventCode.
/// </summary>
public static class AgentSecEventActionNormalizer
{
    public const string FallbackWindowsEventLogKey = "windows.eventlog";

    /// <summary>
    /// Resolves the observation key for an agent event.
    /// Returns null only when there is nothing meaningful to publish.
    /// </summary>
    public static string? ResolveObservationKey(
        string? sourceProduct,
        string? sourceType,
        string? eventCode,
        string? messageFallback)
    {
        var semantic = TryNormalizeSemantic(sourceProduct, sourceType, eventCode, messageFallback);
        if (!string.IsNullOrWhiteSpace(semantic))
            return semantic;

        var product = (sourceProduct ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(product))
            return product;

        // Last resort: still publish when Event ID exists (no package stamp).
        return string.IsNullOrWhiteSpace(eventCode) ? null : FallbackWindowsEventLogKey;
    }

    /// <summary>
    /// Optional semantic map for well-known events (RDP today).
    /// Unmapped events must not be dropped — use <see cref="ResolveObservationKey"/>.
    /// </summary>
    public static string? TryNormalizeSemantic(
        string? sourceProduct,
        string? sourceType,
        string? eventCode,
        string? messageFallback)
    {
        var product = (sourceProduct ?? string.Empty).Trim();
        var code = (eventCode ?? string.Empty).Trim();

        if (IsRdpSession(product, sourceType, messageFallback) && !string.IsNullOrEmpty(code))
        {
            return code switch
            {
                "21" => "rdp.logon",
                "23" => "rdp.logoff",
                "24" => "rdp.disconnect",
                "25" => "rdp.reconnect",
                _ => null
            };
        }

        return null;
    }

    /// <summary>Backward-compatible alias for semantic-only lookup. Prefer <see cref="ResolveObservationKey"/>. </summary>
    public static string? TryNormalize(
        string? sourceProduct,
        string? sourceType,
        string? eventCode,
        string? messageFallback) =>
        TryNormalizeSemantic(sourceProduct, sourceType, eventCode, messageFallback);

    private static bool IsRdpSession(string product, string? sourceType, string? messageOrPreview)
    {
        if (product.Equals("rdp-session", StringComparison.OrdinalIgnoreCase)
            || product.Equals("rdp-sessions", StringComparison.OrdinalIgnoreCase))
            return true;

        if (!string.IsNullOrWhiteSpace(messageOrPreview)
            && messageOrPreview.Contains("LocalSessionManager", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }
}
