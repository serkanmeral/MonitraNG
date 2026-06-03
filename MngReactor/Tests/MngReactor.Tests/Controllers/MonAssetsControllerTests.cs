using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using MngReactor.Tests.Helpers;
using Xunit;

namespace MngReactor.Tests.Controllers;

public class MonAssetsControllerTests : IClassFixture<MngReactorWebApplicationFactory>
{
    private readonly HttpClient _client;

    public MonAssetsControllerTests(MngReactorWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Get_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/v1/monitoring/assets");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_WithAuth_ReturnsOk()
    {
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "test");
        var response = await _client.GetAsync("/api/v1/monitoring/assets");
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Insert_WithAuth_ReturnsOk()
    {
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "test");

        var data = new JsonObject
        {
            ["name"] = "Test Asset",
            ["assetTypeId"] = "type-1"
        };

        var response = await _client.PostAsJsonAsync("/api/v1/monitoring/assets", data);
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Update_WithAuth_ReturnsOk()
    {
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "test");

        var data = new JsonObject
        {
            ["__dataId"] = "some-asset-id",
            ["name"] = "Updated Asset"
        };

        var response = await _client.PutAsJsonAsync("/api/v1/monitoring/assets", data);
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Delete_WithAuth_ReturnsOk()
    {
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "test");

        var data = new JsonObject { ["__dataId"] = "some-asset-id" };

        var response = await _client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/v1/monitoring/assets")
        {
            Content = JsonContent.Create(data)
        });

        Assert.True(response.IsSuccessStatusCode);
    }
}
