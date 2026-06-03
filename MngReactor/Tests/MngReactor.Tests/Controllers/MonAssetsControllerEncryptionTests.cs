using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using MngReactor.Tests.Helpers;
using Xunit;

namespace MngReactor.Tests.Controllers;

/// <summary>
/// MonAssetsController connection_info icindeki hassas alanlarin (password, privateKey, community) sifrelendigini dogrular.
/// </summary>
public class MonAssetsControllerEncryptionTests : IClassFixture<MngReactorEncryptionTestFactory>
{
    private readonly HttpClient _client;
    private readonly MngReactorEncryptionTestFactory _factory;

    public MonAssetsControllerEncryptionTests(MngReactorEncryptionTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "test");
    }

    [Fact]
    public async Task Insert_WithConnectionInfoPassword_EncryptsBeforeSendingToDg()
    {
        var capture = _factory.CapturingClient;
        capture.LastCreatePayload = null;

        var data = new JsonObject
        {
            ["name"] = "Test Asset",
            ["assetTypeId"] = "type-1",
            ["connection_info"] = new JsonObject
            {
                ["host"] = "localhost",
                ["password"] = "plaintext-secret"
            }
        };

        var response = await _client.PostAsJsonAsync("/api/v1/monitoring/assets", data);

        Assert.True(response.IsSuccessStatusCode);
        Assert.NotNull(capture.LastCreatePayload);

        var connInfo = capture.LastCreatePayload["connection_info"] as JsonObject;
        Assert.NotNull(connInfo);

        var password = connInfo["password"]?.GetValue<string>();
        Assert.NotNull(password);
        Assert.StartsWith(MockCryptProcessing.EncryptedPrefix, password);
        Assert.Equal(MockCryptProcessing.EncryptedPrefix + "plaintext-secret", password);
    }

    [Fact]
    public async Task Update_WithConnectionInfoPrivateKey_EncryptsBeforeSendingToDg()
    {
        var capture = _factory.CapturingClient;
        capture.Reset();

        var data = new JsonObject
        {
            ["__dataId"] = "asset-123",
            ["name"] = "Updated Asset",
            ["connection_info"] = new JsonObject
            {
                ["privateKey"] = "my-private-key-data"
            }
        };

        var response = await _client.PutAsJsonAsync("/api/v1/monitoring/assets", data);

        Assert.True(response.IsSuccessStatusCode);
        Assert.NotNull(capture.LastUpdatePayload);

        var connInfo = capture.LastUpdatePayload["connection_info"] as JsonObject;
        Assert.NotNull(connInfo);

        var privateKey = connInfo["privateKey"]?.GetValue<string>();
        Assert.NotNull(privateKey);
        Assert.StartsWith(MockCryptProcessing.EncryptedPrefix, privateKey);
        Assert.Equal(MockCryptProcessing.EncryptedPrefix + "my-private-key-data", privateKey);
    }

    [Fact]
    public async Task Insert_WithConnectionInfoAuthPassword_EncryptsNestedAuth()
    {
        var capture = _factory.CapturingClient;
        capture.Reset();

        var data = new JsonObject
        {
            ["name"] = "Test Asset",
            ["connection_info"] = new JsonObject
            {
                ["auth"] = new JsonObject
                {
                    ["password"] = "nested-password"
                }
            }
        };

        var response = await _client.PostAsJsonAsync("/api/v1/monitoring/assets", data);

        Assert.True(response.IsSuccessStatusCode);
        Assert.NotNull(capture.LastCreatePayload);

        var connInfo = capture.LastCreatePayload["connection_info"] as JsonObject;
        var auth = connInfo?["auth"] as JsonObject;
        Assert.NotNull(auth);

        var password = auth["password"]?.GetValue<string>();
        Assert.NotNull(password);
        Assert.Equal(MockCryptProcessing.EncryptedPrefix + "nested-password", password);
    }
}
