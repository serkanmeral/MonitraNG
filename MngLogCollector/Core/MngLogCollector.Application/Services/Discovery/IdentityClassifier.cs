namespace MngLogCollector.Application.Services.Discovery;

public sealed class IdentityClassification
{
    public required string OsFamilyHint { get; init; }
    public required string DeviceRoleHint { get; init; }
    public required string IdentityConfidence { get; init; }
    public required string IdentitySummary { get; init; }
}

/// <summary>
/// Combines TCP port profile + lite service signals into OS/role/confidence.
/// Prefer admitting "unknown" over inventing identity.
/// </summary>
public static class IdentityClassifier
{
    public static IdentityClassification Classify(
        IReadOnlyList<int> openPorts,
        string portOsHint,
        ServiceFingerprintSignals? signals)
    {
        var title = (signals?.HttpTitle ?? "").ToLowerInvariant();
        var tls = (signals?.TlsCommonName ?? "").ToLowerInvariant();
        var ssh = (signals?.SshBanner ?? "").ToLowerInvariant();
        var blob = $"{title} {tls} {ssh}";

        var os = NormalizeOs(portOsHint);
        var role = "unknown";
        var confidence = "low";
        var reasons = new List<string>();

        if (LooksLikePrinter(blob))
        {
            role = "printer";
            confidence = "high";
            reasons.Add("printer signal");
        }
        else if (LooksLikeNetworkGear(blob))
        {
            role = "network_gear";
            confidence = "medium";
            reasons.Add("network appliance signal");
        }
        else if (openPorts.Contains(3389) || openPorts.Contains(5985))
        {
            role = "workstation";
            if (openPorts.Contains(445) && openPorts.Contains(3389))
                role = "workstation";
            reasons.Add("RDP/WinRM");
        }
        else if (openPorts.Contains(445) && !openPorts.Contains(22))
        {
            role = "server";
            reasons.Add("SMB");
        }
        else if (openPorts.Contains(22) && !HasWindowsPorts(openPorts))
        {
            role = "server";
            reasons.Add("SSH");
        }
        else if (openPorts.Contains(80) || openPorts.Contains(443))
        {
            role = "iot_or_other";
            reasons.Add("web");
        }

        // Refine OS from banners
        if (ssh.Contains("openssh") || ssh.Contains("ubuntu") || ssh.Contains("debian"))
        {
            os = "linux";
            confidence = Bump(confidence);
            reasons.Add("SSH banner");
        }
        else if (HasWindowsPorts(openPorts) && !openPorts.Contains(22))
        {
            os = "windows";
            confidence = confidence == "low" ? "medium" : confidence;
            reasons.Add("Windows ports");
        }
        else if (HasWindowsPorts(openPorts) && openPorts.Contains(22))
        {
            os = "windows";
            confidence = "medium";
            reasons.Add("Windows ports + SSH");
        }
        else if (openPorts.Contains(22) && !HasWindowsPorts(openPorts))
        {
            os = "linux";
            confidence = confidence == "low" ? "medium" : confidence;
        }

        if (!string.IsNullOrWhiteSpace(signals?.HttpTitle) || !string.IsNullOrWhiteSpace(signals?.TlsCommonName))
            confidence = Bump(confidence);

        if (os == "unknown" && role == "unknown" && openPorts.Count > 0)
            confidence = "low";

        if (role == "printer" || (os != "unknown" && confidence != "low" && reasons.Count >= 2))
            confidence = confidence == "low" ? "medium" : confidence;

        if ((os == "windows" || os == "linux") &&
            (HasWindowsPorts(openPorts) && openPorts.Contains(3389) ||
             (!string.IsNullOrWhiteSpace(ssh) && os == "linux")))
        {
            if (confidence != "high" && reasons.Count >= 1)
                confidence = Bump(confidence);
        }

        // Strong combo → high
        if (os == "windows" && openPorts.Contains(445) && (openPorts.Contains(3389) || openPorts.Contains(5985)))
            confidence = "high";
        if (os == "linux" && !string.IsNullOrWhiteSpace(ssh) && ssh.Contains("openssh"))
            confidence = confidence == "low" ? "medium" : confidence;
        if (role == "printer")
            confidence = "high";

        var osLabel = os switch
        {
            "windows" => "Windows",
            "linux" => "Linux",
            _ => "Unknown OS"
        };
        var roleLabel = role switch
        {
            "printer" => "printer",
            "network_gear" => "network gear",
            "workstation" => "workstation",
            "server" => "server",
            "iot_or_other" => "web/IoT",
            _ => "unknown role"
        };
        var confLabel = confidence switch
        {
            "high" => "high confidence",
            "medium" => "medium confidence",
            _ => "low confidence"
        };

        var summary = $"{osLabel} · {roleLabel} · {confLabel}";
        if (!string.IsNullOrWhiteSpace(signals?.HttpTitle))
            summary += $" · \"{Truncate(signals.HttpTitle!, 40)}\"";

        return new IdentityClassification
        {
            OsFamilyHint = os,
            DeviceRoleHint = role,
            IdentityConfidence = confidence,
            IdentitySummary = summary
        };
    }

    private static string NormalizeOs(string hint)
    {
        var h = (hint ?? "").Trim().ToLowerInvariant();
        if (h is "windows" or "linux" or "unknown") return h;
        if (h.Contains("windows")) return "windows";
        if (h.Contains("linux") || h.Contains("ubuntu") || h.Contains("debian")) return "linux";
        return "unknown";
    }

    private static bool HasWindowsPorts(IReadOnlyList<int> ports) =>
        ports.Contains(445) || ports.Contains(3389) || ports.Contains(5985);

    private static bool LooksLikePrinter(string blob) =>
        blob.Contains("printer")
        || blob.Contains("laserjet")
        || blob.Contains("officejet")
        || blob.Contains("brother")
        || blob.Contains("epson")
        || blob.Contains("canon")
        || blob.Contains("kyocera")
        || blob.Contains("xerox")
        || blob.Contains("hp ")
        || blob.Contains("hewlett");

    private static bool LooksLikeNetworkGear(string blob) =>
        blob.Contains("cisco")
        || blob.Contains("mikrotik")
        || blob.Contains("ubiquiti")
        || blob.Contains("unifi")
        || blob.Contains("router")
        || blob.Contains("gateway")
        || blob.Contains("firewall")
        || blob.Contains("switch");

    private static string Bump(string c) => c switch
    {
        "low" => "medium",
        "medium" => "high",
        _ => c
    };

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "…";
}
