using System.Globalization;
using System.Text.Json;
using MngOperations.Application.Contracts.Runtime;

namespace MngOperations.Application.Utilities;

/// <summary>
/// Dashboard widgets count rows or sum story points. A missing point is not zero.
/// </summary>
public static class DashboardMeasure
{
    public const string Count = "count";
    public const string Sum = "sum";
    public const string StoryPoints = "storyPoints";
    public const string Unpointed = "unpointed";

    public static bool IsSum(string? measure) =>
        string.Equals(measure?.Trim(), Sum, StringComparison.OrdinalIgnoreCase);

    public static double? ReadStoryPoints(WorkItemCardDto card)
    {
        if (card.Fields is not { ValueKind: JsonValueKind.Object } fields)
            return null;
        if (!fields.TryGetProperty("storyPoints", out var value)
            && !fields.TryGetProperty("StoryPoints", out value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number))
            return number;
        if (value.ValueKind == JsonValueKind.String
            && double.TryParse(value.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        return null;
    }

    public static (double Value, int Unpointed) SumPoints(IReadOnlyList<WorkItemCardDto> cards)
    {
        double sum = 0;
        var missing = 0;
        foreach (var card in cards)
        {
            var points = ReadStoryPoints(card);
            if (points is null)
            {
                missing++;
                continue;
            }

            sum += points.Value;
        }

        return (sum, missing);
    }
}
