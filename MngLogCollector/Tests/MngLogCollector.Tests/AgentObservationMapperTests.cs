using System.Text.Json;
using MngLogCollector.Application.Configuration;
using MngLogCollector.Application.Contracts.Ingest;
using MngLogCollector.Application.Services.Ingest;

namespace MngLogCollector.Tests;

public class AgentObservationMapperTests
{
    [Fact]
    public void TryMap_rdp_logon_maps_key_and_dimensions()
    {
        var item = new IngestEventItem
        {
            Id = "evt-21",
            Source = "windows-eventlog",
            SourceProduct = "rdp-session",
            TimestampUtc = new DateTime(2026, 8, 7, 9, 0, 0, DateTimeKind.Utc),
            Fields = new Dictionary<string, object?>
            {
                ["eventId"] = 21,
                ["eventData"] = new Dictionary<string, object?>
                {
                    ["User"] = @"ODAK\monitra",
                    ["Address"] = "192.168.20.1",
                    ["SessionID"] = "3"
                }
            }
        };

        var payload = AgentObservationMapper.TryMap("odak", "host-1", "TERMINAL.odak.local", item);

        Assert.NotNull(payload);
        Assert.Equal("rdp.logon", payload!.Key);
        Assert.Equal("odak", payload.DomainId);
        Assert.Equal("odak", payload.DomainName);
        Assert.Equal(@"ODAK\monitra", payload.Dimensions["userId"]);
        Assert.Equal("192.168.20.1", payload.Dimensions["srcIp"]);
        Assert.Equal("TERMINAL", payload.Dimensions["sourceHost"]);
        Assert.Equal("windows-eventlog", payload.Dimensions["sourceType"]);
        Assert.Equal("rdp-session", payload.Dimensions["sourceProduct"]);
        Assert.Equal("mnglogcollector", payload.Dimensions["parserId"]);
        Assert.Equal("21", payload.Dimensions["eventCode"]);
        Assert.Equal("evt-21", payload.Dimensions["secEventId"]);
    }

    [Fact]
    public void TryMap_powershell_uses_package_key_and_keeps_eventCode()
    {
        var item = new IngestEventItem
        {
            Id = "ps-400",
            Source = "windows-eventlog",
            SourceProduct = "powershell-engine",
            Fields = new Dictionary<string, object?> { ["eventId"] = 400 }
        };

        var payload = AgentObservationMapper.TryMap("odak", "host-1", "TERMINAL", item);

        Assert.NotNull(payload);
        Assert.Equal("powershell-engine", payload!.Key);
        Assert.Equal("400", payload.Dimensions["eventCode"]);
        Assert.Equal("TERMINAL", payload.Dimensions["sourceHost"]);
        Assert.Equal("powershell-engine", payload.Dimensions["sourceProduct"]);
    }

    [Fact]
    public void TryMap_rdp_unmapped_event_falls_back_to_package_key()
    {
        var item = new IngestEventItem
        {
            Source = "windows-eventlog",
            SourceProduct = "rdp-session",
            Fields = new Dictionary<string, object?> { ["eventId"] = 999 }
        };
        var payload = AgentObservationMapper.TryMap("odak", "h1", "TERMINAL", item);
        Assert.NotNull(payload);
        Assert.Equal("rdp-session", payload!.Key);
        Assert.Equal("999", payload.Dimensions["eventCode"]);
    }

    [Fact]
    public void ShortHostName_strips_dns_suffix()
    {
        Assert.Equal("TERMINAL", AgentObservationMapper.ShortHostName("TERMINAL.odak.local", "h1"));
        Assert.Equal("TERMINAL", AgentObservationMapper.ShortHostName("TERMINAL", "h1"));
        Assert.Equal("h1", AgentObservationMapper.ShortHostName(null, "h1"));
    }

    [Fact]
    public void IsSourceProductAllowed_wildcard_and_allowlist()
    {
        var all = new ObservationPublishSettings
        {
            Enabled = true,
            SourceProducts = ["*"]
        };
        Assert.True(AgentObservationMapper.IsSourceProductAllowed(all, "powershell-engine"));
        Assert.True(AgentObservationMapper.IsSourceProductAllowed(all, "rdp-session"));
        Assert.False(AgentObservationMapper.IsSourceProductAllowed(all, null));

        var emptyMeansAll = new ObservationPublishSettings
        {
            Enabled = true,
            SourceProducts = []
        };
        Assert.True(AgentObservationMapper.IsSourceProductAllowed(emptyMeansAll, "security-auth"));

        var settings = new ObservationPublishSettings
        {
            Enabled = true,
            SourceProducts = ["rdp-session"]
        };
        Assert.True(AgentObservationMapper.IsSourceProductAllowed(settings, "rdp-session"));
        Assert.True(AgentObservationMapper.IsSourceProductAllowed(settings, "RDP-SESSION"));
        Assert.False(AgentObservationMapper.IsSourceProductAllowed(settings, "windows-security"));

        settings.Enabled = false;
        Assert.False(AgentObservationMapper.IsSourceProductAllowed(settings, "rdp-session"));
    }

    [Fact]
    public void SerializeEventPayload_includes_kind_and_key()
    {
        var payload = AgentObservationMapper.TryMap(
            "odak",
            "h1",
            "TERMINAL",
            new IngestEventItem
            {
                Source = "windows-eventlog",
                SourceProduct = "rdp-session",
                Fields = new Dictionary<string, object?> { ["eventId"] = "21" }
            })!;
        var json = AgentObservationMapper.SerializeEventPayload(payload);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("event", doc.RootElement.GetProperty("kind").GetString());
        Assert.Equal("rdp.logon", doc.RootElement.GetProperty("key").GetString());
        Assert.Equal(
            "odak.event.rdp.logon",
            AgentObservationMapper.BuildEventRoutingKey("odak", "rdp.logon"));
    }
}
