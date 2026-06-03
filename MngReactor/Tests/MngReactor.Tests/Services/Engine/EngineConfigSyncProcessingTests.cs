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

public class EngineConfigSyncProcessingTests
{
    private static IOptions<MngReactorSettings> CreateOptions()
    {
        return Options.Create(new MngReactorSettings());
    }

    [Fact]
    public async Task GetConfigAsync_ValidEngine_ReturnsResult()
    {
        var engineId = "engine-1";
        var engine = new JsonObject { ["__dataId"] = engineId };
        var agents = new JsonArray
        {
            new JsonObject
            {
                ["__dataId"] = "agent-1",
                ["name"] = "Test Agent",
                ["status"] = "active",
                ["asset_configs"] = new JsonArray
                {
                    new JsonObject { ["assetId"] = "asset-1", ["active"] = true }
                }
            }
        };
        var periods = new JsonArray { new JsonObject { ["__dataId"] = "period-1", ["name"] = "1 dakika", ["expression"] = "*/1 * * * *" } };
        var schedules = new JsonArray { new JsonObject { ["__dataId"] = "sched-1", ["name"] = "Sürekli" } };
        var asset = new JsonObject
        {
            ["__dataId"] = "asset-1",
            ["connection_info"] = new JsonObject { ["endpoint"] = new JsonObject { ["host"] = "1.2.3.4" } },
            ["type"] = new JsonObject { ["collection_method"] = "ssh" }
        };

        var mockDg = new Mock<IDataGatewayClient>();
        mockDg.Setup(d => d.GetByIdAsync("mon_engines", engineId, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(engine);
        mockDg.Setup(d => d.GetListAsync("mon_agents", It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(agents);
        mockDg.Setup(d => d.GetListAsync("mon_collection_periods", It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(periods);
        mockDg.Setup(d => d.GetListAsync("mon_schedules", It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(schedules);
        mockDg.Setup(d => d.GetByIdAsync("mon_assets", "asset-1", It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(asset);

        var mockCrypt = new Mock<ICryptProcessing>();
        mockCrypt.Setup(c => c.Decrypt(It.IsAny<string>())).ReturnsAsync("decrypted");

        var logger = new Mock<ILogger<EngineConfigSyncProcessing>>().Object;
        var sut = new EngineConfigSyncProcessing(logger, mockDg.Object, mockCrypt.Object, CreateOptions());

        var result = await sut.GetConfigAsync(engineId, "testdomain", "token");

        Assert.NotNull(result);
        Assert.Equal(engineId, result.EngineId);
        Assert.Equal("testdomain", result.Domain);
        Assert.Single(result.Agents);
        Assert.Equal("agent-1", result.Agents[0].AgentId);
        Assert.Single(result.AssetConfigs);
        Assert.Equal("asset-1", result.AssetConfigs[0].AssetId);
    }

    [Fact]
    public async Task GetConfigAsync_UnknownEngine_ReturnsNull()
    {
        var mockDg = new Mock<IDataGatewayClient>();
        mockDg.Setup(d => d.GetByIdAsync("mon_engines", It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(null as JsonObject);

        var logger = new Mock<ILogger<EngineConfigSyncProcessing>>().Object;
        var mockCrypt = new Mock<ICryptProcessing>().Object;
        var sut = new EngineConfigSyncProcessing(logger, mockDg.Object, mockCrypt, CreateOptions());

        var result = await sut.GetConfigAsync("unknown-engine", "testdomain", "token");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetConfigAsync_NoToken_ReturnsNullWhenNoDomainToken()
    {
        var mockDg = new Mock<IDataGatewayClient>();
        var options = Options.Create(new MngReactorSettings { DataGateway = new DataGatewaySettings() });
        var logger = new Mock<ILogger<EngineConfigSyncProcessing>>().Object;
        var mockCrypt = new Mock<ICryptProcessing>().Object;
        var sut = new EngineConfigSyncProcessing(logger, mockDg.Object, mockCrypt, options);

        var result = await sut.GetConfigAsync("engine-1", "testdomain", accessToken: null);

        Assert.Null(result);
        mockDg.Verify(d => d.GetByIdAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
