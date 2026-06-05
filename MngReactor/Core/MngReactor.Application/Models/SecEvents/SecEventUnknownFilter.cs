namespace MngReactor.Application.Models.SecEvents;

public static class SecEventUnknownFilter
{
    public const string UnknownAction = "unknown";

    public static bool IsUnknown(ParsedSecEvent parsed) =>
        string.Equals(parsed.EventAction, UnknownAction, StringComparison.OrdinalIgnoreCase)
        || string.Equals(parsed.ParserId, "unknown.fallback.v1", StringComparison.OrdinalIgnoreCase);
}
