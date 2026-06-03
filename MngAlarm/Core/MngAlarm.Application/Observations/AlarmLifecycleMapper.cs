using MngAlarm.Domain.Constants;

namespace MngAlarm.Application.Observations;

public static class AlarmLifecycleMapper
{
    public static string ToPayloadEventType(string lifecycle) =>
        lifecycle switch
        {
            AlarmEventTypes.Raised => "AlarmRaised",
            AlarmEventTypes.Updated => "AlarmUpdated",
            AlarmEventTypes.Resolved => "AlarmResolved",
            _ => lifecycle
        };
}
