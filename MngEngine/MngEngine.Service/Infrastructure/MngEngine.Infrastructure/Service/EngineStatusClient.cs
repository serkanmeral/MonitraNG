using System.Text.Json;
using MngEngine.Application.Interfaces;
using MngEngine.Infrastructure.Context;
using RestSharp;
using Serilog;

namespace MngEngine.Infrastructure.Service;

public class EngineStatusClient : IEngineStatusClient
{
    private readonly ILogger _logger;
    private readonly IEngineConfigProvider _configProvider;
    private readonly IAccessTokenProvider _tokenProvider;
    private readonly IRestContext _context;

    public EngineStatusClient(
        ILogger logger,
        IEngineConfigProvider configProvider,
        IAccessTokenProvider tokenProvider,
        IRestContext context)
    {
        _logger = logger;
        _configProvider = configProvider;
        _tokenProvider = tokenProvider;
        _context = context;
    }

    public async Task<bool> SendStatusAsync(EngineStatusPayload payload, CancellationToken ct = default)
    {
        var token = await _tokenProvider.GetAccessTokenAsync(ct);
        if (string.IsNullOrEmpty(token))
        {
            _logger.Warning("EngineStatus: Token alınamadı");
            return false;
        }

        var baseUrl = _configProvider.GetConfig()?.ServerUrl;
        if (string.IsNullOrEmpty(baseUrl))
        {
            _logger.Warning("EngineStatus: ServerUrl yok");
            return false;
        }

        var body = new
        {
            engineId = payload.EngineId,
            domain = payload.Domain,
            timestamp = payload.Timestamp,
            health = payload.Health,
            hostAddress = payload.HostAddress,
            errors = payload.Errors.Select(e => new
            {
                assetId = e.AssetId,
                agentId = e.AgentId,
                errorCode = e.ErrorCode,
                message = e.Message,
                occurredAt = e.OccurredAt
            }).ToList(),
            queueDepth = payload.QueueDepth,
            assetCount = payload.AssetCount
        };

        var request = new RestRequest("/api/v1/engine/status", Method.Post)
            .AddJsonBody(body)
            .AddHeader("Content-Type", "application/json")
            .AddHeader("Authorization", "Bearer " + token);

        try
        {
            var client = _context.RestClient(baseUrl);
            var response = await client.ExecuteAsync(request, ct);
            if (response.IsSuccessful)
            {
                _logger.Debug("EngineStatus gönderildi engineId={EngineId}", payload.EngineId);
                return true;
            }
            _logger.Warning("EngineStatus HTTP {StatusCode}: {Content}", response.StatusCode, response.Content);
            return false;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "EngineStatus gönderimi başarısız");
            return false;
        }
    }
}
