using MngAlarm.Application.Observations;
using MngAlarm.Domain.Constants;
using Xunit;

namespace MngAlarm.Tests;

public sealed class ObservationProcessorMappingTests
{
    [Theory]
    [InlineData(AlarmEventTypes.Raised, "AlarmRaised")]
    [InlineData(AlarmEventTypes.Updated, "AlarmUpdated")]
    [InlineData(AlarmEventTypes.Resolved, "AlarmResolved")]
    public void MapPayloadEventType_maps_lifecycle(string lifecycle, string expected) =>
        Assert.Equal(expected, AlarmLifecycleMapper.ToPayloadEventType(lifecycle));
}
