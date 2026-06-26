using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngDocument.Application.Configuration;
using MngDocument.Application.Interfaces;

namespace MngDocument.Infrastructure.Services;

public interface IDomainLogoProvider
{
    Task<DomainLogoResult?> GetCurrentDomainLogoAsync(string? bearerToken, CancellationToken ct = default);
}

public sealed class DomainLogoResult
{
    public required byte[] Bytes { get; init; }
    public string Extension { get; init; } = ".png";
}

public sealed class DomainLogoProvider : IDomainLogoProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IRequestContext _ctx;
    private readonly KeeperSettings _settings;
    private readonly ILogger<DomainLogoProvider> _logger;

    public DomainLogoProvider(
        IHttpClientFactory httpClientFactory,
        IRequestContext ctx,
        IOptions<MngDocumentSettings> settings,
        ILogger<DomainLogoProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _ctx = ctx;
        _settings = settings.Value.Keeper;
        _logger = logger;
    }

    public async Task<DomainLogoResult?> GetCurrentDomainLogoAsync(string? bearerToken, CancellationToken ct = default)
    {
        var domainId = _ctx.DomainId;
        var token = bearerToken ?? _ctx.BearerToken;
        if (string.IsNullOrWhiteSpace(domainId) || string.IsNullOrWhiteSpace(token))
            return null;

        try
        {
            var client = _httpClientFactory.CreateClient("MngKeeper");
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"{_settings.BaseUrl.TrimEnd('/')}/api/domain/{Uri.EscapeDataString(domainId)}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Domain logo fetch failed: {Status}", response.StatusCode);
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            var root = doc.RootElement;

            if (TryReadProperty(root, "logo", out var logoProp))
            {
                var logo = logoProp.GetString();
                var decoded = DecodeLogoString(logo);
                if (decoded is not null)
                    return decoded;
            }

            if (TryReadProperty(root, "logoUrl", out var urlProp))
            {
                var url = urlProp.GetString();
                if (!string.IsNullOrWhiteSpace(url))
                    return await DownloadLogoAsync(url, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Domain logo fetch error for domain {DomainId}", domainId);
        }

        return null;
    }

    private async Task<DomainLogoResult?> DownloadLogoAsync(string url, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient("MngKeeper");
        using var response = await client.GetAsync(url, ct);
        if (!response.IsSuccessStatusCode)
            return null;

        var bytes = await response.Content.ReadAsByteArrayAsync(ct);
        if (bytes.Length == 0)
            return null;

        var ext = GuessExtension(bytes, response.Content.Headers.ContentType?.MediaType);
        return new DomainLogoResult { Bytes = bytes, Extension = ext };
    }

    private static DomainLogoResult? DecodeLogoString(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var payload = raw.Trim();
        if (payload.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            var comma = payload.IndexOf(',');
            if (comma < 0)
                return null;
            var meta = payload[..comma];
            payload = payload[(comma + 1)..];
            var ext = meta.Contains("jpeg", StringComparison.OrdinalIgnoreCase)
                      || meta.Contains("jpg", StringComparison.OrdinalIgnoreCase)
                ? ".jpg"
                : ".png";
            try
            {
                var bytes = Convert.FromBase64String(payload);
                return bytes.Length == 0 ? null : new DomainLogoResult { Bytes = bytes, Extension = ext };
            }
            catch
            {
                return null;
            }
        }

        try
        {
            var bytes = Convert.FromBase64String(payload);
            return bytes.Length == 0 ? null : new DomainLogoResult { Bytes = bytes, Extension = GuessExtension(bytes, null) };
        }
        catch
        {
            return null;
        }
    }

    private static string GuessExtension(byte[] bytes, string? mediaType)
    {
        if (mediaType?.Contains("jpeg", StringComparison.OrdinalIgnoreCase) == true
            || mediaType?.Contains("jpg", StringComparison.OrdinalIgnoreCase) == true)
            return ".jpg";

        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8)
            return ".jpg";
        return ".png";
    }

    private static bool TryReadProperty(JsonElement root, string name, out JsonElement value)
    {
        if (root.TryGetProperty(name, out value))
            return true;
        foreach (var prop in root.EnumerateObject())
        {
            if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = prop.Value;
                return true;
            }
        }

        value = default;
        return false;
    }
}
