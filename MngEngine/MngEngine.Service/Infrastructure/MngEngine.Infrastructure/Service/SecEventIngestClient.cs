using System.Text.Json;
using System.Text.Json.Nodes;
using MngEngine.Application.Features.SecEvents;
using MngEngine.Application.Interfaces;
using MngEngine.Infrastructure.Context;
using RestSharp;
using Serilog;

namespace MngEngine.Infrastructure.Service;

public sealed class SecEventIngestClient : ISecEventIngestClient
{
    private readonly ILogger _logger;
    private readonly IEngineConfigProvider _configProvider;
    private readonly IAccessTokenProvider _tokenProvider;
    private readonly IRestContext _context;
    private readonly ICryptProcessing _cryptProcessing;

    public SecEventIngestClient(
        ILogger logger,
        IEngineConfigProvider configProvider,
        IAccessTokenProvider tokenProvider,
        IRestContext context,
        ICryptProcessing cryptProcessing)
    {
        _logger = logger;
        _configProvider = configProvider;
        _tokenProvider = tokenProvider;
        _context = context;
        _cryptProcessing = cryptProcessing;
    }

    public async Task<SecEventIngestResult> SendAsync(SecEventIngestRequest request, CancellationToken ct = default)
    {
        if (request?.Items == null || request.Items.Count == 0)
            return new SecEventIngestResult { Success = true };

        var token = await _tokenProvider.GetAccessTokenAsync(ct);
        if (string.IsNullOrEmpty(token))
        {
            _logger.Warning("SecEvent ingest: Token alınamadı");
            return new SecEventIngestResult
            {
                Success = false,
                Rejected = request.Items.Count,
                ErrorMessage = "Token alınamadı"
            };
        }

        var baseUrl = _configProvider.GetConfig()?.ServerUrl;
        if (string.IsNullOrEmpty(baseUrl))
        {
            _logger.Warning("SecEvent ingest: Reactor URL yapılandırılmamış");
            return new SecEventIngestResult
            {
                Success = false,
                Rejected = request.Items.Count,
                ErrorMessage = "Reactor URL yapılandırılmamış"
            };
        }

        RestRequest restRequest;
        var config = _configProvider.GetConfig();
        var useEncryption = !string.IsNullOrEmpty(config?.CompressPrk) && !string.IsNullOrEmpty(config?.CompressPbk);

        if (useEncryption)
        {
            var json = JsonSerializer.Serialize(request);
            var encrypted = await _cryptProcessing.CompressAndEncrypt(json, config!.CompressPrk!, config.CompressPbk!);
            var base64 = Convert.ToBase64String(encrypted);
            restRequest = new RestRequest("/api/v1/ingest/sec-events", Method.Post)
                .AddStringBody(base64, DataFormat.None)
                .AddHeader("Content-Type", "text/plain")
                .AddHeader("X-Payload-Format", "encrypted")
                .AddHeader("Authorization", "Bearer " + token);
        }
        else
        {
            restRequest = new RestRequest("/api/v1/ingest/sec-events", Method.Post)
                .AddJsonBody(request)
                .AddHeader("Content-Type", "application/json")
                .AddHeader("Authorization", "Bearer " + token);
        }

        try
        {
            var client = _context.RestClient(baseUrl);
            var response = await client.ExecuteAsync(restRequest, ct);
            if (response.IsSuccessful && !string.IsNullOrEmpty(response.Content))
            {
                var doc = JsonNode.Parse(response.Content)?.AsObject();
                return new SecEventIngestResult
                {
                    Success = true,
                    Accepted = doc?["accepted"]?.GetValue<int>() ?? 0,
                    Rejected = doc?["rejected"]?.GetValue<int>() ?? 0,
                    Published = doc?["published"]?.GetValue<int>() ?? 0
                };
            }

            _logger.Warning("SecEvent ingest HTTP {StatusCode}: {Content}", response.StatusCode, response.Content);
            return new SecEventIngestResult
            {
                Success = false,
                Rejected = request.Items.Count,
                ErrorMessage = response.ErrorMessage ?? response.Content ?? $"HTTP {(int)response.StatusCode}"
            };
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "SecEvent ingest gönderimi başarısız");
            return new SecEventIngestResult
            {
                Success = false,
                Rejected = request.Items.Count,
                ErrorMessage = ex.Message
            };
        }
    }
}
