namespace MngLogs.Agent.EventLog;

/// <summary>
/// Enriches Service Control Manager System events (7031/7034/7036/7040/7045)
/// with structured fields for SIEM correlation and service-watch matching.
/// </summary>
public static class ServiceControlEventEnricher
{
    private static readonly HashSet<int> ServiceEventIds = [7031, 7034, 7036, 7040, 7045];

    public static bool IsServiceControlEvent(int eventId) => ServiceEventIds.Contains(eventId);

    /// <summary>
    /// Mutates <paramref name="fields"/> with serviceName / event.action / detail keys.
    /// Returns false when eventId is not an SCM service event or service name is missing.
    /// </summary>
    public static bool TryEnrich(
        int eventId,
        IReadOnlyList<string?> properties,
        IDictionary<string, object?> fields,
        out string? action)
    {
        action = null;
        if (!IsServiceControlEvent(eventId))
            return false;

        var serviceName = Prop(properties, 0);
        if (string.IsNullOrWhiteSpace(serviceName))
            return false;

        fields["serviceName"] = serviceName.Trim();
        fields["watchKind"] = "service";

        switch (eventId)
        {
            case 7031:
                action = "service.os.crash";
                fields["crashCount"] = ParseInt(Prop(properties, 1));
                fields["milliseconds"] = ParseInt(Prop(properties, 2));
                fields["recoveryAction"] = Prop(properties, 3);
                break;
            case 7034:
                action = "service.os.crash";
                fields["crashCount"] = ParseInt(Prop(properties, 1));
                break;
            case 7036:
                action = "service.os.state_change";
                fields["serviceState"] = Prop(properties, 1);
                break;
            case 7040:
                action = "service.os.start_type_changed";
                fields["startTypeOld"] = Prop(properties, 1);
                fields["startTypeNew"] = Prop(properties, 2);
                break;
            case 7045:
                action = "service.os.installed";
                fields["imagePath"] = Prop(properties, 1);
                fields["serviceType"] = Prop(properties, 2);
                fields["startType"] = Prop(properties, 3);
                fields["serviceAccount"] = Prop(properties, 4);
                break;
        }

        if (action != null)
            fields["event.action"] = action;

        return true;
    }

    public static string? Prop(IReadOnlyList<string?> properties, int index) =>
        index >= 0 && index < properties.Count ? properties[index] : null;

    private static int? ParseInt(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        return int.TryParse(value.Trim(), out var n) ? n : null;
    }
}
