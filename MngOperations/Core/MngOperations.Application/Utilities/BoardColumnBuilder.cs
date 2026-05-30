using System.Text.Json;
using MngOperations.Application.Contracts.Runtime;

namespace MngOperations.Application.Utilities;

public static class BoardColumnBuilder
{
    public const string DefaultColumnQueryKey = "wi_board_column";

    public static IReadOnlyList<BoardColumnDto> Build(
        JsonElement? transitions,
        string? initialStateId,
        string workspaceId,
        string boardId,
        JsonElement? boardConfigColumns)
    {
        if (boardConfigColumns is { ValueKind: JsonValueKind.Array })
            return ParseConfigColumns(boardConfigColumns.Value, workspaceId, boardId, transitions);

        return BuildFromStateFlow(transitions, initialStateId, workspaceId, boardId);
    }

    private static IReadOnlyList<BoardColumnDto> ParseConfigColumns(
        JsonElement configColumns,
        string workspaceId,
        string boardId,
        JsonElement? transitions)
    {
        var list = new List<BoardColumnDto>();
        foreach (var col in configColumns.EnumerateArray())
        {
            var stateId = StateFlowCatalog.GetStringProperty(col, "stateId");
            if (string.IsNullOrEmpty(stateId))
                continue;

            var queryKey = StateFlowCatalog.GetStringProperty(col, "queryKey") ?? DefaultColumnQueryKey;
            list.Add(BuildColumn(
                stateId,
                StateFlowCatalog.GetStringProperty(col, "title"),
                queryKey,
                workspaceId,
                boardId,
                transitions,
                StateFlowCatalog.GetStringProperty(col, "defaultTransitionKey")));
        }

        return list;
    }

    private static IReadOnlyList<BoardColumnDto> BuildFromStateFlow(
        JsonElement? transitions,
        string? initialStateId,
        string workspaceId,
        string boardId)
    {
        var stateOrder = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        if (!string.IsNullOrEmpty(initialStateId) && seen.Add(initialStateId))
            stateOrder.Add(initialStateId);

        if (transitions is { ValueKind: JsonValueKind.Array })
        {
            foreach (var t in transitions.Value.EnumerateArray())
            {
                var to = StateFlowCatalog.GetStringProperty(t, "toStateId");
                if (!string.IsNullOrEmpty(to) && seen.Add(to))
                    stateOrder.Add(to);
            }
        }

        return stateOrder
            .Select(stateId => BuildColumn(stateId, null, DefaultColumnQueryKey, workspaceId, boardId, transitions, null))
            .ToList();
    }

    private static BoardColumnDto BuildColumn(
        string stateId,
        string? title,
        string queryKey,
        string workspaceId,
        string boardId,
        JsonElement? transitions,
        string? defaultTransitionKeyOverride)
    {
        var intoColumn = FindTransitionsToState(transitions, stateId);
        var defaultKey = defaultTransitionKeyOverride ?? intoColumn.FirstOrDefault()?.TransitionKey;
        var alternatives = intoColumn.Skip(1).Select(x => x.TransitionKey).Where(k => !string.IsNullOrEmpty(k)).ToList();

        return new BoardColumnDto
        {
            StateId = stateId,
            Title = title ?? stateId,
            DropEligible = intoColumn.Count > 0,
            DefaultTransitionKey = defaultKey,
            AlternativeTransitionKeys = alternatives,
            IncomingTransitions = intoColumn,
            QueryKey = queryKey,
            ParametersTemplate = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["workspaceId"] = workspaceId,
                ["boardId"] = boardId,
                ["stateId"] = stateId
            },
            SuggestedPageSize = 50
        };
    }

    private static List<BoardColumnTransitionDto> FindTransitionsToState(JsonElement? transitions, string stateId)
    {
        var list = new List<BoardColumnTransitionDto>();
        if (transitions is not { ValueKind: JsonValueKind.Array })
            return list;

        foreach (var t in transitions.Value.EnumerateArray())
        {
            var to = StateFlowCatalog.GetStringProperty(t, "toStateId");
            if (!string.Equals(to, stateId, StringComparison.Ordinal))
                continue;

            var key = StateFlowCatalog.GetStringProperty(t, "transitionKey");
            if (string.IsNullOrEmpty(key))
                continue;

            list.Add(new BoardColumnTransitionDto
            {
                TransitionKey = key,
                FromStateId = StateFlowCatalog.GetStringProperty(t, "fromStateId") ?? string.Empty,
                RequiredFields = StateFlowCatalog.GetRequiredFields(t)
            });
        }

        return list;
    }
}
