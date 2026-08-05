namespace MngAlarm.Domain.Enums;

public enum AlarmStatus
{
    Active = 0,
    Acknowledged = 1,
    Resolved = 2,
    Suppressed = 3
}

public static class AlarmStatusExtensions
{
    public static bool IsOpen(this AlarmStatus status) =>
        status is AlarmStatus.Active or AlarmStatus.Acknowledged or AlarmStatus.Suppressed;
}
