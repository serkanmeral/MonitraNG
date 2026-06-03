using System.Net;
using MngReactor.Tests.Helpers;
using Xunit;

namespace MngReactor.Tests.Controllers;

public class HealthControllerTests : IClassFixture<MngReactorWebApplicationFactory>
{
    private readonly HttpClient _client;

    public HealthControllerTests(MngReactorWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetHealth_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/v1/health");
        Assert.True(response.IsSuccessStatusCode, $"Expected 2xx, got {response.StatusCode}");
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("status", json);
    }

    [Fact]
    public async Task GetLive_ReturnsAlive()
    {
        var response = await _client.GetAsync("/api/v1/health/live");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("alive", json);
    }

    [Fact]
    public async Task GetReady_ReturnsReady()
    {
        var response = await _client.GetAsync("/api/v1/health/ready");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("ready", json);
    }
}
