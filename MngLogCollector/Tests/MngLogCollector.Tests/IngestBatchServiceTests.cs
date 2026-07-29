using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
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
        var svc = CreateService(writeEnabled: false, writer: new FakeWriter());
        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.IngestAsync(new IngestBatchRequest { Domain = "", HostId = "h1" }));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.IngestAsync(new IngestBatchRequest { Domain = "odak", HostId = "" }));
    }

    [Fact]
    public async Task IngestAsync_accepts_without_write_when_disabled()
    {
        var writer = new FakeWriter();
        var svc = CreateService(writeEnabled: false, writer);
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
        var svc = CreateService(writeEnabled: true, writer);
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

    private static IngestBatchService CreateService(bool writeEnabled, IOpenSearchBulkWriter writer)
    {
        var settings = Options.Create(new MngLogCollectorSettings
        {
            OpenSearch = new OpenSearchSettings { WriteEnabled = writeEnabled, Url = "http://opensearch:9200" },
            Ingest = new IngestSettings { MaxEventsPerBatch = 500 }
        });
        return new IngestBatchService(writer, settings, NullLogger<IngestBatchService>.Instance);
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
}
