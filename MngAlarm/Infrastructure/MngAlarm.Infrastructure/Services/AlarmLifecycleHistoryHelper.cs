using System.Text.Json;
using MngAlarm.Application.Contracts;
using MngAlarm.Domain.Enums;

namespace MngAlarm.Infrastructure.Services;

internal static class AlarmLifecycleHistoryHelper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public static void AppendManualEntry(
        IDictionary<string, object?> context,
        AlarmStatus fromStatus,
        AlarmStatus toStatus,
        AlarmActorContext actor)
    {
        var at = DateTime.UtcNow.ToString("O");
        var byUserName = actor.UserName ?? actor.UserId ?? "unknown";

        var entry = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["action"] = toStatus.ToString(),
            ["fromStatus"] = fromStatus.ToString(),
            ["toStatus"] = toStatus.ToString(),
            ["at"] = at,
            ["byUserId"] = actor.UserId,
            ["byUserName"] = byUserName,
            ["source"] = "manual",
        };

        var history = ReadHistory(context);
        history.Add(entry);
        context["lifecycleHistory"] = history;

        context["manualAction"] = toStatus.ToString();
        context["manualActionAt"] = at;
        context["manualActionBy"] = byUserName;
        if (!string.IsNullOrWhiteSpace(actor.UserId))
            context["manualActionByUserId"] = actor.UserId;
    }

    public static void AppendSystemEntry(
        IDictionary<string, object?> context,
        AlarmStatus fromStatus,
        AlarmStatus toStatus,
        string reason)
    {
        var at = DateTime.UtcNow.ToString("O");
        var entry = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["action"] = toStatus.ToString(),
            ["fromStatus"] = fromStatus.ToString(),
            ["toStatus"] = toStatus.ToString(),
            ["at"] = at,
            ["byUserName"] = "system",
            ["source"] = "automatic",
            ["reason"] = reason,
        };

        var history = ReadHistory(context);
        history.Add(entry);
        context["lifecycleHistory"] = history;
    }

    private static List<Dictionary<string, object?>> ReadHistory(IDictionary<string, object?> context)
    {
        if (!context.TryGetValue("lifecycleHistory", out var raw) || raw is null)
            return [];

        try
        {
            if (raw is List<Dictionary<string, object?>> typed)
                return typed;

            if (raw is IEnumerable<object> enumerable)
            {
                var list = new List<Dictionary<string, object?>>();
                foreach (var item in enumerable)
                {
                    var json = JsonSerializer.Serialize(item, JsonOptions);
                    var dict = JsonSerializer.Deserialize<Dictionary<string, object?>>(json, JsonOptions);
                    if (dict != null)
                        list.Add(dict);
                }

                return list;
            }

            var serialized = JsonSerializer.Serialize(raw, JsonOptions);
            return JsonSerializer.Deserialize<List<Dictionary<string, object?>>>(serialized, JsonOptions) ?? [];
        }
        catch
        {
            return [];
        }
    }
}
