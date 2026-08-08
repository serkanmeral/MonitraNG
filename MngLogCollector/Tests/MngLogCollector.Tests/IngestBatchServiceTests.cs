using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MngLogCollector.Application.Abstractions.Observations;
using MngLogCollector.Application.Abstractions.OpenSearch;
using MngLogCollector.Application.Configuration;
using MngLogCollector.Application.Contracts.Ingest;
using MngLogCollector.Application.Services.Ingest;
using MngLogCollector.Persistence.OpenSearch;

namespace MngLogCollector.Tests;

public class IngestBatchServiceTests
{
    [Fact]
    public async Task IngestAsync_requires_domain_and_host()
    {
        var svc = CreateService(writeEnabled: false, obsEnabled: false, writer: new FakeWriter(), observations: new FakeObservations());
        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.IngestAsync(new IngestBatchRequest { Domain = "", HostId = "h1" }));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.IngestAsync(new IngestBatchRequest { Domain = "odak", HostId = "" }));
    }

    [Fact]
    public async Task IngestAsync_accepts_without_write_when_disabled()
    {
        var writer = new FakeWriter();
        var svc = CreateService(writeEnabled: false, obsEnabled: false, writer, new FakeObservations());
        var result = await svc.IngestAsync(new IngestBatchRequest
        {
            Domain = "odak",
            HostId = "host-1",
            Events =
            [
                new IngestEventItem { Source = "windows-eventlog", Message = "test" }
            ]
        });

        Assert.Equal(1, result.Accepted);
        Assert.Equal(0, result.Written);
        Assert.False(result.OpenSearchWriteEnabled);
        Assert.Empty(writer.Items);
    }

    [Fact]
    public async Task IngestAsync_writes_when_enabled()
    {
        var writer = new FakeWriter();
        var svc = CreateService(writeEnabled: true, obsEnabled: false, writer, new FakeObservations());
        var result = await svc.IngestAsync(new IngestBatchRequest
        {
            Domain = "odak",
            HostId = "host-1",
            Hostname = "WKS01",
            Events =
            [
                new IngestEventItem
                {
                    Id = "evt-1",
                    Source = "windows-eventlog",
                    Message = "logon",
                    Raw = JsonDocument.Parse("{\"EventID\":4624}").RootElement
                }
            ]
        });

        Assert.Equal(1, result.Accepted);
        Assert.Equal(1, result.Written);
        Assert.Single(writer.Items);
        Assert.Equal("evt-1", writer.Items[0].Id);
        Assert.Equal("host-1", writer.Items[0].HostId);
    }

    [Fact]
    public async Task IngestAsync_publishes_rdp_observation_when_enabled()
    {
        var observations = new FakeObservations();
        var svc = CreateService(writeEnabled: false, obsEnabled: true, new FakeWriter(), observations);
        await svc.IngestAsync(new IngestBatchRequest
        {
            Domain = "odak",
            HostId = "host-1",
            Hostname = "TERMINAL.odak.local",
            Events =
            [
                new IngestEventItem
                {
                    Source = "windows-eventlog",
                    SourceProduct = "rdp-session",
                    Fields = new Dictionary<string, object?>
                    {
                        ["eventId"] = 21,
                        ["eventData"] = new Dictionary<string, object?>
                        {
                            ["User"] = @"ODAK\monitra",
                            ["Address"] = "192.168.20.1"
                        }
                    }
                }
            ]
        });

        Assert.Single(observations.Published);
        Assert.Equal("rdp.logon", observations.Published[0].Key);
        Assert.Equal("TERMINAL", observations.Published[0].Dimensions["sourceHost"]);
    }

    [Fact]
    public async Task IngestAsync_skips_observation_when_publish_disabled()
    {
        var observations = new FakeObservations();
        var svc = CreateService(writeEnabled: false, obsEnabled: false, new FakeWriter(), observations);
        await svc.IngestAsync(new IngestBatchRequest
        {
            Domain = "odak",
            HostId = "host-1",
            Hostname = "TERMINAL",
            Events =
            [
                new IngestEventItem
                {
                    Source = "windows-eventlog",
                    SourceProduct = "rdp-session",
                    Fields = new Dictionary<string, object?> { ["eventId"] = 21 }
                }
            ]
        });

        Assert.Empty(observations.Published);
    }

    [Fact]
    public async Task IngestAsync_skips_observation_for_non_allowlisted_product()
    {
        var observations = new FakeObservations();
        var svc = CreateService(writeEnabled: false, obsEnabled: true, new FakeWriter(), observations);
        await svc.IngestAsync(new IngestBatchRequest
        {
            Domain = "odak",
            HostId = "host-1",
            Hostname = "TERMINAL",
            Events =
            [
                new IngestEventItem
                {
                    Source = "windows-eventlog",
                    SourceProduct = "windows-security",
                    Fields = new Dictionary<string, object?> { ["eventId"] = 4625 }
                }
            ]
        });

        Assert.Empty(observations.Published);
    }

    [Fact]
    public void BuildBulkNdjson_contains_index_and_id()
    {
        var docs = new List<OpenSearchSecEventDocument>
        {
            new()
            {
                Id = "abc",
                IngestedAtUtc = new DateTime(2026, 7, 29, 10, 0, 0, DateTimeKind.Utc),
                EventTimeUtc = new DateTime(2026, 7, 29, 9, 0, 0, DateTimeKind.Utc),
                HostId = "h1",
                Source = "windows-eventlog",
                Message = "hi"
            }
        };

        var ndjson = OpenSearchBulkWriter.BuildBulkNdjson("odak", docs);
        Assert.Contains("mng-odak-sec-events-2026.07.29", ndjson);
        Assert.Contains("\"_id\":\"abc\"", ndjson);
        Assert.Contains("\"@timestamp\"", ndjson);
        Assert.Contains("\"parser\"", ndjson);
        Assert.Contains("\"type\":\"windows-eventlog\"", ndjson);
        Assert.DoesNotContain("\"source\":\"windows-eventlog\"", ndjson);
    }

    private static IngestBatchService CreateService(
        bool writeEnabled,
        bool obsEnabled,
        IOpenSearchBulkWriter writer,
        IAgentObservationPublisher observations)
    {
        var settings = Options.Create(new MngLogCollectorSettings
        {
            OpenSearch = new OpenSearchSettings { WriteEnabled = writeEnabled, Url = "http://opensearch:9200" },
            Ingest = new IngestSettings { MaxEventsPerBatch = 500 },
            ObservationPublish = new ObservationPublishSettings
            {
                Enabled = obsEnabled,
                SourceProducts = ["rdp-session"]
            }
        });
        return new IngestBatchService(writer, observations, settings, NullLogger<IngestBatchService>.Instance);
    }

    private sealed class FakeWriter : IOpenSearchBulkWriter
    {
        public List<OpenSearchSecEventDocument> Items { get; } = [];

        public Task<int> IndexSecEventsAsync(
            string domain,
            IReadOnlyList<OpenSearchSecEventDocument> documents,
            CancellationToken cancellationToken = default)
        {
            Items.AddRange(documents);
            return Task.FromResult(documents.Count);
        }
    }

    private sealed class FakeObservations : IAgentObservationPublisher
    {
        public List<AgentObservationPayload> Published { get; } = [];

        public Task PublishEventAsync(AgentObservationPayload payload, CancellationToken cancellationToken = default)
        {
            Published.Add(payload);
            return Task.CompletedTask;
        }
    }
}
