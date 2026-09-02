using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using MngLogCollector.Application.Abstractions.AgentPackages;
using MngLogCollector.Application.Configuration;
using MngLogCollector.Application.Contracts.AgentPackages;

namespace MngLogCollector.Application.Services.AgentPackages;

public sealed class AgentPackageCatalog(IOptions<MngLogCollectorSettings> options) : IAgentPackageCatalog
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly Regex VersionInName = new(@"(\d+\.\d+\.\d+(?:\.\d+)?)", RegexOptions.Compiled);

    private readonly ConcurrentDictionary<string, (long Ticks, long Length, string Hash)> _hashCache = new();

    public AgentPackageCatalogResponse GetCatalog(string? requestBaseUrl)
    {
        var settings = options.Value.AgentPackages ?? new AgentPackagesSettings();
        var collectorBase = NormalizeBase(settings.PublicBaseUrl, requestBaseUrl);
        var packages = Scan(settings.Directory)
            .Select(p => ToDto(p, collectorBase))
            .ToList();

        return new AgentPackageCatalogResponse
        {
            CollectorBaseUrl = collectorBase,
            Packages = packages
        };
    }

    public AgentPackageFile? GetFile(string id)
    {
        var settings = options.Value.AgentPackages ?? new AgentPackagesSettings();
        var match = Scan(settings.Directory)
            .FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));
        if (match is null || !File.Exists(match.AbsolutePath))
            return null;

        return new AgentPackageFile
        {
            Id = match.Id,
            FileName = match.DisplayFileName,
            AbsolutePath = match.AbsolutePath,
            ContentType = match.Id == "linux" ? "application/gzip" : "application/octet-stream"
        };
    }

    private List<ScannedPackage> Scan(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            return [];

        var fromManifest = TryReadManifest(directory);
        if (fromManifest.Count > 0)
            return fromManifest;

        var list = new List<ScannedPackage>();
        var windows = PickFile(directory, ["windows.msi"], "*.msi", name =>
            name.Contains("mnglogs", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".msi", StringComparison.OrdinalIgnoreCase));
        var linux = PickFile(directory, ["linux.tar.gz"], "*.tar.gz", name =>
            name.Contains("linux", StringComparison.OrdinalIgnoreCase)
            || name.Contains("mnglogs", StringComparison.OrdinalIgnoreCase));

        if (windows != null)
            list.Add(Describe("windows", "windows", windows));
        if (linux != null)
            list.Add(Describe("linux", "linux", linux));

        return list;
    }

    private List<ScannedPackage> TryReadManifest(string directory)
    {
        var path = Path.Combine(directory, "manifest.json");
        if (!File.Exists(path))
            return [];

        try
        {
            var json = File.ReadAllText(path);
            var doc = JsonSerializer.Deserialize<ManifestFile>(json, JsonOptions);
            if (doc?.Packages is null || doc.Packages.Count == 0)
                return [];

            var list = new List<ScannedPackage>();
            var root = Path.GetFullPath(directory);
            foreach (var row in doc.Packages)
            {
                var id = (row.Id ?? "").Trim().ToLowerInvariant();
                if (id is not ("windows" or "linux"))
                    continue;

                var fileName = (row.FileName ?? "").Trim();
                if (string.IsNullOrWhiteSpace(fileName)
                    || fileName.Contains("..", StringComparison.Ordinal)
                    || Path.IsPathRooted(fileName))
                    continue;

                var full = Path.GetFullPath(Path.Combine(directory, fileName));
                if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !File.Exists(full))
                    continue;

                var info = new FileInfo(full);
                var display = string.IsNullOrWhiteSpace(row.DisplayFileName)
                    ? Path.GetFileName(full)
                    : row.DisplayFileName.Trim();
                var sha = string.IsNullOrWhiteSpace(row.Sha256) ? HashFile(full, info) : row.Sha256.Trim();
                var version = string.IsNullOrWhiteSpace(row.Version) ? ExtractVersion(display) : row.Version.Trim();

                list.Add(new ScannedPackage(
                    id,
                    string.IsNullOrWhiteSpace(row.Platform) ? id : row.Platform.Trim(),
                    display,
                    version,
                    sha,
                    info.Length,
                    full));
            }

            return list;
        }
        catch
        {
            return [];
        }
    }

    private ScannedPackage Describe(string id, string platform, FileInfo file)
    {
        var name = file.Name;
        return new ScannedPackage(
            id,
            platform,
            name,
            ExtractVersion(name),
            HashFile(file.FullName, file),
            file.Length,
            file.FullName);
    }

    private static FileInfo? PickFile(
        string directory,
        IReadOnlyList<string> preferredNames,
        string wildcard,
        Func<string, bool> extraFilter)
    {
        foreach (var name in preferredNames)
        {
            var preferred = new FileInfo(Path.Combine(directory, name));
            if (preferred.Exists)
                return preferred;
        }

        return new DirectoryInfo(directory)
            .EnumerateFiles(wildcard, SearchOption.TopDirectoryOnly)
            .Where(f => extraFilter(f.Name))
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .FirstOrDefault();
    }

    private string HashFile(string path, FileInfo info)
    {
        if (_hashCache.TryGetValue(path, out var cached)
            && cached.Ticks == info.LastWriteTimeUtc.Ticks
            && cached.Length == info.Length)
        {
            return cached.Hash;
        }

        using var stream = File.OpenRead(path);
        var hash = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        _hashCache[path] = (info.LastWriteTimeUtc.Ticks, info.Length, hash);
        return hash;
    }

    private static string ExtractVersion(string fileName)
    {
        var m = VersionInName.Match(fileName);
        return m.Success ? m.Groups[1].Value : "";
    }

    private static AgentPackageDto ToDto(ScannedPackage p, string collectorBase)
    {
        var path = $"/api/v1/agent/packages/{p.Id}";
        return new AgentPackageDto
        {
            Id = p.Id,
            Platform = p.Platform,
            FileName = p.DisplayFileName,
            Version = p.Version,
            Sha256 = p.Sha256,
            SizeBytes = p.SizeBytes,
            DownloadPath = path,
            DownloadUrl = string.IsNullOrWhiteSpace(collectorBase) ? path : $"{collectorBase}{path}"
        };
    }

    private static string NormalizeBase(string configured, string? requestBase)
    {
        var raw = string.IsNullOrWhiteSpace(configured) ? requestBase : configured;
        return (raw ?? "").Trim().TrimEnd('/');
    }

    private sealed record ScannedPackage(
        string Id,
        string Platform,
        string DisplayFileName,
        string Version,
        string Sha256,
        long SizeBytes,
        string AbsolutePath);

    private sealed class ManifestFile
    {
        public List<ManifestPackage> Packages { get; set; } = [];
    }

    private sealed class ManifestPackage
    {
        public string? Id { get; set; }
        public string? Platform { get; set; }
        public string? FileName { get; set; }
        public string? DisplayFileName { get; set; }
        public string? Version { get; set; }
        public string? Sha256 { get; set; }
    }
}
