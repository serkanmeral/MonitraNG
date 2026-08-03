namespace MngLogCollector.Application.Services.Ingest;

/// <summary>
/// Maps known agent package/event IDs to SIEM event.action values
/// (aligns with Reactor parse-rule catalog, e.g. windows.rdp.* → rdp.*).
/// </summary>
public static class AgentSecEventActionNormalizer
{
    public static string? TryNormalize(
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
