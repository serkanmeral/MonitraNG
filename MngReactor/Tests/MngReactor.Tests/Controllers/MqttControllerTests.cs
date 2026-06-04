using System.Net;
using System.Net.Http.Json;
using MngReactor.Application.Features.Mqtt;
using MngReactor.Tests.Helpers;
using Xunit;

namespace MngReactor.Tests.Controllers;

public sealed class MqttControllerTests : IClassFixture<MngReactorWebApplicationFactory>
{
    private readonly HttpClient _client;

    public MqttControllerTests(MngReactorWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Publish_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/mqtt/publish",
            new MqttPublishRequest { Topic = "test/topic", Message = "{}" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Publish_EmptyTopic_ReturnsBadRequest()
    {
        var token = TestTokenHelper.CreateBearerToken();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await _client.PostAsJsonAsync("/api/v1/mqtt/publish",
            new MqttPublishRequest { Topic = "  ", Message = "{\"command\":\"block_ip\"}" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
