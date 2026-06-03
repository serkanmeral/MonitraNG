using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngReactor.Application.Abstractions.Data;
using MngReactor.Application.Configuration;
using MngReactor.Persistence.Services.Domain;
using Moq;
using Xunit;

namespace MngReactor.Tests.Services.Domain;

public class DomainDefaultsProcessingTests
{
    [Fact]
    public async Task CreateDefaultsAsync_NewDomain_InsertsSchedulesAndPeriods()
    {
        var mockDg = new Mock<IDataGatewayClient>();
        mockDg.Setup(d => d.GetListAsync("mon_schedules", "name:eq:Sürekli", It.IsAny<string?>(), 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new JsonArray());
        mockDg.Setup(d => d.GetListAsync("mon_collection_periods", "name:eq:1 dakika", It.IsAny<string?>(), 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new JsonArray());

        var createResponse = new JsonObject { ["success"] = true };
        mockDg.Setup(d => d.CreateAsync("mon_schedules", It.IsAny<JsonObject>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(createResponse);
        mockDg.Setup(d => d.CreateAsync("mon_collection_periods", It.IsAny<JsonObject>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(createResponse);

        var options = Options.Create(new MngReactorSettings
        {
            DataGateway = new DataGatewaySettings { DomainTokens = new Dictionary<string, string> { ["testdomain"] = "token123" } }
        });
        var logger = new Mock<ILogger<DomainDefaultsProcessing>>().Object;
        var sut = new DomainDefaultsProcessing(logger, mockDg.Object, options);

        var result = await sut.CreateDefaultsAsync("testdomain", "token123");

        Assert.True(result);
        mockDg.Verify(d => d.CreateAsync("mon_schedules", It.IsAny<JsonObject>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        mockDg.Verify(d => d.CreateAsync("mon_collection_periods", It.IsAny<JsonObject>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateDefaultsAsync_Idempotent_WhenAlreadyExists()
    {
        var existingSchedule = new JsonArray { new JsonObject { ["name"] = "Sürekli" } };
        var existingPeriod = new JsonArray { new JsonObject { ["name"] = "1 dakika" } };

        var mockDg = new Mock<IDataGatewayClient>();
        mockDg.Setup(d => d.GetListAsync("mon_schedules", "name:eq:Sürekli", It.IsAny<string?>(), 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingSchedule);
        mockDg.Setup(d => d.GetListAsync("mon_collection_periods", "name:eq:1 dakika", It.IsAny<string?>(), 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingPeriod);

        var options = Options.Create(new MngReactorSettings
        {
            DataGateway = new DataGatewaySettings { DomainTokens = new Dictionary<string, string> { ["testdomain"] = "token123" } }
        });
        var logger = new Mock<ILogger<DomainDefaultsProcessing>>().Object;
        var sut = new DomainDefaultsProcessing(logger, mockDg.Object, options);

        var result = await sut.CreateDefaultsAsync("testdomain", "token123");

        Assert.True(result);
        mockDg.Verify(d => d.CreateAsync(It.IsAny<string>(), It.IsAny<JsonObject>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateDefaultsAsync_NoToken_ReturnsFalse()
    {
        var mockDg = new Mock<IDataGatewayClient>();
        var options = Options.Create(new MngReactorSettings { DataGateway = new DataGatewaySettings() });
        var logger = new Mock<ILogger<DomainDefaultsProcessing>>().Object;
        var sut = new DomainDefaultsProcessing(logger, mockDg.Object, options);

        var result = await sut.CreateDefaultsAsync("testdomain", accessToken: null);

        Assert.False(result);
        mockDg.Verify(d => d.GetListAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
