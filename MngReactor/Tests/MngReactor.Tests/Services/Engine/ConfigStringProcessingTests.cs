using System;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngReactor.Application.Abstractions.Crypt;
using MngReactor.Application.Abstractions.Data;
using MngReactor.Application.Configuration;
using MngReactor.Persistence.Services.Engine;
using Moq;
using Xunit;

namespace MngReactor.Tests.Services.Engine;

public class ConfigStringProcessingTests
{
    private static IOptions<MngReactorSettings> CreateOptions()
    {
        return Options.Create(new MngReactorSettings
        {
            Server = new ServerSettings { Port = 15010 },
            OpenApiServerPath = "http://localhost:15010",
            Actors = new ActorsSettings { MngKeeper = "http://localhost:5001" },
            Mqtt = new MqttSettings { Host = "localhost", Port = 1883 }
        });
    }

    [Fact]
    public async Task CreateConfigStringAsync_ValidEngine_ReturnsBase64()
    {
        var engineId = "engine-1";
        var engine = new JsonObject
        {
            ["__dataId"] = engineId,
            ["username"] = "eng_user",
            ["password"] = Convert.ToBase64String(new byte[] { 1, 2, 3 }), // compressed format
            ["sendSchedule"] = "0 */2 * * *",
            ["configSyncPeriodMinutes"] = 10
        };

        var mockDg = new Mock<IDataGatewayClient>();
        mockDg.Setup(d => d.GetByIdAsync("mon_engines", engineId, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(engine);

        var mockCrypt = new Mock<ICryptProcessing>();
        mockCrypt.Setup(c => c.DeCompress(It.IsAny<byte[]>())).ReturnsAsync("plainpass");
        mockCrypt.Setup(c => c.Compress(It.IsAny<string>())).ReturnsAsync(new byte[] { 65, 66, 67 });
        mockCrypt.Setup(c => c.Encrypt(It.IsAny<string>())).ReturnsAsync("RSA-encrypted-base64");

        var options = CreateOptions();
        var optsWithCrypt = Options.Create(new MngReactorSettings
        {
            Server = options.Value.Server,
            OpenApiServerPath = options.Value.OpenApiServerPath,
            Actors = options.Value.Actors,
            Mqtt = options.Value.Mqtt,
            Crypt = new CryptSettings
            {
                IngestEncryptKey = "pbk16bytes!!!!!!",
                IngestDecryptKey = "prk16bytes!!!!!!"
            }
        });
        var logger = new Mock<ILogger<ConfigStringProcessing>>().Object;
        var sut = new ConfigStringProcessing(logger, mockDg.Object, mockCrypt.Object, optsWithCrypt);

        var result = await sut.CreateConfigStringAsync(engineId, "testdomain", "token");

        Assert.NotNull(result);
        Assert.NotEmpty(result);
        // URL-safe Base64: + ve / yerine - ve _ kullanılır
        Assert.Matches("^[A-Za-z0-9_-]+=*$", result);
        // Decode edilebilmeli ve decode edilen JSON'da backslash olmamalı (Engine'da Base64 hatası olmaz)
        var standardB64 = result.Replace('-', '+').Replace('_', '/');
        var bytes = Convert.FromBase64String(standardB64);
        var decodedJson = Encoding.UTF8.GetString(bytes);
        Assert.DoesNotContain("\\", decodedJson);
        var obj = JsonSerializer.Deserialize<JsonObject>(decodedJson);
        Assert.NotNull(obj);
        Assert.True(obj.ContainsKey("CompressPbk") && obj.ContainsKey("CompressPrk") && obj.ContainsKey("EngineInfo"));
        mockCrypt.Verify(c => c.Encrypt(It.IsAny<string>()), Times.Exactly(2));
    }

    [Fact]
    public async Task CreateConfigStringAsync_UnknownEngine_ReturnsNull()
    {
        var mockDg = new Mock<IDataGatewayClient>();
        mockDg.Setup(d => d.GetByIdAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((JsonObject?)null);

        var logger = new Mock<ILogger<ConfigStringProcessing>>().Object;
        var mockCrypt = new Mock<ICryptProcessing>().Object;
        var sut = new ConfigStringProcessing(logger, mockDg.Object, mockCrypt, CreateOptions());

        var result = await sut.CreateConfigStringAsync("unknown-engine", "testdomain", "token");

        Assert.Null(result);
    }

    [Fact]
    public async Task CreateConfigStringAsync_NoToken_ReturnsNullWhenNoDomainToken()
    {
        var mockDg = new Mock<IDataGatewayClient>();
        var options = Options.Create(new MngReactorSettings { DataGateway = new DataGatewaySettings() });
        var logger = new Mock<ILogger<ConfigStringProcessing>>().Object;
        var mockCrypt = new Mock<ICryptProcessing>().Object;
        var sut = new ConfigStringProcessing(logger, mockDg.Object, mockCrypt, options);

        var result = await sut.CreateConfigStringAsync("engine-1", "testdomain", accessToken: null);

        Assert.Null(result);
        mockDg.Verify(d => d.GetByIdAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
