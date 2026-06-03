using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngReactor.Application.Abstractions.Data;
using MngReactor.Application.Abstractions.Engine;
using MngReactor.Application.Configuration;
using MngReactor.Application.Features.Engine;

namespace MngReactor.Persistence.Services.Engine;

public class EngineStatusProcessing : IEngineStatusProcessing
{
    private readonly ILogger<EngineStatusProcessing> _logger;
    private readonly IOptions<MngReactorSettings> _options;
    private readonly IDataGatewayClient _dg;

    public EngineStatusProcessing(
        ILogger<EngineStatusProcessing> logger,
        IOptions<MngReactorSettings> options,
        IDataGatewayClient dg)
    {
        _logger = logger;
        _options = options;
        _dg = dg;
    }

    public async Task<bool> ProcessStatusAsync(
        EngineStatusRequest request,
        string domainFromToken,
        string? accessToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(request.EngineId) || string.IsNullOrEmpty(request.Domain))
        {
            _logger.LogWarning("EngineStatus: engineId veya domain bos");
            return false;
        }

        if (!string.Equals(request.Domain, domainFromToken, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("EngineStatus: domain uyusmaz request.Domain={ReqDomain} token.Domain={TokenDomain}", request.Domain, domainFromToken);
            return false;
        }

        var token = ResolveToken(domainFromToken, accessToken);
        if (string.IsNullOrEmpty(token))
        {
            _logger.LogWarning("EngineStatus: token bulunamadi domain={Domain}", domainFromToken);
            return false;
        }

        var existing = await _dg.GetByIdAsync("mon_engines", request.EngineId, token, cancellationToken);
        if (existing == null)
        {
            _logger.LogWarning("EngineStatus: engine bulunamadi engineId={EngineId} domain={Domain}", request.EngineId, domainFromToken);
            return false;
        }

        var timestamp = request.Timestamp ?? DateTime.UtcNow;
        var data = new JsonObject
        {
            ["lastSeenAt"] = JsonValue.Create(timestamp),
            ["health"] = request.Health ?? (request.Errors != null && request.Errors.Count > 0 ? "degraded" : "ok")
        };
        if (!string.IsNullOrEmpty(request.HostAddress))
            data["hostAddress"] = request.HostAddress;

        var errorsArray = new JsonArray();
        if (request.Errors != null && request.Errors.Count > 0)
        {
            foreach (var e in request.Errors.Take(100))
            {
                errorsArray.Add(new JsonObject
                {
                    ["assetId"] = e.AssetId,
                    ["agentId"] = e.AgentId,
                    ["errorCode"] = e.ErrorCode,
                    ["message"] = e.Message,
                    ["occurredAt"] = JsonValue.Create(e.OccurredAt)
                });
            }
        }
        data["lastErrors"] = errorsArray;

        try
        {
            var ok = await _dg.UpdateAsync("mon_engines", request.EngineId, data, token, cancellationToken, skipEventPublish: true);
            if (ok)
                _logger.LogDebug("EngineStatus: lastSeenAt guncellendi engineId={EngineId}", request.EngineId);
            return ok;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "EngineStatus: mon_engines guncelleme hatasi engineId={EngineId}", request.EngineId);
            return false;
        }
    }

    private string? ResolveToken(string domain, string? accessToken)
    {
        if (!string.IsNullOrEmpty(accessToken)) return accessToken;
        return _options.Value?.DataGateway?.DomainTokens?.GetValueOrDefault(domain);
    }
}
