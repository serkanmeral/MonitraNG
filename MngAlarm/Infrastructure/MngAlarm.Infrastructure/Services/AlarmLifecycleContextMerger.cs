namespace MngAlarm.Infrastructure.Services;

internal static class AlarmLifecycleContextMerger
{
    private static readonly string[] PreservedKeys =
    [
        "lifecycleHistory",
        "manualAction",
        "manualActionAt",
        "manualActionBy",
        "manualActionByUserId",
    ];

    public static void PreserveFromExisting(Dictionary<string, object?> incoming, Dictionary<string, object?> existing)
    {
        foreach (var key in PreservedKeys)
        {
            if (!existing.TryGetValue(key, out var value) || value is null)
                continue;

            incoming[key] = value;
        }
    }
}
