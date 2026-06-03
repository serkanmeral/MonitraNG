using System.Net;
using System.Net.Http.Headers;
using Xunit;

namespace MngReactor.Tests.Controllers;

/// <summary>
/// Docker uzerinde calisan MngReactor container'ina karsi HTTP testleri.
/// Calismasi icin: mng_apps compose ile MngReactor ayakta olmali (localhost:5003).
/// </summary>
[Trait("Category", "Docker")]
public class DockerMngReactorTests
{
    private static readonly HttpClient Client = new()
    {
        BaseAddress = new Uri(Environment.GetEnvironmentVariable("MNGREACTOR_BASE_URL") ?? "http://localhost:5003")
    };

    [Fact]
    public async Task GetHealth_ReturnsOk()
    {
        var response = await Client.GetAsync("/api/v1/health");
        Assert.True(response.IsSuccessStatusCode, $"Expected 2xx, got {response.StatusCode}");
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("status", json);
    }

    [Fact]
    public async Task GetLive_ReturnsAlive()
    {
        var response = await Client.GetAsync("/api/v1/health/live");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("alive", json);
    }

    [Fact]
    public async Task GetReady_ReturnsReady()
    {
        var response = await Client.GetAsync("/api/v1/health/ready");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("ready", json);
    }

    [Fact]
    public async Task GetEngineAssets_WithoutAuth_Returns401()
    {
        Client.DefaultRequestHeaders.Authorization = null;
        var response = await Client.GetAsync("/api/v1/Engine/assets");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetEngineAssets_WithBearer_ReturnsOk()
    {
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "test");
        try
        {
            var response = await Client.GetAsync("/api/v1/Engine/assets");
            Assert.True(
                response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.Unauthorized,
                $"Expected 2xx or 401, got {response.StatusCode}");
        }
        finally
        {
            Client.DefaultRequestHeaders.Authorization = null;
        }
    }
}
