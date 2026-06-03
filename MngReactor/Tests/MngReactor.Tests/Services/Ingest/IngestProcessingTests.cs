using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngReactor.Application.Abstractions.Data;
using MngReactor.Application.Abstractions.Ingest;
using MngReactor.Application.Configuration;
using MngReactor.Application.Features.Commands.Ingest;
using MngReactor.Persistence.Services.Ingest;
using Moq;
using Xunit;

namespace MngReactor.Tests.Services.Ingest;

public class IngestProcessingTests
{
    private static IngestMetricsRequest CreateValidRequest()
    {
        return new IngestMetricsRequest
        {
            Batches =
            [
                new IngestBatch
                {
                    AssetId = "asset-1",
                    ItemId = "item-1",
                    AgentId = "agent-1",
                    EngineId = "engine-1",
                    CollectedAt = DateTime.UtcNow,
                    Metrics =
                    [
                        new IngestMetric { CollectibleCode = "cpu", Value = 42.5, Unit = "%" }
                    ]
                }
            ]
        };
    }

    [Fact]
    public async Task ProcessAsync_ValidBatch_SavedCountPositive()
    {
        var mockMetricsRepo = new Mock<IMonMetricsRepository>();
        mockMetricsRepo.Setup(r => r.InsertManyAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<JsonObject>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, IReadOnlyList<JsonObject> docs, CancellationToken _) => docs.Count);

        var mockDg = new Mock<IDataGatewayClient>();
        mockDg.Setup(d => d.UpdateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<JsonObject>(), It.IsAny<string?>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()))
            .ReturnsAsync(true);

        var mockPublisher = new Mock<IMetricPublisher>();
        mockPublisher.Setup(p => p.PublishAsync(It.IsAny<object>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var options = Options.Create(new MngReactorSettings
        {
            DataGateway = new DataGatewaySettings { DomainTokens = new Dictionary<string, string> { ["testdomain"] = "token123" } }
        });
        var mockIngestNotify = new Mock<IIngestNotifyPublisher>();
        mockIngestNotify.Setup(x => x.TryPublishDataUpdatedAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var logger = new Mock<ILogger<IngestProcessing>>().Object;
        var sut = new IngestProcessing(logger, options, mockPublisher.Object, mockIngestNotify.Object, mockMetricsRepo.Object, mockDg.Object);

        var result = await sut.ProcessAsync(CreateValidRequest(), "testdomain", "token123");

        Assert.True(result.SavedCount > 0);
        Assert.Equal(0, result.FailedCount);
    }

    [Fact]
    public async Task ProcessAsync_EmptyBatches_ReturnsZeroSaved()
    {
        var mockMetricsRepo = new Mock<IMonMetricsRepository>();
        var mockDg = new Mock<IDataGatewayClient>();
        var mockPublisher = new Mock<IMetricPublisher>();
        var mockIngestNotify = new Mock<IIngestNotifyPublisher>();
        mockIngestNotify.Setup(x => x.TryPublishDataUpdatedAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var options = Options.Create(new MngReactorSettings
        {
            DataGateway = new DataGatewaySettings { DomainTokens = new Dictionary<string, string> { ["testdomain"] = "token123" } }
        });
        var logger = new Mock<ILogger<IngestProcessing>>().Object;
        var sut = new IngestProcessing(logger, options, mockPublisher.Object, mockIngestNotify.Object, mockMetricsRepo.Object, mockDg.Object);

        var request = new IngestMetricsRequest { Batches = [] };
        var result = await sut.ProcessAsync(request, "testdomain", "token123");

        Assert.Equal(0, result.SavedCount);
        mockMetricsRepo.Verify(r => r.InsertManyAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<JsonObject>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_NoToken_ReturnsAuthError()
    {
        var mockMetricsRepo = new Mock<IMonMetricsRepository>();
        var mockDg = new Mock<IDataGatewayClient>();
        var mockPublisher = new Mock<IMetricPublisher>();
        var mockIngestNotify = new Mock<IIngestNotifyPublisher>();
        var options = Options.Create(new MngReactorSettings { DataGateway = new DataGatewaySettings() });
        var logger = new Mock<ILogger<IngestProcessing>>().Object;
        var sut = new IngestProcessing(logger, options, mockPublisher.Object, mockIngestNotify.Object, mockMetricsRepo.Object, mockDg.Object);

        var result = await sut.ProcessAsync(CreateValidRequest(), "testdomain", accessToken: null);

        Assert.Equal(0, result.SavedCount);
        Assert.True(result.FailedCount > 0);
        Assert.Contains(result.ErrorList, e => e.Code == "auth_error");
        mockMetricsRepo.Verify(r => r.InsertManyAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<JsonObject>>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
