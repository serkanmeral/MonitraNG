namespace MngLogs.Agent.Configuration;

public static class ProcessNameNormalizer
{
    public static string Normalize(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        var n = name.Trim();
        n = n.Replace('\\', '/');
        var slash = n.LastIndexOf('/');
        if (slash >= 0 && slash < n.Length - 1)
            n = n[(slash + 1)..];

        if (n.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            n = n[..^4];

        return n.Trim();
    }
}
