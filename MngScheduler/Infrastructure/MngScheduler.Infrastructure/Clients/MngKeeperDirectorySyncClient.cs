using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngScheduler.Application.Configuration;
using MngScheduler.Application.Interfaces;

namespace MngScheduler.Infrastructure.Clients;

public class MngKeeperDirectorySyncClient : IMngKeeperDirectorySyncClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<MngKeeperDirectorySyncClient> _logger;
    private readonly MngSchedulerSettings _settings;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public MngKeeperDirectorySyncClient(
        IHttpClientFactory httpClientFactory,
        ILogger<MngKeeperDirectorySyncClient> logger,
        IOptions<MngSchedulerSettings> settings)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _settings = settings.Value;
    }

    public async Task<MngKeeperDirectorySyncResponse> TriggerScheduledSyncAsync(
        string domainIdOrRealm,
        Dictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        var baseUrl = ResolveKeeperBaseUrl();
        var url = $"{baseUrl.TrimEnd('/')}/api/directory/sync";
        // Keeper API: enum JSON must be numeric (no JsonStringEnumConverter on API).
        var body = JsonSerializer.Serialize(new
        {
            domainId = domainIdOrRealm,
            triggeredBy = 1 // DirectorySyncTrigger.Scheduled
        }, JsonOptions);

        var client = _httpClientFactory.CreateClient("MngKeeperDirectorySync");
        client.Timeout = TimeSpan.FromSeconds(_settings.HttpClient.TimeoutSeconds);

        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };

        if (headers != null)
        {
            foreach (var header in headers)
            {
                if (string.Equals(header.Key, "Content-Type", StringComparison.OrdinalIgnoreCase))
                    continue;
                request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        _logger.LogInformation(
            "[DirectorySync] POST {Url} domain={Domain} trigger=Scheduled",
            url, domainIdOrRealm);

        using var response = await client.SendAsync(request, cancellationToken);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken);

        var result = new MngKeeperDirectorySyncResponse
        {
            StatusCode = (int)response.StatusCode,
            RawBody = Truncate(raw, 2048)
        };

        if (!string.IsNullOrWhiteSpace(raw))
        {
            try
            {
                using var doc = JsonDocument.Parse(raw);
                if (doc.RootElement.TryGetProperty("code", out var codeEl))
                    result.Code = codeEl.GetString();
                if (doc.RootElement.TryGetProperty("message", out var msgEl))
                    result.Message = msgEl.GetString();

                if (response.IsSuccessStatusCode)
                {
                    var root = doc.RootElement;
                    _logger.LogInformation(
                        "[DirectorySync] Keeper response domain={Domain} HTTP {Status} code={Code} usersCreated={UsersCreated} usersUpdated={UsersUpdated} groupsCreated={GroupsCreated} groupsUpdated={GroupsUpdated} durationMs={DurationMs}",
                        domainIdOrRealm,
                        (int)response.StatusCode,
                        result.Code,
                        TryGetInt(root, "usersCreated"),
                        TryGetInt(root, "usersUpdated"),
                        TryGetInt(root, "groupsCreated"),
                        TryGetInt(root, "groupsUpdated"),
                        TryGetLong(root, "durationMs"));
                }
                else if ((int)response.StatusCode == 409)
                {
                    _logger.LogInformation(
                        "[DirectorySync] Keeper sync already running domain={Domain} HTTP 409 code={Code}",
                        domainIdOrRealm, result.Code);
                }
                else
                {
                    _logger.LogWarning(
                        "[DirectorySync] Keeper error domain={Domain} HTTP {Status} code={Code} message={Message} body={Body}",
                        domainIdOrRealm, (int)response.StatusCode, result.Code, result.Message, result.RawBody);
                }
            }
            catch (JsonException)
            {
                result.Message = Truncate(raw, 500);
                _logger.LogWarning(
                    "[DirectorySync] Non-JSON Keeper response domain={Domain} HTTP {Status}",
                    domainIdOrRealm, (int)response.StatusCode);
            }
        }

        return result;
    }

    private static int? TryGetInt(JsonElement root, string name) =>
        root.TryGetProperty(name, out var el) && el.TryGetInt32(out var v) ? v : null;

    private static long? TryGetLong(JsonElement root, string name) =>
        root.TryGetProperty(name, out var el) && el.TryGetInt64(out var v) ? v : null;

    private string ResolveKeeperBaseUrl()
    {
        var configured = _settings.DirectorySyncOrchestration.MngKeeperBaseUrl;
        if (!string.IsNullOrWhiteSpace(configured))
            return configured;

        if (!string.IsNullOrWhiteSpace(_settings.Actors.MngKeeper))
            return _settings.Actors.MngKeeper;

        throw new InvalidOperationException(
            "MngKeeper base URL is not configured. Set MngSchedulerSettings:Actors:MngKeeper or DirectorySyncOrchestration:MngKeeperBaseUrl.");
    }

    private static string Truncate(string value, int max)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= max)
            return value;
        return value[..max] + "... [truncated]";
    }
}
