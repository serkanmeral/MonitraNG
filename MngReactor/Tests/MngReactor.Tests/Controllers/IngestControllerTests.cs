using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MngReactor.Application.Features.Commands.Ingest;
using MngReactor.Tests.Helpers;
using Xunit;

namespace MngReactor.Tests.Controllers;

public class IngestControllerTests : IClassFixture<MngReactorWebApplicationFactory>
{
    private readonly HttpClient _client;

    public IngestControllerTests(MngReactorWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task IngestMetrics_WithoutAuth_ReturnsUnauthorized()
    {
        var request = new IngestMetricsRequest { Batches = [] };
        var response = await _client.PostAsJsonAsync("/api/v1/Ingest/metrics", request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task IngestMetrics_EmptyBatches_ReturnsBadRequest()
    {
        var token = TestTokenHelper.CreateBearerToken();
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var request = new IngestMetricsRequest { Batches = [] };
        var response = await _client.PostAsJsonAsync("/api/v1/Ingest/metrics", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task IngestMetrics_ValidRequest_ReturnsOk()
    {
        var token = TestTokenHelper.CreateBearerToken();
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var request = new IngestMetricsRequest
        {
            Batches =
            [
                new IngestBatch
                {
                    AssetId = "asset-1",
                    ItemId = "item-1",
                    AgentId = "agent-1",
                    EngineId = "engine-1",
                    CollectedAt = DateTime.UtcNow,
                    Metrics = [new IngestMetric { CollectibleCode = "cpu", Value = 42.5, Unit = "%" }]
                }
            ]
        };

        var response = await _client.PostAsJsonAsync("/api/v1/Ingest/metrics", request);

        Assert.True(response.IsSuccessStatusCode, $"Expected 2xx, got {response.StatusCode}");
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.True(root.TryGetProperty("savedCount", out _));
        Assert.True(root.TryGetProperty("failedCount", out _));
    }
}
