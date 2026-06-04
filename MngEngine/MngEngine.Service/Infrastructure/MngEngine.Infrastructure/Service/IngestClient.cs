using System.Text.Json;
using System.Text.Json.Nodes;
using MngEngine.Application.Features.Ingest;
using MngEngine.Application.Interfaces;
using MngEngine.Infrastructure.Context;
using RestSharp;
using Serilog;

namespace MngEngine.Infrastructure.Service;

public class IngestClient : IIngestClient
{
    private readonly ILogger _logger;
    private readonly IEngineConfigProvider _configProvider;
    private readonly IAccessTokenProvider _tokenProvider;
    private readonly IRestContext _context;
    private readonly ICryptProcessing _cryptProcessing;

    public IngestClient(
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

    public async Task<IngestResult> SendAsync(IngestMetricsRequest request, CancellationToken ct = default)
    {
        if (request?.Batches == null || request.Batches.Count == 0)
            return new IngestResult { Success = true, SavedCount = 0, FailedCount = 0 };

        var token = await _tokenProvider.GetAccessTokenAsync(ct);
        if (string.IsNullOrEmpty(token))
        {
            _logger.Warning("Ingest: Token alınamadı");
            return new IngestResult
            {
                Success = false,
                FailedCount = request.Batches.Sum(b => b.Metrics.Count),
                ErrorMessage = "Token alınamadı"
            };
        }

        var baseUrl = GetReactorBaseUrl();
        if (string.IsNullOrEmpty(baseUrl))
        {
            _logger.Warning("Ingest: Reactor URL yapılandırılmamış");
            return new IngestResult
            {
                Success = false,
                FailedCount = request.Batches.Sum(b => b.Metrics.Count),
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
            restRequest = new RestRequest("/api/v1/ingest/metrics", Method.Post)
                .AddStringBody(base64, DataFormat.None)
                .AddHeader("Content-Type", "text/plain")
                .AddHeader("X-Payload-Format", "encrypted")
                .AddHeader("Authorization", "Bearer " + token);
        }
        else
        {
            restRequest = new RestRequest("/api/v1/ingest/metrics", Method.Post)
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
                var savedCount = doc?["savedCount"]?.GetValue<int>() ?? 0;
                var failedCount = doc?["failedCount"]?.GetValue<int>() ?? 0;
                return new IngestResult
                {
                    Success = true,
                    SavedCount = savedCount,
                    FailedCount = failedCount
                };
            }

            _logger.Warning("Ingest HTTP {StatusCode}: {Content}", response.StatusCode, response.Content);
            return new IngestResult
            {
                Success = false,
                FailedCount = request.Batches.Sum(b => b.Metrics.Count),
                ErrorMessage = response.ErrorMessage ?? response.Content ?? $"HTTP {(int)response.StatusCode}"
            };
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Ingest gönderimi başarısız");
            return new IngestResult
            {
                Success = false,
                FailedCount = request.Batches.Sum(b => b.Metrics.Count),
                ErrorMessage = ex.Message
            };
        }
    }

    private string? GetReactorBaseUrl() => _configProvider.GetConfig()?.ServerUrl;
}
