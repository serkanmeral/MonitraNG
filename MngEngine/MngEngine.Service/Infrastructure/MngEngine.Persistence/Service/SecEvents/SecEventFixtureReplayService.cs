using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using MngEngine.Application.Features.SecEvents;
using MngEngine.Application.Interfaces;
using MngEngine.Persistence.Options;
using Serilog;

namespace MngEngine.Persistence.Service.SecEvents;

public sealed class SecEventFixtureReplayService : ISecEventFixtureReplay
{
    private readonly ILogger _logger;
    private readonly ISecEventIngestClient _ingestClient;
    private readonly SecEventFixtureOptions _options;

    public SecEventFixtureReplayService(
        ILogger logger,
        ISecEventIngestClient ingestClient,
        IOptions<SecEventFixtureOptions> options)
    {
        _logger = logger;
        _ingestClient = ingestClient;
        _options = options.Value;
    }

    public SecEventIngestRequest BuildFixtureRequest(DateTime? receivedAt = null)
    {
        var at = receivedAt ?? DateTime.UtcNow;
        var fixtureRoot = ResolveFixtureRoot();

        var firewallRaw = ReadFixtureText(fixtureRoot, "firewall_deny.syslog.txt");
        var windowsRaw = ReadWindowsFixture(fixtureRoot, "windows_4625_failed_logon.json", at);
        var unknownRaw = ReadFixtureText(fixtureRoot, "unparseable_01.txt");

        return new SecEventIngestRequest
        {
            Items =
            [
                new SecEventIngestItem
                {
                    ReceivedAt = at,
                    Source = new SecEventIngestSource
                    {
                        Type = "firewall",
                        Product = "generic-syslog",
                        Host = "fw01"
                    },
                    Raw = firewallRaw
                },
                new SecEventIngestItem
                {
                    ReceivedAt = at,
                    Source = new SecEventIngestSource
                    {
                        Type = "ad",
                        Product = "windows",
                        Host = "dc01"
                    },
                    Raw = windowsRaw
                },
                new SecEventIngestItem
                {
                    ReceivedAt = at,
                    Source = new SecEventIngestSource
                    {
                        Type = "unknown",
                        Product = "unknown",
                        Host = "host01"
                    },
                    Raw = unknownRaw
                }
            ]
        };
    }

    public async Task<SecEventIngestResult> ReplayFixturesAsync(CancellationToken ct = default)
    {
        var request = BuildFixtureRequest();
        _logger.Information("SecEvent fixture replay: {Count} item Reactor'a gönderiliyor", request.Items.Count);
        var result = await _ingestClient.SendAsync(request, ct);
        if (result.Success)
            _logger.Information("SecEvent fixture replay tamamlandı. Accepted={Accepted}, Published={Published}",
                result.Accepted, result.Published);
        else
            _logger.Warning("SecEvent fixture replay başarısız: {Error}", result.ErrorMessage);

        return result;
    }

    private string ResolveFixtureRoot()
    {
        var configured = _options.Path?.Trim();
        if (string.IsNullOrEmpty(configured))
            configured = "fixtures/siem";

        if (Path.IsPathRooted(configured) && Directory.Exists(configured))
            return configured;

        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, configured),
            Path.Combine(Directory.GetCurrentDirectory(), configured),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "tests", "fixtures", "siem"))
        };

        foreach (var candidate in candidates)
        {
            if (Directory.Exists(candidate)
                && File.Exists(Path.Combine(candidate, "firewall_deny.syslog.txt")))
                return candidate;
        }

        throw new DirectoryNotFoundException(
            $"SIEM fixture dizini bulunamadi. Denenen: {string.Join("; ", candidates)}");
    }

    private static string ReadFixtureText(string root, string fileName)
    {
        var path = Path.Combine(root, fileName);
        if (!File.Exists(path))
            throw new FileNotFoundException($"Fixture eksik: {path}");

        return File.ReadAllText(path).TrimEnd();
    }

    private static JsonObject ReadWindowsFixture(string root, string fileName, DateTime receivedAt)
    {
        var path = Path.Combine(root, fileName);
        if (!File.Exists(path))
            throw new FileNotFoundException($"Fixture eksik: {path}");

        var node = JsonNode.Parse(File.ReadAllText(path))?.AsObject()
                   ?? throw new InvalidOperationException($"Fixture JSON gecersiz: {path}");

        node["TimeCreated"] = receivedAt.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        return node;
    }
}
