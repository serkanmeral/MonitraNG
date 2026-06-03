using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using MngReactor.Application.Abstractions.Data;
using MngReactor.Application.Configuration;
using MngReactor.Persistence.Services.Engine;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace MngReactor.Tests.Services.Engine;

public class EngineIdsForAssetResolverTests
{
    [Fact]
    public async Task GetEngineIdsForAssetAsync_EmptyResult_ReturnsEmptyList()
    {
        var mockDg = new Mock<IDataGatewayClient>();
        mockDg.Setup(d => d.AggregateAsync(It.IsAny<string>(), It.IsAny<JsonArray>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new JsonArray());

        var options = Options.Create(new MngReactorSettings());
        var sut = new EngineIdsForAssetResolver(mockDg.Object, options);
        var result = await sut.GetEngineIdsForAssetAsync("testdomain", "asset-1", "token");

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetEngineIdsForAssetAsync_WithAgents_ReturnsDistinctEngineIds()
    {
        var mockDg = new Mock<IDataGatewayClient>();
        var arr = new JsonArray
        {
            new JsonObject { ["engineId"] = "engine-1" },
            new JsonObject { ["engineId"] = "engine-2" },
            new JsonObject { ["engineId"] = "engine-1" }
        };
        mockDg.Setup(d => d.AggregateAsync(It.IsAny<string>(), It.IsAny<JsonArray>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(arr);

        var options = Options.Create(new MngReactorSettings());
        var sut = new EngineIdsForAssetResolver(mockDg.Object, options);
        var result = await sut.GetEngineIdsForAssetAsync("testdomain", "asset-1", "token");

        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Contains("engine-1", result);
        Assert.Contains("engine-2", result);
    }

    [Fact]
    public async Task GetEngineIdsForAssetAsync_NoToken_ReturnsEmptyWhenNoDomainToken()
    {
        var mockDg = new Mock<IDataGatewayClient>();
        var options = Options.Create(new MngReactorSettings { DataGateway = new DataGatewaySettings() });
        var sut = new EngineIdsForAssetResolver(mockDg.Object, options);
        var result = await sut.GetEngineIdsForAssetAsync("testdomain", "asset-1", accessToken: null);

        Assert.NotNull(result);
        Assert.Empty(result);
        mockDg.Verify(d => d.AggregateAsync(It.IsAny<string>(), It.IsAny<JsonArray>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
