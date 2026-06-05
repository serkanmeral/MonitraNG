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
            Search = "admin",
            From = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            To = new DateTime(2026, 6, 2, 0, 0, 0, DateTimeKind.Utc)
        });

        Assert.NotNull(filter);
    }

    [Fact]
    public void Build_ExcludeUnknown_DoesNotThrow()
    {
        var filter = SecEventQueryFilterBuilder.Build(new SecEventQueryFilter
        {
            ExcludeUnknown = true,
            EventAction = "login_failed"
        });

        Assert.NotNull(filter);
    }
}
