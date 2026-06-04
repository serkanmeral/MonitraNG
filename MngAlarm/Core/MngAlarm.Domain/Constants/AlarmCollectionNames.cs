namespace MngAlarm.Domain.Constants;

public static class AlarmCollectionNames
{
    public const string Rules = "@mon_alarm_rules";
    public const string Alarms = "@mon_alarms";
    public const string CorrelationWindows = "@mon_alarm_correlation_windows";
    public const string ObservationActivity = "@mon_alarm_observation_activity";
}

public static class AlarmMessagingConstants
{
    public const string Exchange = "mng.alarms";
    public const string ObservationQueue = "alarm.observation.inbound";
    public const string ObservationExchange = "monitra.observations";
    public const string ObservationRoutingPattern = "*.metric.*";
    public const string ObservationEventRoutingPattern = "*.event.*";

    /// <summary>MngReactor metric publish exchange (legacy path until Reactor emits monitra.observations directly).</summary>
    public const string ReactorMetricsExchange = "mng.topics";
    public const string ReactorMetricsRoutingPattern = "monitoring.metric.inserted.#";
    public const string ReactorMetricsBridgeQueue = "alarm.reactor.metric.bridge";
}

public static class AlarmRuleTypes
{
    public const string Threshold = "threshold";
    public const string Correlation = "correlation";
    public const string Scheduled = "scheduled";
}

public static class AlarmEventTypes
{
    public const string Raised = "alarm.raised";
    public const string Updated = "alarm.updated";
    public const string Resolved = "alarm.resolved";
}
