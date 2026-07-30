using MngLogs.Agent.EventLog;

namespace MngLogs.Tests;

public class ServiceControlEventEnricherTests
{
    [Fact]
    public void Enrich_7034_sets_serviceName_and_crash_action()
    {
        var fields = new Dictionary<string, object?>();
        var ok = ServiceControlEventEnricher.TryEnrich(
            7034,
            ["Spooler", "2"],
            fields,
            out var action);

        Assert.True(ok);
        Assert.Equal("service.os.crash", action);
        Assert.Equal("Spooler", fields["serviceName"]);
        Assert.Equal("service.os.crash", fields["event.action"]);
        Assert.Equal(2, fields["crashCount"]);
    }

    [Fact]
    public void Enrich_7036_sets_state_change()
    {
        var fields = new Dictionary<string, object?>();
        var ok = ServiceControlEventEnricher.TryEnrich(
            7036,
            ["Print Spooler", "running"],
            fields,
            out var action);

        Assert.True(ok);
        Assert.Equal("service.os.state_change", action);
        Assert.Equal("Print Spooler", fields["serviceName"]);
        Assert.Equal("running", fields["serviceState"]);
    }

    [Fact]
    public void Enrich_7040_sets_start_type()
    {
        var fields = new Dictionary<string, object?>();
        Assert.True(ServiceControlEventEnricher.TryEnrich(
            7040,
            ["Spooler", "auto start", "disabled"],
            fields,
            out var action));
        Assert.Equal("service.os.start_type_changed", action);
        Assert.Equal("auto start", fields["startTypeOld"]);
        Assert.Equal("disabled", fields["startTypeNew"]);
    }

    [Fact]
    public void Enrich_ignores_non_scm_events()
    {
        var fields = new Dictionary<string, object?>();
        Assert.False(ServiceControlEventEnricher.TryEnrich(6005, ["x"], fields, out _));
        Assert.Empty(fields);
    }

    [Fact]
    public void Enrich_requires_service_name()
    {
        var fields = new Dictionary<string, object?>();
        Assert.False(ServiceControlEventEnricher.TryEnrich(7034, [""], fields, out _));
    }
}
