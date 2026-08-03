namespace MngReactor.Application.Services.SecEvents;

/// <summary>LocalSessionManager Operational IDs used by agent package rdp-session.</summary>
public static class SecEventRdpActionCodes
{
    public static readonly string[] EventCodes = ["21", "23", "24", "25"];

    public const string ProductRdpSession = "rdp-session";

    public static bool IsRdpActionPrefix(string? prefix) =>
        !string.IsNullOrWhiteSpace(prefix)
        && prefix.Trim().Equals("rdp.", StringComparison.OrdinalIgnoreCase);

    public static string? ActionForCode(string? code) =>
        (code ?? string.Empty).Trim() switch
        {
            "21" => "rdp.logon",
            "23" => "rdp.logoff",
            "24" => "rdp.disconnect",
            "25" => "rdp.reconnect",
            _ => null
        };
}
