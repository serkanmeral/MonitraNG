using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MngDocument.Application.Interfaces;

namespace MngDocument.Infrastructure.Services;

/// <summary>
/// Kayıt bittikten sonra aynı etki alanı kapısını bekleyip tek belgeyi indeksler.
/// </summary>
public sealed class DocumentAutoIndex : IDocumentAutoIndex
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IRequestContext _ctx;
    private readonly ILogger<DocumentAutoIndex> _logger;

    public DocumentAutoIndex(
        IServiceScopeFactory scopes,
        IRequestContext ctx,
        ILogger<DocumentAutoIndex> logger)
    {
        _scopes = scopes;
        _ctx = ctx;
        _logger = logger;
    }

    public void Schedule(string resourceId, string? bearerToken = null)
    {
        if (string.IsNullOrWhiteSpace(resourceId))
            return;

        var token = string.IsNullOrWhiteSpace(bearerToken) ? _ctx.BearerToken : bearerToken.Trim();
        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogWarning("Auto index skipped for {ResourceId}: bearer token is missing", resourceId);
            return;
        }

        var domain = ResolveDomain(token);
        if (string.IsNullOrWhiteSpace(domain))
        {
            _logger.LogWarning("Auto index skipped for {ResourceId}: domain is missing", resourceId);
            return;
        }

        var id = resourceId.Trim();
        _ = Task.Run(() => IndexAsync(id, token, domain));
    }

    private async Task IndexAsync(string id, string token, string domain)
    {
        var gate = DocumentJobGate.For(domain);
        var entered = false;
        try
        {
            await gate.WaitAsync().ConfigureAwait(false);
            entered = true;

            await using var scope = _scopes.CreateAsyncScope();
            var runner = scope.ServiceProvider.GetRequiredService<DocumentIndexBatchRunner>();
            var outcome = await runner.IndexOneAsync(id, string.Empty, token, domain, CancellationToken.None)
                .ConfigureAwait(false);
            _logger.LogInformation("Auto index {ResourceId}: {Outcome}", id, outcome);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Auto index failed for {ResourceId}", id);
        }
        finally
        {
            if (entered)
                gate.Release();
        }
    }

    private string? ResolveDomain(string token)
    {
        if (!string.IsNullOrWhiteSpace(_ctx.DomainName))
            return _ctx.DomainName.Trim();

        return DomainFromToken(token);
    }

    private static string? DomainFromToken(string token)
    {
        var parts = token.Split('.');
        if (parts.Length < 2)
            return null;

        try
        {
            var payload = parts[1].Replace('-', '+').Replace('_', '/');
            switch (payload.Length % 4)
            {
                case 2: payload += "=="; break;
                case 3: payload += "="; break;
            }

            using var json = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(payload)));
            if (json.RootElement.TryGetProperty("domain_name", out var domain)
                && domain.ValueKind == JsonValueKind.String)
            {
                var value = domain.GetString();
                return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
            }
        }
        catch (FormatException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }
}
