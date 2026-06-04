using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using MngReactor.Application.Features.Commands.Ingest;
using MngReactor.Application.Models.SecEvents;
using MngReactor.Tests.Helpers;
using MngReactor.Tests.Services.SecEvents;
using Xunit;

namespace MngReactor.Tests.Controllers;

public sealed class SecEventsIngestControllerTests : IClassFixture<MngReactorWebApplicationFactory>
{
    private const string Endpoint = "/api/v1/Ingest/sec-events";
    private readonly HttpClient _client;

    public SecEventsIngestControllerTests(MngReactorWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task IngestSecEvents_WithoutAuth_ReturnsUnauthorized()
    {
        var request = new SecEventIngestRequest { Items = [] };
        var response = await _client.PostAsJsonAsync(Endpoint, request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task IngestSecEvents_EmptyItems_ReturnsBadRequest()
    {
        Authorize();

        var request = new SecEventIngestRequest { Items = [] };
        var response = await _client.PostAsJsonAsync(Endpoint, request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task IngestSecEvents_OversizedBatch_ReturnsBadRequest()
    {
        Authorize();

        var request = new SecEventIngestRequest
        {
            Items = Enumerable.Range(0, SecEventIngestLimits.MaxItemsPerRequest + 1)
                .Select(_ => SampleItem())
                .ToList()
        };
        var response = await _client.PostAsJsonAsync(Endpoint, request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task IngestSecEvents_ValidFirewallFixture_ReturnsOkWithCounts()
    {
        Authorize();

        var request = new SecEventIngestRequest { Items = [SampleItem()] };
        var response = await _client.PostAsJsonAsync(Endpoint, request);

        Assert.True(response.IsSuccessStatusCode, $"Expected 2xx, got {response.StatusCode}");
        var body = await response.Content.ReadFromJsonAsync<SecEventIngestResponse>();
        Assert.NotNull(body);
        Assert.Equal(1, body.Accepted);
        Assert.Equal(0, body.Rejected);
        Assert.Equal(1, body.Published);
        Assert.False(body.ImplementationPending);
    }

    [Fact]
    public async Task IngestSecEvents_EncryptedPayload_DecryptsAndProcesses()
    {
        Authorize();

        await using var factory = new MngReactorEncryptionTestFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = _client.DefaultRequestHeaders.Authorization;

        var json = JsonSerializer.Serialize(new SecEventIngestRequest { Items = [SampleItem()] });
        var encrypted = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, Endpoint)
        {
            Content = new StringContent(encrypted, Encoding.UTF8, "text/plain")
        };
        httpRequest.Headers.Add("X-Payload-Format", "encrypted");

        var response = await client.SendAsync(httpRequest);

        Assert.True(response.IsSuccessStatusCode, $"Expected 2xx, got {response.StatusCode}");
        var body = await response.Content.ReadFromJsonAsync<SecEventIngestResponse>();
        Assert.NotNull(body);
        Assert.Equal(1, body.Accepted);
    }

    private void Authorize()
    {
        var token = TestTokenHelper.CreateBearerToken();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }

    private static SecEventIngestItem SampleItem() =>
        new()
        {
            ReceivedAt = DateTime.Parse("2026-06-03T14:00:01Z").ToUniversalTime(),
            Source = new SecEventIngestSource
            {
                Type = "firewall",
                Product = "generic-syslog",
                Host = "fw01"
            },
            Raw = JsonSerializer.SerializeToElement(SiemFixtureHelper.ReadFixture("firewall_deny.syslog.txt"))
        };
}
