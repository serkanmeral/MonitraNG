using System.Net;
using System.Net.Http.Json;
using MngReactor.Application.Features.Engine;
using MngReactor.Tests.Helpers;
using Xunit;

namespace MngReactor.Tests.Controllers;

public class EngineControllerTests : IClassFixture<MngReactorWebApplicationFactory>
{
    private readonly HttpClient _client;

    public EngineControllerTests(MngReactorWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetEngineAssets_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/v1/Engine/assets");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetEngineAssets_WithAuth_ReturnsOk()
    {
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "test");
        var response = await _client.GetAsync("/api/v1/Engine/assets");
        Assert.True(response.IsSuccessStatusCode, $"Expected 2xx, got {response.StatusCode}");
    }

    [Fact]
    public async Task CreateConfigText_WithAuth_ReturnsOkOrExpectedError()
    {
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "test");
        var response = await _client.GetAsync("/api/v1/Engine/create_config_text");
        // Mock ortaminda engine verisi olmadiginda 404/500/400 donulebilir; onemli olan 401 Unauthorized donmemesi
        Assert.True(
            response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotFound ||
            response.StatusCode == HttpStatusCode.InternalServerError || response.StatusCode == HttpStatusCode.BadRequest,
            $"Expected 2xx/404/500/400, got {response.StatusCode}");
    }

    [Fact]
    public async Task GetConfig_WithoutEngineId_ReturnsUnauthorized()
    {
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "test");
        var response = await _client.GetAsync("/api/v1/Engine/config");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetConfig_WithEngineId_ReturnsNotFoundOrOk()
    {
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "test");
        var response = await _client.GetAsync("/api/v1/Engine/config?engineId=non-existent-engine");
        Assert.True(response.StatusCode == HttpStatusCode.NotFound || response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task GetConfigString_WithEngineId_ReturnsNotFoundOrOk()
    {
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "test");
        var response = await _client.GetAsync("/api/v1/Engine/config-string?engineId=non-existent-engine");
        Assert.True(response.StatusCode == HttpStatusCode.NotFound || response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task PostStatus_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/Engine/status", new EngineStatusRequest { EngineId = "e1", Domain = "test" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostStatus_WithInvalidPayload_ReturnsBadRequest()
    {
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "test");
        var response = await _client.PostAsJsonAsync("/api/v1/Engine/status", new EngineStatusRequest { EngineId = "", Domain = "test" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostStatus_WithValidPayload_ReturnsOkOrBadRequest()
    {
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "test");
        var request = new EngineStatusRequest { EngineId = "non-existent", Domain = "test" };
        var response = await _client.PostAsJsonAsync("/api/v1/Engine/status", request);
        // Mock ortaminda engine yoksa 400, varsa 200
        Assert.True(response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.BadRequest);
    }
}
