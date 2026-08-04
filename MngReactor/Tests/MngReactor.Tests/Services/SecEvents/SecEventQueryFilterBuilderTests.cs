using MngReactor.Application.Models.SecEvents;
using MngReactor.Persistence.Services.SecEvents;
using Xunit;

namespace MngReactor.Tests.Services.SecEvents;

public sealed class SecEventQueryFilterBuilderTests
{
    [Fact]
    public void NormalizeLimit_CapsAtMax()
    {
        Assert.Equal(200, SecEventQueryFilterBuilder.NormalizeLimit(500));
        Assert.Equal(50, SecEventQueryFilterBuilder.NormalizeLimit(0));
    }

    [Fact]
    public void NormalizeSkip_NeverNegative()
    {
        Assert.Equal(0, SecEventQueryFilterBuilder.NormalizeSkip(-5));
        Assert.Equal(10, SecEventQueryFilterBuilder.NormalizeSkip(10));
    }

    [Fact]
    public void Build_WithFilters_DoesNotThrow()
    {
        var filter = SecEventQueryFilterBuilder.Build(new SecEventQueryFilter
        {
            SourceType = "ad",
            EventAction = "login_failed",
            SrcIp = "10.0.0.1",
            DstIp = "10.0.0.2",
            ActorUser = "admin",
            SourceHost = "dc01",
            EventCode = "4625",
            Search = "admin",
            From = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            To = new DateTime(2026, 6, 2, 0, 0, 0, DateTimeKind.Utc)
        });

        Assert.NotNull(filter);
    }

    [Fact]
    public void Build_WithSourceProductAndEventCodes_DoesNotThrow()
    {
        var filter = SecEventQueryFilterBuilder.Build(new SecEventQueryFilter
        {
            SourceProduct = "rdp-session",
            EventCodes = "24,25",
            SourceHosts = "TERMINAL,HOST2",
            ExcludeUnknown = false
        });

        Assert.NotNull(filter);
    }

    [Fact]
    public void Build_WithFieldFilters_CustomAndCore_DoesNotThrow()
    {
        var filter = SecEventQueryFilterBuilder.Build(new SecEventQueryFilter
        {
            FieldFilters =
            [
                new SecEventFieldFilterClause { Field = "custom.session_id", Op = "eq", Value = "abc" },
                new SecEventFieldFilterClause { Field = "message", Op = "contains", Value = "failed" },
                new SecEventFieldFilterClause { Field = "network.protocol", Op = "eq", Value = "tcp" },
            ],
            ExcludeUnknown = false
        });

        Assert.NotNull(filter);
    }
}
