using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using MngReactor.Tests.Helpers;
using Xunit;

namespace MngReactor.Tests.Controllers;

public class MonAgentsControllerTests : IClassFixture<MngReactorWebApplicationFactory>
{
    private readonly HttpClient _client;

    public MonAgentsControllerTests(MngReactorWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Get_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/v1/monitoring/agents");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_WithAuth_ReturnsOk()
    {
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "test");
        var response = await _client.GetAsync("/api/v1/monitoring/agents");
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Insert_WithAuth_ReturnsOk()
    {
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "test");

        var data = new JsonObject
        {
            ["name"] = "Test Agent",
            ["engineId"] = "engine-1"
        };

        var response = await _client.PostAsJsonAsync("/api/v1/monitoring/agents", data);
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Update_WithAuth_ReturnsOk()
    {
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "test");

        var data = new JsonObject
        {
            ["__dataId"] = "some-agent-id",
            ["name"] = "Updated Agent",
            ["engineId"] = "engine-1"
        };

        var response = await _client.PutAsJsonAsync("/api/v1/monitoring/agents", data);
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Delete_WithAuth_ReturnsOk()
    {
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "test");

        var data = new JsonObject { ["__dataId"] = "some-agent-id" };

        var response = await _client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/v1/monitoring/agents")
        {
            Content = JsonContent.Create(data)
        });

        Assert.True(response.IsSuccessStatusCode);
    }
}
