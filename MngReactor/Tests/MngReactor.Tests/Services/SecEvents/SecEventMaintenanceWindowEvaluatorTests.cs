using Microsoft.Extensions.Options;
using MngReactor.Application.Configuration;
using MngReactor.Persistence.Services.SecEvents.Parsers;
using Xunit;

namespace MngReactor.Tests.Services.SecEvents;

public sealed class SecEventMaintenanceWindowEvaluatorTests
{
    [Fact]
    public void IsOutsideAllowedWindow_InsideDefaultWindow_ReturnsFalse()
    {
        var sut = CreateEvaluator(new SecEventMaintenanceWindowSettings
        {
            AllowedStartHourUtc = 8,
            AllowedEndHourUtc = 20
        });

        var inside = DateTime.Parse("2026-06-03T12:00:00Z").ToUniversalTime();
        Assert.False(sut.IsOutsideAllowedWindow(inside));
    }

    [Fact]
    public void IsOutsideAllowedWindow_OutsideDefaultWindow_ReturnsTrue()
    {
        var sut = CreateEvaluator(new SecEventMaintenanceWindowSettings
        {
            AllowedStartHourUtc = 8,
            AllowedEndHourUtc = 20
        });

        var outside = DateTime.Parse("2026-06-03T22:15:00Z").ToUniversalTime();
        Assert.True(sut.IsOutsideAllowedWindow(outside));
    }

    [Fact]
    public void IsOutsideAllowedWindow_Disabled_ReturnsFalse()
    {
        var sut = CreateEvaluator(new SecEventMaintenanceWindowSettings { Enabled = false });
        var outside = DateTime.Parse("2026-06-03T22:15:00Z").ToUniversalTime();
        Assert.False(sut.IsOutsideAllowedWindow(outside));
    }

    private static SecEventMaintenanceWindowEvaluator CreateEvaluator(
        SecEventMaintenanceWindowSettings settings) =>
        new(Options.Create(new MngReactorSettings { SecEventMaintenanceWindow = settings }));
}
