using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using MngReactor.Application.Abstractions.SecEvents;
using MngReactor.Application.Contracts.SecEvents;
using MngReactor.Application.Services.SecEvents;
using Xunit;

namespace MngReactor.Tests.Services.SecEvents;

/// <summary>
/// Smoke: RDP named EventData map + LogAlarm text regex via draft preview (no persist).
/// </summary>
public sealed class SecEventParseRuleDraftPreviewTests
{
    private const string Domain = "odak";

    [Fact]
    public async Task Preview_Draft_RdpFieldMap_ExtractsNamedEventData()
    {
        var sut = CreateSut();
        var preview = await sut.PreviewAsync(Domain, new SecEventParseRulePreviewRequest
        {
            DraftRule = new SecEventParseRuleUpsertRequest
            {
                RuleId = "custom.windows.rdp.21",
                Name = "RDP logon",
                Enabled = true,
                Priority = 100,
                Match = new SecEventParseRuleMatchDto
                {
                    SourceProduct = ["windows"],
                    SourceType = ["windows-eventlog"],
                    Channel = ["Microsoft-Windows-TerminalServices-LocalSessionManager/Operational"],
                    EventIds = [21]
                },
                Extract =
                [
                    new SecEventParseRuleExtractStepDto { Type = "event_data", From = "User", To = "actor.user" },
                    new SecEventParseRuleExtractStepDto { Type = "event_data", From = "Address", To = "network.srcIp" },
                    new SecEventParseRuleExtractStepDto { Type = "constant", To = "event.action", Value = "rdp.logon" },
                    new SecEventParseRuleExtractStepDto { Type = "constant", To = "event.outcome", Value = "success" }
                ]
            },
            Context = new SecEventParseRulePreviewContext
            {
                Source = new SecEventParseRulePreviewSource
                {
                    Product = "rdp-session",
                    Type = "windows-eventlog",
                    Host = "TERMINAL"
                },
                Channel = "Microsoft-Windows-TerminalServices-LocalSessionManager/Operational",
                EventId = 21,
                Message = "Remote Desktop Services: Session logon succeeded",
                Raw = JsonSerializer.Deserialize<object>("""
                    {
                      "channel": "Microsoft-Windows-TerminalServices-LocalSessionManager/Operational",
                      "eventId": 21,
                      "eventData": { "User": "ODAK\\monitra", "Address": "192.168.20.50", "SessionID": "2" },
                      "message": "Remote Desktop Services: Session logon succeeded"
                    }
                    """)
            }
        });

        Assert.True(preview.Matched);
        Assert.Equal("ODAK\\monitra", preview.Fields["actor.user"]?.ToString());
        Assert.Equal("192.168.20.50", preview.Fields["network.srcIp"]?.ToString());
        Assert.Equal("rdp.logon", preview.Fields["event.action"]?.ToString());
    }

    [Fact]
    public async Task Preview_Draft_LogAlarmText_ParsesMessage()
    {
        var sut = CreateSut();
        var preview = await sut.PreviewAsync(Domain, new SecEventParseRulePreviewRequest
        {
            DraftRule = new SecEventParseRuleUpsertRequest
            {
                RuleId = "custom.windows.app.logalarm.65002",
                Name = "LogAlarm RabbitMQ",
                Enabled = true,
                Priority = 100,
                Match = new SecEventParseRuleMatchDto
                {
                    SourceProduct = ["windows"],
                    SourceType = ["windows-eventlog"],
                    Channel = ["Application"],
                    EventIds = [65002]
                },
                Extract =
                [
                    new SecEventParseRuleExtractStepDto
                    {
                        Type = "regex",
                        From = "message",
                        Pattern = @"dial tcp (?<dst>[^:]+):(?<port>\d+)",
                        Groups = new Dictionary<string, string>
                        {
                            ["dst"] = "network.dstIp",
                            ["port"] = "network.dstPort"
                        }
                    },
                    new SecEventParseRuleExtractStepDto
                    {
                        Type = "constant",
                        To = "event.action",
                        Value = "app.rabbitmq.connect_failed"
                    },
                    new SecEventParseRuleExtractStepDto
                    {
                        Type = "constant",
                        To = "event.outcome",
                        Value = "failure"
                    }
                ]
            },
            Context = new SecEventParseRulePreviewContext
            {
                Source = new SecEventParseRulePreviewSource
                {
                    Product = "application-signals",
                    Type = "windows-eventlog",
                    Host = "TERMINAL"
                },
                Channel = "Application",
                EventId = 65002,
                Message =
                    "failed to connect rabbitmq server, error: dial tcp 192.168.20.17:5672: connectex: No connection could be made",
                Raw = JsonSerializer.Deserialize<object>("""
                    {
                      "channel": "Application",
                      "eventId": 65002,
                      "eventData": { "Data_0": "failed to connect rabbitmq server, error: dial tcp 192.168.20.17:5672: connectex: No connection could be made" },
                      "eventDataText": "failed to connect rabbitmq server, error: dial tcp 192.168.20.17:5672: connectex: No connection could be made",
                      "message": "failed to connect rabbitmq server, error: dial tcp 192.168.20.17:5672: connectex: No connection could be made"
                    }
                    """)
            }
        });

        Assert.True(preview.Matched);
        Assert.Equal("192.168.20.17", preview.Fields["network.dstIp"]?.ToString());
        Assert.Equal(5672, Convert.ToInt32(preview.Fields["network.dstPort"]));
        Assert.Equal("app.rabbitmq.connect_failed", preview.Fields["event.action"]?.ToString());
    }

    private static SecEventParseRuleCatalogService CreateSut()
    {
        return new SecEventParseRuleCatalogService(
            new EmptyStore(),
            new MockCache(),
            NullLogger<SecEventParseRuleCatalogService>.Instance);
    }

    private sealed class MockCache : ISecEventParseRuleCatalogCache
    {
        public Task<IReadOnlyList<MngReactor.Application.Models.SecEvents.SecEventParseRuleDocument>> GetEnabledRulesAsync(
            string domain,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MngReactor.Application.Models.SecEvents.SecEventParseRuleDocument>>([]);

        public void Invalidate(string domain)
        {
        }
    }

    /// <summary>Empty store so EnsureSeeded loads builtins; draft preview does not need persisted custom rules.</summary>
    private sealed class EmptyStore : ISecEventParseRuleCatalogStore
    {
        private MngReactor.Application.Models.SecEvents.SecEventParseCatalogMetaDocument? _meta =
            new() { Version = "1", PublishedUtc = DateTime.UtcNow };

        private readonly Dictionary<string, MngReactor.Application.Models.SecEvents.SecEventParseRuleDocument> _rules =
            new(StringComparer.OrdinalIgnoreCase);

        public Task EnsureIndexesAsync(string databaseName, CancellationToken ct = default) => Task.CompletedTask;

        public Task<IReadOnlyList<MngReactor.Application.Models.SecEvents.SecEventParseRuleDocument>> ListAsync(
            string databaseName,
            CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<MngReactor.Application.Models.SecEvents.SecEventParseRuleDocument>>(
                _rules.Values.ToList());

        public Task<MngReactor.Application.Models.SecEvents.SecEventParseRuleDocument?> GetByRuleIdAsync(
            string databaseName,
            string ruleId,
            CancellationToken ct = default)
        {
            _rules.TryGetValue(ruleId, out var doc);
            return Task.FromResult(doc);
        }

        public Task UpsertAsync(
            string databaseName,
            MngReactor.Application.Models.SecEvents.SecEventParseRuleDocument doc,
            CancellationToken ct = default)
        {
            _rules[doc.RuleId] = doc;
            return Task.CompletedTask;
        }

        public Task<bool> DeleteByRuleIdAsync(string databaseName, string ruleId, CancellationToken ct = default) =>
            Task.FromResult(_rules.Remove(ruleId));

        public Task<MngReactor.Application.Models.SecEvents.SecEventParseCatalogMetaDocument?> GetMetaAsync(
            string databaseName,
            CancellationToken ct = default) =>
            Task.FromResult(_meta);

        public Task SaveMetaAsync(
            string databaseName,
            MngReactor.Application.Models.SecEvents.SecEventParseCatalogMetaDocument meta,
            CancellationToken ct = default)
        {
            _meta = meta;
            return Task.CompletedTask;
        }

        public Task<long> CountAsync(string databaseName, CancellationToken ct = default) =>
            Task.FromResult((long)Math.Max(1, _rules.Count));

        public Task<IReadOnlyList<MngReactor.Application.Models.SecEvents.SecEventCustomFieldDocument>> ListCustomFieldsAsync(
            string databaseName,
            CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<MngReactor.Application.Models.SecEvents.SecEventCustomFieldDocument>>([]);

        public Task UpsertCustomFieldAsync(
            string databaseName,
            MngReactor.Application.Models.SecEvents.SecEventCustomFieldDocument doc,
            CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task<bool> DeleteCustomFieldAsync(
            string databaseName,
            string name,
            CancellationToken ct = default) =>
            Task.FromResult(false);
    }
}
