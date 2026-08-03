namespace MngLogs.Agent.EventLog;

public sealed class CatalogRefreshResult
{
    public bool Ok { get; init; }
    public bool NotModified { get; init; }
    public string Source { get; init; } = "builtin";
    public string? Version { get; init; }
    public int PackageCount { get; init; }
    public int OptionalCount { get; init; }
    public string? Message { get; init; }

    public static CatalogRefreshResult Succeeded(
        string source,
        string? version,
        int packages,
        int optionals,
        string? message = null) => new()
    {
        Ok = true,
        Source = source,
        Version = version,
        PackageCount = packages,
        OptionalCount = optionals,
        Message = message
    };

    public static CatalogRefreshResult Unchanged(string source, string? version, int packages, int optionals) =>
        new()
        {
            Ok = true,
            NotModified = true,
            Source = source,
            Version = version,
            PackageCount = packages,
            OptionalCount = optionals,
            Message = "Not modified (ETag)"
        };

    public static CatalogRefreshResult Failed(string message, string source, string? version, int packages, int optionals) =>
        new()
        {
            Ok = false,
            Source = source,
            Version = version,
            PackageCount = packages,
            OptionalCount = optionals,
            Message = message
        };
}
